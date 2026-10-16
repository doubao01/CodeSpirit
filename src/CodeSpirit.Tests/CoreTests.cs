using CodeSpirit.Core;
using CodeSpirit.Core.Attributes;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Core.Page;

namespace CodeSpirit.Tests;

public class AttributeTests
{
    [Fact]
    public void ServiceAttribute_DefaultsToScoped()
    {
        var attr = new ServiceAttribute();
        Assert.Equal(ServiceLifetime.Scoped, attr.Lifetime);
    }

    [Fact]
    public void ServiceAttribute_CanSetServiceType()
    {
        var attr = new ServiceAttribute { ServiceType = typeof(IDisposable) };
        Assert.Equal(typeof(IDisposable), attr.ServiceType);
    }
}

public class RepositoryAttribute_Tests
{
    [Fact]
    public void RepositoryAttribute_CanApplyToClass()
    {
        var attr = Attribute.GetCustomAttribute(typeof(FakeRepo), typeof(RepositoryAttribute));
        Assert.NotNull(attr);
    }

    [Repository]
    class FakeRepo;
}

public class AutowiredAttribute_Tests
{
    [Fact]
    public void AutowiredAttribute_TargetsPropertyAndField()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AutowiredAttribute), typeof(AttributeUsageAttribute));
        Assert.NotNull(usage);
    }
}

public class ValueAttribute_Tests
{
    [Fact]
    public void ValueAttribute_StoresKey()
    {
        var attr = new ValueAttribute("Config:Key");
        Assert.Equal("Config:Key", attr.Key);
    }
}

public class ScheduledAttribute_Tests
{
    [Fact]
    public void ScheduledAttribute_StoresCronAndName()
    {
        var attr = new ScheduledAttribute("0 */5 * * * ?") { Name = "MyJob" };
        Assert.Equal("0 */5 * * * ?", attr.Cron);
        Assert.Equal("MyJob", attr.Name);
    }

    [Fact]
    public void EveryAttribute_StoresInterval()
    {
        var attr = new EveryAttribute(60);
        Assert.Equal(60, attr.Seconds);
    }

    [Fact]
    public void OnStartupAttribute_StoresDelay()
    {
        var attr = new OnStartupAttribute(2000);
        Assert.Equal(2000, attr.DelayMs);
    }
}

public class HttpAttribute_Tests
{
    [Fact]
    public void HttpGetAttribute_StoresPath()
    {
        var attr = new HttpGetAttribute("/api/users");
        Assert.Equal("/api/users", attr.Url);
    }

    [Fact]
    public void HttpPostAttribute_StoresPath()
    {
        var attr = new HttpPostAttribute("/api/users");
        Assert.Equal("/api/users", attr.Url);
    }
}

public class PageDirectiveAttribute_Tests
{
    [Fact]
    public void PageDirectiveAttribute_StoresRouteAndTitle()
    {
        var attr = new PageDirectiveAttribute { Route = "/weather", Title = "Weather" };
        Assert.Equal("/weather", attr.Route);
        Assert.Equal("Weather", attr.Title);
    }
}

public class BindAttribute_Tests
{
    [Fact]
    public void BindAttribute_DefaultsToOneWay()
    {
        var attr = new BindAttribute();
        Assert.Equal(BindDirection.OneWay, attr.Direction);
    }

    [Fact]
    public void BindAttribute_CanConfigureNameAndDirection()
    {
        var attr = new BindAttribute { Name = "city", Direction = BindDirection.TwoWay };
        Assert.Equal("city", attr.Name);
        Assert.Equal(BindDirection.TwoWay, attr.Direction);
    }
}

public class CommandAttribute_Tests
{
    [Fact]
    public void CommandAttribute_StoresName()
    {
        var attr = new CommandAttribute("Save");
        Assert.Equal("Save", attr.Name);
    }
}

public class DependsOnAttribute_Tests
{
    [Fact]
    public void DependsOnAttribute_StoresModuleTypes()
    {
        var attr = new DependsOnAttribute(typeof(AutowiredAttribute), typeof(ValueAttribute));
        Assert.Equal(2, attr.ModuleTypes.Length);
        Assert.Equal(typeof(AutowiredAttribute), attr.ModuleTypes[0]);
    }
}

public class TransactionalAttribute_Tests
{
    [Fact]
    public void TransactionalAttribute_TargetsMethod()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(TransactionalAttribute), typeof(AttributeUsageAttribute));
        Assert.NotNull(usage);
    }
}

public class CacheableAttribute_Tests
{
    [Fact]
    public void CacheableAttribute_DefaultsTo300Seconds()
    {
        var attr = new CacheableAttribute();
        Assert.Equal(300, attr.ExpirationSeconds);
    }

    [Fact]
    public void CacheableAttribute_CanSetKey()
    {
        var attr = new CacheableAttribute { CacheKey = "users:{0}" };
        Assert.Equal("users:{0}", attr.CacheKey);
    }
}

