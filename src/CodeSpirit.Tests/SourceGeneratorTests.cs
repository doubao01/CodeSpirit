using System.Collections.Immutable;
using System.Reflection;
using CodeSpirit.Core;
using CodeSpirit.Core.Attributes;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CodeSpirit.Tests;

public class SourceGeneratorTests
{
    private static (ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<GeneratedSourceResult> GeneratedSources) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ViewModel).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CommandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CodeSpiritServiceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results.SelectMany(r => r.GeneratedSources).ToImmutableArray();

        return (diagnostics, generatedSources);
    }

    [Fact]
    public void Generator_CommandReturningNonTask_EmitsCSP014()
    {
        const string source = """
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/test")]
public class MyViewModel : ViewModel
{
    [Command]
    public string Calculate()
    {
        return "result";
    }
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp014 = diagnostics.FirstOrDefault(d => d.Id == "CSP014");

        Assert.NotNull(csp014);
        Assert.Equal(DiagnosticSeverity.Warning, csp014!.Severity);
        Assert.Contains("Calculate", csp014.GetMessage());
        Assert.Contains("string", csp014.GetMessage());
    }

    [Fact]
    public void Generator_CommandReturningVoidOrTask_NoCSP014()
    {
        const string source = """
using System.Threading.Tasks;
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/test")]
public class ValidViewModel : ViewModel
{
    [Command]
    public void Refresh()
    {
    }

    [Command]
    public Task SaveAsync()
    {
        return Task.CompletedTask;
    }

    [Command]
    public ValueTask SubmitAsync()
    {
        return default;
    }
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp014 = diagnostics.Where(d => d.Id == "CSP014").ToList();

        Assert.Empty(csp014);
    }

    [Fact]
    public void Generator_DuplicateBindingNames_EmitsCSP010()
    {
        const string source = """
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/test")]
public class DupBindViewModel : ViewModel
{
    [Bind(Name = "dup")]
    public string Prop1 { get; set; } = "";

    [Bind(Name = "dup")]
    public string Prop2 { get; set; } = "";
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp010 = diagnostics.FirstOrDefault(d => d.Id == "CSP010");

        Assert.NotNull(csp010);
        Assert.Contains("dup", csp010!.GetMessage());
    }

    [Fact]
    public void Generator_TwoWayBindingsWithoutName_DoNotEmitCSP010()
    {
        const string source = """
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/test")]
public class ValidBindViewModel : ViewModel
{
    [Bind(BindDirection.TwoWay)]
    public string First { get; set; } = "";

    [Bind(BindDirection.TwoWay)]
    public string Second { get; set; } = "";
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp010 = diagnostics.Where(d => d.Id == "CSP010").ToList();

        Assert.Empty(csp010);
    }

    [Fact]
    public void Generator_CommandWithParameters_EmitsCSP003()
    {
        const string source = """
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/test")]
public class ParamCommandViewModel : ViewModel
{
    [Command]
    public void Execute(int count)
    {
    }
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp003 = diagnostics.FirstOrDefault(d => d.Id == "CSP003");

        Assert.NotNull(csp003);
        Assert.Equal(DiagnosticSeverity.Error, csp003!.Severity);
    }

    [Fact]
    public void Generator_AsyncVoidCommand_EmitsCSP012()
    {
        const string source = """
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/test")]
public class AsyncVoidViewModel : ViewModel
{
    [Command]
    public async void Run()
    {
        await System.Threading.Tasks.Task.Delay(1);
    }
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp012 = diagnostics.FirstOrDefault(d => d.Id == "CSP012");

        Assert.NotNull(csp012);
        Assert.Equal(DiagnosticSeverity.Warning, csp012!.Severity);
    }

    [Fact]
    public void Generator_ServiceRegistration_GeneratesCorrectRegistrations()
    {
        const string source = """
using CodeSpirit.Core.Attributes;

namespace TestApp;

public interface IGreeterService
{
    string Greet();
}

[Service]
public class GreeterService : IGreeterService
{
    public string Greet() => "hello";
}

[Service(Lifetime = ServiceLifetime.Singleton)]
public class StandaloneService
{
}
""";

        var (diagnostics, generatedSources) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));

        var regSource = generatedSources.First(s => s.HintName == "CodeSpiritServiceRegistration.g.cs");
        var text = regSource.SourceText.ToString();

        Assert.Contains("services.AddScoped<TestApp.IGreeterService, TestApp.GreeterService>();", text);
        Assert.Contains("services.AddSingleton<TestApp.StandaloneService>();", text);
    }

    [Fact]
    public void Generator_PageDirectiveWithoutViewModelBase_EmitsCSP015()
    {
        const string source = """
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/broken")]
public class BrokenController
{
    public string Title => "broken";
}
""";

        var (diagnostics, _) = RunGenerator(source);
        var csp015 = diagnostics.FirstOrDefault(d => d.Id == "CSP015");

        Assert.NotNull(csp015);
        Assert.Equal(DiagnosticSeverity.Warning, csp015!.Severity);
        Assert.Contains("BrokenController", csp015.GetMessage());
    }

    [Fact]
    public void Generator_PageDirectiveOnViewModel_NoCSP015()
    {
        const string source = """
using CodeSpirit.Core;
using CodeSpirit.Core.Page;

namespace TestApp;

[PageDirective(Route = "/ok")]
public class OkViewModel : ViewModel
{
}
""";

        var (diagnostics, _) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Id == "CSP015"));
    }
}
