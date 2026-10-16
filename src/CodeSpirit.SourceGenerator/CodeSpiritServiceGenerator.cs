using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace CodeSpirit.SourceGenerator;

[Generator]
public class CodeSpiritServiceGenerator : IIncrementalGenerator
{
    private const string ServiceAttributeName = "CodeSpirit.Core.Attributes.ServiceAttribute";
    private const string RepositoryAttributeName = "CodeSpirit.Core.Attributes.RepositoryAttribute";
    private const string CommandAttributeName = "CodeSpirit.Core.Mvvm.CommandAttribute";
    private const string TransactionalAttributeName = "CodeSpirit.Core.Attributes.TransactionalAttribute";
    private const string CacheableAttributeName = "CodeSpirit.Core.Attributes.CacheableAttribute";
    private const string AutowiredAttributeName = "CodeSpirit.Core.Attributes.AutowiredAttribute";
    private const string ValueAttributeName = "CodeSpirit.Core.Attributes.ValueAttribute";
    private const string BindAttributeName = "CodeSpirit.Core.Mvvm.BindAttribute";

    private static readonly DiagnosticDescriptor AbstractServiceWarning = new(
        "CSP001",
        "Abstract class with [Service] is ignored",
        "Class '{0}' is abstract and will not be registered. Remove [Service] or make the class non-abstract.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoPublicConstructorWarning = new(
        "CSP002",
        "[Service] class missing public constructor",
        "Class '{0}' has no accessible public constructor. CodeSpirit may fail to activate this service.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CommandHasParametersError = new(
        "CSP003",
        "[Command] method must not have parameters",
        "Command '{0}' declares parameters. Command methods must be parameterless.",
        "CodeSpirit",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonVirtualAopWarning = new(
        "CSP004",
        "[Transactional] or [Cacheable] on non-virtual method has no effect",
        "Method '{0}' with [{1}] is not virtual. AOP class proxies can only intercept virtual/override methods. Add 'virtual' keyword.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonWritablePropertyWarning = new(
        "CSP005",
        "[Autowired] or [Value] on non-writable property has no effect",
        "Property '{0}' with [{1}] has no writable setter. Injection requires a writable property. Add a 'set;' accessor.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonPublicBoundPropertyWarning = new(
        "CSP006",
        "[Bind] on non-public property",
        "Property '{0}' with [Bind] is not public. MVVM binding works on public properties only.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonPublicCommandWarning = new(
        "CSP007",
        "[Command] on non-public or static method is unreachable",
        "Method '{0}' with [Command] is {1}. Command methods must be public instance methods.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AopOnNonServiceWarning = new(
        "CSP008",
        "[Transactional] or [Cacheable] on non-[Service] class has no effect",
        "Method '{0}' with [{1}] is in class '{2}' which is not marked with [Service]. AOP proxy interception only works on [Service]-registered classes. Add [Service] to the class.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateCommandNameWarning = new(
        "CSP009",
        "Duplicate command name in ViewModel",
        "Command name '{0}' is already defined in ViewModel '{1}'. Command names must be unique within a ViewModel.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateBindingNameWarning = new(
        "CSP010",
        "Duplicate binding name in ViewModel",
        "Binding name '{0}' is already defined in ViewModel '{1}'. Binding names must be unique within a ViewModel.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CommandNameReservedWarning = new(
        "CSP011",
        "Command name uses reserved prefix",
        "Command name '{0}' uses reserved prefix '__'. Avoid using double underscore prefix for command names.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncVoidCommandWarning = new(
        "CSP012",
        "Async void command method",
        "Command method '{0}' returns void but uses async operations. Consider returning Task instead.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingPageDirectiveWarning = new(
        "CSP013",
        "ViewModel missing [PageDirective]",
        "ViewModel '{0}' does not have [PageDirective] attribute. It will not be exposed as a page endpoint.",
        "CodeSpirit",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidCommandReturnTypeWarning = new(
        "CSP014",
        "Invalid command return type",
        "Command method '{0}' returns '{1}'. Command methods should return void, Task, or ValueTask.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PageDirectiveWithoutViewModelWarning = new(
        "CSP015",
        "[PageDirective] class does not inherit ViewModel",
        "Class '{0}' has [PageDirective] but does not inherit from ViewModel. Page routing requires the ViewModel base class.",
        "CodeSpirit",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetServiceRegistrationInfo(ctx))
            .Where(static info => info is not null);

        var combined = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(combined, GenerateServiceRegistration);

        var methods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0 && m.ParameterList.Parameters.Count > 0,
                transform: static (ctx, _) => GetCommandDiagnosticInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(methods.Collect(), GenerateCommandDiagnostics);

        var aopMethods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetAopDiagnosticInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(aopMethods.Collect(), GenerateAopDiagnostics);

        var injectionProperties = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is PropertyDeclarationSyntax p && p.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetPropertyInjectionDiagnosticInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(injectionProperties.Collect(), GeneratePropertyInjectionDiagnostics);

        var bindProperties = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is PropertyDeclarationSyntax p && p.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetBindAccessibilityInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(bindProperties.Collect(), GenerateBindAccessibilityDiagnostics);

        var commandMethods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetCommandAccessibilityInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(commandMethods.Collect(), GenerateCommandAccessibilityDiagnostics);

        var asyncVoidCommands = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0 && m.Modifiers.Any(mod => mod.ValueText == "async"),
                transform: static (ctx, _) => GetAsyncVoidCommandInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(asyncVoidCommands.Collect(), GenerateAsyncVoidCommandDiagnostics);

        var commandReturnTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetCommandReturnTypeInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(commandReturnTypes.Collect(), GenerateCommandReturnTypeDiagnostics);

        // Additional diagnostics for ViewModel validation
        var viewModelClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.BaseList?.Types.Count > 0,
                transform: static (ctx, _) => GetViewModelValidationInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(viewModelClasses.Collect(), GenerateViewModelValidationDiagnostics);

        var pageDirectiveClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetPageDirectiveValidationInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(pageDirectiveClasses.Collect(), GeneratePageDirectiveDiagnostics);
    }

    private static ServiceRegistrationInfo? GetServiceRegistrationInfo(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var model = ctx.SemanticModel;
        var symbol = model.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol typeSymbol)
            return null;

        var serviceLifetime = ServiceLifetimeKind.Scoped;
        var hasService = false;
        string? interfaceName = null;
        var hasRepository = false;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;

            var fullName = attrClass.ToDisplayString();

            if (fullName == ServiceAttributeName)
            {
                hasService = true;
                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "Lifetime" && named.Value.Value is int lifetimeVal)
                    {
                        serviceLifetime = (ServiceLifetimeKind)lifetimeVal;
                    }
                    else if (named.Key == "ServiceType" && named.Value.Value is INamedTypeSymbol st)
                    {
                        interfaceName = st.ToDisplayString();
                    }
                }
            }
            else if (fullName == RepositoryAttributeName)
            {
                hasRepository = true;
            }
        }

        if (!hasService && !hasRepository)
            return null;

        return new ServiceRegistrationInfo
        {
            ClassName = typeSymbol.ToDisplayString(),
            InterfaceName = interfaceName
                ?? typeSymbol.AllInterfaces
                    .FirstOrDefault(i => !i.ToDisplayString().Contains("ICodeSpiritModule"))
                    ?.ToDisplayString()
                ?? typeSymbol.ToDisplayString(),
            Lifetime = serviceLifetime,
            IsRepository = hasRepository,
            IsAbstract = typeSymbol.IsAbstract,
            HasPublicConstructor = typeSymbol.Constructors.Any(c => c.DeclaredAccessibility == Accessibility.Public),
            Location = typeSymbol.Locations.FirstOrDefault()
        };
    }

    private static CommandDiagnosticInfo? GetCommandDiagnosticInfo(GeneratorSyntaxContext ctx)
    {
        var methodDecl = (MethodDeclarationSyntax)ctx.Node;
        var model = ctx.SemanticModel;
        var symbol = model.GetDeclaredSymbol(methodDecl);

        if (symbol is not IMethodSymbol methodSymbol || methodSymbol.Parameters.Length == 0)
            return null;

        var hasCommand = methodSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == CommandAttributeName);

        if (!hasCommand)
            return null;

        return new CommandDiagnosticInfo
        {
            MethodName = methodSymbol.Name,
            Location = methodSymbol.Locations.FirstOrDefault()
        };
    }

    private static AopDiagnosticInfo? GetAopDiagnosticInfo(GeneratorSyntaxContext ctx)
    {
        var methodDecl = (MethodDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(methodDecl);

        if (symbol is not IMethodSymbol methodSymbol)
            return null;

        string? aopAttrName = null;
        foreach (var attr in methodSymbol.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (fullName == TransactionalAttributeName)
                aopAttrName = "Transactional";
            else if (fullName == CacheableAttributeName)
                aopAttrName = "Cacheable";

            if (aopAttrName != null)
                break;
        }

        if (aopAttrName == null)
            return null;

        var containingType = methodSymbol.ContainingType;
        var isInServiceClass = false;
        if (containingType is not null)
        {
            foreach (var attr in containingType.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == ServiceAttributeName)
                {
                    isInServiceClass = true;
                    break;
                }
            }
        }

        return new AopDiagnosticInfo
        {
            MethodName = methodSymbol.Name,
            AttributeName = aopAttrName,
            ContainingClassName = containingType?.Name ?? "?",
            IsInServiceClass = isInServiceClass,
            IsVirtual = methodSymbol.IsVirtual,
            IsOverride = methodSymbol.IsOverride,
            IsAbstract = methodSymbol.IsAbstract,
            Location = methodSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GenerateServiceRegistration(
        SourceProductionContext context,
        (Compilation Left, ImmutableArray<ServiceRegistrationInfo?> Right) source)
    {
        var allInfos = new List<ServiceRegistrationInfo>();
        var seen = new HashSet<string>();

        foreach (var info in source.Right)
        {
            if (info is null || !seen.Add(info.ClassName))
                continue;

            if (info.IsAbstract)
            {
                if (info.Location is not null)
                    context.ReportDiagnostic(Diagnostic.Create(AbstractServiceWarning, info.Location, info.ClassName));
                continue;
            }

            if (!info.HasPublicConstructor && info.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(NoPublicConstructorWarning, info.Location, info.ClassName));

            allInfos.Add(info);
        }

        if (allInfos.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.Configuration;");
        sb.AppendLine("using CodeSpirit.Core.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("namespace CodeSpirit.AutoGenerated");
        sb.AppendLine("{");
        sb.AppendLine("    public sealed class CodeSpiritGeneratedRegistrar : ICodeSpiritGeneratedRegistrar");
        sb.AppendLine("    {");
        sb.AppendLine("        public void RegisterServices(IServiceCollection services, IConfiguration configuration)");
        sb.AppendLine("        {");

        foreach (var reg in allInfos)
        {
            var lifetimeStr = reg.IsRepository ? "Scoped" : reg.Lifetime switch
            {
                ServiceLifetimeKind.Singleton => "Singleton",
                ServiceLifetimeKind.Scoped => "Scoped",
                ServiceLifetimeKind.Transient => "Transient",
                _ => "Scoped"
            };

            if (reg.InterfaceName == reg.ClassName)
            {
                sb.AppendLine($"            services.Add{lifetimeStr}<{reg.ClassName}>();");
            }
            else
            {
                sb.AppendLine($"            services.Add{lifetimeStr}<{reg.InterfaceName}, {reg.ClassName}>();");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("CodeSpiritServiceRegistration.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCommandDiagnostics(
        SourceProductionContext context,
        ImmutableArray<CommandDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(CommandHasParametersError, info.Location, info.MethodName));
        }
    }

    private static void GenerateAopDiagnostics(
        SourceProductionContext context,
        ImmutableArray<AopDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is null)
                continue;

            if (!info.IsInServiceClass)
                context.ReportDiagnostic(Diagnostic.Create(
                    AopOnNonServiceWarning, info.Location, info.MethodName, info.AttributeName, info.ContainingClassName));

            if (!info.IsVirtual && !info.IsOverride && !info.IsAbstract)
                context.ReportDiagnostic(Diagnostic.Create(
                    NonVirtualAopWarning, info.Location, info.MethodName, info.AttributeName));
        }
    }

    private static PropertyDiagnosticInfo? GetPropertyInjectionDiagnosticInfo(GeneratorSyntaxContext ctx)
    {
        var propDecl = (PropertyDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(propDecl);
        if (symbol is not IPropertySymbol propSymbol)
            return null;

        string? attrName = null;
        foreach (var attr in propSymbol.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (fullName == AutowiredAttributeName)
                attrName = "Autowired";
            else if (fullName == ValueAttributeName)
                attrName = "Value";

            if (attrName is not null) break;
        }

        if (attrName is null)
            return null;

        var setMethod = propSymbol.SetMethod;
        if (setMethod is not null && !setMethod.IsInitOnly)
            return null;

        return new PropertyDiagnosticInfo
        {
            MemberName = propSymbol.Name,
            AttributeName = attrName!,
            Location = propSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GeneratePropertyInjectionDiagnostics(
        SourceProductionContext context,
        ImmutableArray<PropertyDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(
                    NonWritablePropertyWarning, info.Location, info.MemberName, info.AttributeName));
        }
    }

    private static PropertyDiagnosticInfo? GetBindAccessibilityInfo(GeneratorSyntaxContext ctx)
    {
        var propDecl = (PropertyDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(propDecl);
        if (symbol is not IPropertySymbol propSymbol)
            return null;

        if (propSymbol.DeclaredAccessibility == Accessibility.Public)
            return null;

        var hasBind = propSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == BindAttributeName);

        if (!hasBind)
            return null;

        return new PropertyDiagnosticInfo
        {
            MemberName = propSymbol.Name,
            AttributeName = "Bind",
            Location = propSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GenerateBindAccessibilityDiagnostics(
        SourceProductionContext context,
        ImmutableArray<PropertyDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(
                    NonPublicBoundPropertyWarning, info.Location, info.MemberName));
        }
    }

    private static PropertyDiagnosticInfo? GetCommandAccessibilityInfo(GeneratorSyntaxContext ctx)
    {
        var methodDecl = (MethodDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(methodDecl);
        if (symbol is not IMethodSymbol methodSymbol)
            return null;

        var hasCommand = methodSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == CommandAttributeName);

        if (!hasCommand)
            return null;

        if (methodSymbol.DeclaredAccessibility == Accessibility.Public && !methodSymbol.IsStatic)
            return null;

        var reason = methodSymbol.IsStatic ? "static" : methodSymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

        return new PropertyDiagnosticInfo
        {
            MemberName = methodSymbol.Name,
            AttributeName = reason,
            Location = methodSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GenerateCommandAccessibilityDiagnostics(
        SourceProductionContext context,
        ImmutableArray<PropertyDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(
                    NonPublicCommandWarning, info.Location, info.MemberName, info.AttributeName));
        }
    }

    private static PropertyDiagnosticInfo? GetAsyncVoidCommandInfo(GeneratorSyntaxContext ctx)
    {
        var methodDecl = (MethodDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(methodDecl);
        if (symbol is not IMethodSymbol methodSymbol)
            return null;

        if (!methodSymbol.IsAsync || !methodSymbol.ReturnsVoid)
            return null;

        var hasCommand = methodSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == CommandAttributeName);

        if (!hasCommand)
            return null;

        return new PropertyDiagnosticInfo
        {
            MemberName = methodSymbol.Name,
            AttributeName = "async void",
            Location = methodSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GenerateAsyncVoidCommandDiagnostics(
        SourceProductionContext context,
        ImmutableArray<PropertyDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(
                    AsyncVoidCommandWarning, info.Location, info.MemberName));
        }
    }

    private static PropertyDiagnosticInfo? GetCommandReturnTypeInfo(GeneratorSyntaxContext ctx)
    {
        var methodDecl = (MethodDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(methodDecl);
        if (symbol is not IMethodSymbol methodSymbol)
            return null;

        var hasCommand = methodSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == CommandAttributeName);

        if (!hasCommand)
            return null;

        if (methodSymbol.ReturnsVoid)
            return null;

        var returnTypeStr = methodSymbol.ReturnType.ToDisplayString();
        if (returnTypeStr is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask")
            return null;

        return new PropertyDiagnosticInfo
        {
            MemberName = methodSymbol.Name,
            AttributeName = returnTypeStr,
            Location = methodSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GenerateCommandReturnTypeDiagnostics(
        SourceProductionContext context,
        ImmutableArray<PropertyDiagnosticInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is not null)
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandReturnTypeWarning, info.Location, info.MemberName, info.AttributeName));
        }
    }

    private static ViewModelValidationInfo? GetViewModelValidationInfo(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var model = ctx.SemanticModel;
        var symbol = model.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol typeSymbol)
            return null;

        // Check if it inherits from ViewModel
        if (!InheritsViewModel(typeSymbol))
            return null;

        var commands = new List<string>();
        var bindings = new List<string>();
        var hasPageDirective = false;

        // Check for PageDirective
        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "PageDirectiveAttribute")
            {
                hasPageDirective = true;
                break;
            }
        }

        // Collect commands and bindings from members
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                foreach (var attr in method.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == CommandAttributeName)
                    {
                        var customName = (attr.ConstructorArguments.FirstOrDefault().Value as string)
                            ?? (attr.NamedArguments.FirstOrDefault(na => na.Key == "Name").Value.Value as string);
                        var commandName = !string.IsNullOrWhiteSpace(customName) ? customName! : method.Name;
                        commands.Add(commandName);
                    }
                }
            }
            else if (member is IPropertySymbol prop)
            {
                foreach (var attr in prop.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == BindAttributeName)
                    {
                        var customName = attr.NamedArguments.FirstOrDefault(na => na.Key == "Name").Value.Value as string;
                        var bindName = !string.IsNullOrWhiteSpace(customName) ? customName! : prop.Name;
                        bindings.Add(bindName);
                    }
                }
            }
        }

        return new ViewModelValidationInfo
        {
            ClassName = typeSymbol.Name,
            Commands = commands,
            Bindings = bindings,
            HasPageDirective = hasPageDirective,
            Location = typeSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GenerateViewModelValidationDiagnostics(
        SourceProductionContext context,
        ImmutableArray<ViewModelValidationInfo?> infos)
    {
        var allCommands = new HashSet<string>();
        var allBindings = new HashSet<string>();

        foreach (var info in infos)
        {
            if (info?.Location is null)
                continue;

            // Check for missing PageDirective
            if (!info.HasPageDirective)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingPageDirectiveWarning, info.Location, info.ClassName));
            }

            // Check for duplicate commands
            allCommands.Clear();
            foreach (var cmd in info.Commands)
            {
                if (!allCommands.Add(cmd))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateCommandNameWarning, info.Location, cmd, info.ClassName));
                }

                // Check for reserved prefix
                if (cmd.StartsWith("__"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        CommandNameReservedWarning, info.Location, cmd));
                }
            }

            // Check for duplicate bindings
            allBindings.Clear();
            foreach (var bind in info.Bindings)
            {
                if (!allBindings.Add(bind))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateBindingNameWarning, info.Location, bind, info.ClassName));
                }
            }
        }
    }

    private static bool InheritsViewModel(INamedTypeSymbol typeSymbol)
    {
        var baseType = typeSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "ViewModel" &&
                baseType.ContainingNamespace?.ToDisplayString().Contains("CodeSpirit") == true)
            {
                return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    private static PageDirectiveValidationInfo? GetPageDirectiveValidationInfo(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol typeSymbol)
            return null;

        var hasPageDirective = typeSymbol.GetAttributes()
            .Any(static attr => attr.AttributeClass?.Name == "PageDirectiveAttribute");

        if (!hasPageDirective || InheritsViewModel(typeSymbol))
            return null;

        return new PageDirectiveValidationInfo
        {
            ClassName = typeSymbol.Name,
            Location = typeSymbol.Locations.FirstOrDefault()
        };
    }

    private static void GeneratePageDirectiveDiagnostics(
        SourceProductionContext context,
        ImmutableArray<PageDirectiveValidationInfo?> infos)
    {
        foreach (var info in infos)
        {
            if (info?.Location is null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                PageDirectiveWithoutViewModelWarning, info.Location, info.ClassName));
        }
    }
}