public class AssembliesTests
{
    [Fact]
    public void Assemblies_All_ReturnsAtLeastOne()
    {
        Assert.NotEmpty(CodeSpirit.Core.Assemblies.All);
    }

    [Fact]
    public void Assemblies_CodeSpirit_ContainsCoreAndEntry()
    {
        var names = CodeSpirit.Core.Assemblies.CodeSpirit
            .Select(a => a.GetName().Name)
            .ToList();

        Assert.Contains(names, n => n == "CodeSpirit.Core");
        Assert.Contains(names, n => n == "CodeSpirit.Tests");
    }
}

public class ViewModelTests
{
    [Fact]
    public async Task ViewModel_ToState_ReturnsBindProperties()
    {
        var vm = new TestViewModel();
        await vm.InitAsync();
        await vm.LoadAsync();
        await vm.RenderAsync();

        var state = vm.ToState();
        Assert.Equal("hello", state["Greeting"]);
    }

    [Fact]
    public void ViewModel_ToResponse_ReturnsBindingsAndCommands()
    {
        var vm = new TestViewModel { Greeting = "hello" };

        var response = vm.ToResponse();

        Assert.Equal("hello", response.State["Greeting"]);
        Assert.Equal("TwoWay", response.Bindings["Greeting"].Direction);
        Assert.Contains("Refresh", response.Commands);
    }

    [Fact]
    public void ViewModel_ModelState_IsValidByDefault()
    {
        var vm = new TestViewModel();
        Assert.True(vm.IsValid);
        Assert.Empty(vm.ModelState);
    }

    [Fact]
    public void ViewModel_AddModelError_PopulatesModelState()
    {
        var vm = new TestViewModel();
        vm.AddModelError("Greeting", "Greeting is required");

        Assert.False(vm.IsValid);
        Assert.Single(vm.ModelState);
        Assert.Equal("Greeting is required", vm.ModelState["Greeting"][0]);
    }

    [Fact]
    public void ViewModel_AddModelError_MultipleErrorsForSameField()
    {
        var vm = new TestViewModel();
        vm.AddModelError("Greeting", "Too short");
        vm.AddModelError("Greeting", "No special chars");

        Assert.Equal(2, vm.ModelState["Greeting"].Count);
    }

    [Fact]
    public void ViewModel_ToResponse_IncludesErrors()
    {
        var vm = new TestViewModel { Greeting = "" };
        vm.AddModelError("Greeting", "Greeting is required");
        vm.AddModelError("Greeting", "Too short");

        var response = vm.ToResponse();

        Assert.NotNull(response.Errors);
        Assert.Contains("Greeting is required", response.Errors!["Greeting"]);
        Assert.Contains("Too short", response.Errors!["Greeting"]);
    }

    [Fact]
    public void ViewModel_ToResponse_NoErrors_WhenValid()
    {
        var vm = new TestViewModel { Greeting = "hello" };

        var response = vm.ToResponse();

        Assert.Null(response.Errors);
    }

    [Fact]
    public void ToneAnalyzer_ForStatus_HandlesWhitespaceAndCase()
    {
        Assert.Equal("green", ToneAnalyzer.ForStatus("  AVAILABLE  "));
        Assert.Equal("red", ToneAnalyzer.ForStatus(" Error\n"));
        Assert.Equal("amber", ToneAnalyzer.ForStatus(" pending\t"));
        Assert.Null(ToneAnalyzer.ForStatus("unknown_status"));
        Assert.Null(ToneAnalyzer.ForStatus(null));
    }

    [Fact]
    public void ToneAnalyzer_ForNumeric_HandlesNullOrFewSiblings()
    {
        Assert.Equal("green", ToneAnalyzer.ForNumeric(10m, null));
        Assert.Equal("red", ToneAnalyzer.ForNumeric(-5m, null));
        Assert.Equal("blue", ToneAnalyzer.ForNumeric(0m, null));
        Assert.Equal("green", ToneAnalyzer.ForNumeric(5m, new[] { 1m }));
    }

    [Fact]
    public void ViewModel_ToState_SupportsCustomBindingName()
    {
        var vm = new CustomBindingViewModel { QueryText = "search term" };
        var state = vm.ToState();

        Assert.Equal("search term", state["q"]);
        Assert.Equal("search term", state["QueryText"]);

        var response = vm.ToResponse();
        Assert.Equal("search term", response.State["q"]);
        Assert.True(response.Bindings.ContainsKey("q"));
        Assert.Equal("TwoWay", response.Bindings["q"].Direction);
    }
}

class CustomBindingViewModel : ViewModel
{
    [Bind(BindDirection.TwoWay, Name = "q")]
    public string QueryText { get; set; } = "";
}

class TestViewModel : ViewModel
{
    [Bind(BindDirection.TwoWay)]
    public string Greeting { get; set; } = "";

    [Command]
    public void Refresh()
    {
        Greeting = "refreshed";
    }

    public override Task LoadAsync()
    {
        Greeting = "hello";
        return Task.CompletedTask;
    }
}