internal sealed class PageDirectiveValidationInfo
{
    public string ClassName { get; set; } = string.Empty;
    public Location? Location { get; set; }
}

internal sealed class ServiceRegistrationInfo
{
    public string ClassName { get; set; } = string.Empty;
    public string? InterfaceName { get; set; }
    public ServiceLifetimeKind Lifetime { get; set; } = ServiceLifetimeKind.Scoped;
    public bool IsRepository { get; set; }
    public bool IsAbstract { get; set; }
    public bool HasPublicConstructor { get; set; }
    public Location? Location { get; set; }
}

internal sealed class CommandDiagnosticInfo
{
    public string MethodName { get; set; } = string.Empty;
    public Location? Location { get; set; }
}

internal sealed class AopDiagnosticInfo
{
    public string MethodName { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public string ContainingClassName { get; set; } = string.Empty;
    public bool IsInServiceClass { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsAbstract { get; set; }
    public Location? Location { get; set; }
}

internal sealed class PropertyDiagnosticInfo
{
    public string MemberName { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public Location? Location { get; set; }
}

internal sealed class ViewModelValidationInfo
{
    public string ClassName { get; set; } = string.Empty;
    public List<string> Commands { get; set; } = new();
    public List<string> Bindings { get; set; } = new();
    public bool HasPageDirective { get; set; }
    public Location? Location { get; set; }
}

internal enum ServiceLifetimeKind
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2
}
