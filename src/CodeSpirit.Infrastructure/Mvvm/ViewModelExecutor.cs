using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using CodeSpirit.Core;
using CodeSpirit.Core.Mvvm;
using CodeSpirit.Infrastructure.Page;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeSpirit.Infrastructure.Mvvm;

/// <summary>
/// Executes ViewModels and handles the request lifecycle.
/// </summary>
public class ViewModelExecutor
{
    private readonly ILogger<ViewModelExecutor> _logger;
    private readonly PageRenderer _pageRenderer;

    public ViewModelExecutor(ILogger<ViewModelExecutor> logger, PageRenderer pageRenderer)
    {
        _logger = logger;
        _pageRenderer = pageRenderer;
    }

    public async Task ExecuteAsync(HttpContext ctx, Type vmType)
    {
        var services = ctx.RequestServices;
        ViewModel vm;

        try { vm = (ViewModel)services.GetRequiredService(vmType); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot resolve {Vm}", vmType.Name);
            await WriteError(ctx, 500, $"ViewModel '{vmType.Name}' not found");
            return;
        }

        try
        {
            BindQueryParams(vm, ctx.Request.Query);
            BindRouteParams(vm, ctx.GetRouteData().Values);

            var payload = await ReadPayloadAsync(ctx.Request);
            vm.Ctx = new ViewModelContext(ctx, services, ToDictionary(ctx.GetRouteData().Values), payload);

            await vm.InitAsync();
            await vm.LoadAsync();

            BindPostedProperties(vm, payload);

            if (HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method) || HttpMethods.IsPatch(ctx.Request.Method))
            {
                ValidateModel(vm, services);
                if (!vm.IsValid)
                {
                    var invalidState = vm.ToResponse();
                    await WriteJson(ctx, invalidState);
                    return;
                }
            }

            await ExecuteCommandAsync(vm, GetCommandName(ctx, payload));

            await vm.RenderAsync();

            var state = vm.ToResponse();
            if (HttpMethods.IsGet(ctx.Request.Method))
            {
                await _pageRenderer.RenderAsync(ctx, vmType, state.State);
                return;
            }

            if (!vm.IsValid)
            {
                await WriteJson(ctx, state);
                return;
            }

            state = state with { Regions = _pageRenderer.RenderRegions(ctx, vmType, state.State) };
            await WriteJson(ctx, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VM {Name} failed", vmType.Name);
            await WriteError(ctx, 500, ex.Message);
        }
    }

    private static void ValidateModel(ViewModel vm, IServiceProvider services)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(vm, services, null);

        if (Validator.TryValidateObject(vm, context, results, validateAllProperties: true))
            return;

        foreach (var result in results)
        {
            var memberNames = result.MemberNames.Any()
                ? result.MemberNames
                : [string.Empty];

            foreach (var member in memberNames)
                vm.AddModelError(member, result.ErrorMessage ?? "Invalid value.");
        }
    }

    private void BindQueryParams(ViewModel vm, IQueryCollection query)
    {
        foreach (var (prop, attr) in ViewModel.GetMetadata(vm.GetType()).FromQueryProps)
        {
            var name = attr.Name ?? prop.Name;
            if (query.TryGetValue(name, out var value) && value.Count > 0)
            {
                var strValue = value[0];
                if (strValue is not null)
                    prop.SetValue(vm, ValueConverter.ConvertValue(strValue, prop.PropertyType));
            }
        }
    }

    private void BindRouteParams(ViewModel vm, RouteValueDictionary route)
    {
        foreach (var (prop, attr) in ViewModel.GetMetadata(vm.GetType()).FromRouteProps)
        {
            var name = attr.Name ?? prop.Name;
            if (route.TryGetValue(name, out var value) && value is not null)
            {
                var strValue = value.ToString();
                if (strValue is not null)
                    prop.SetValue(vm, ValueConverter.ConvertValue(strValue, prop.PropertyType));
            }
        }
    }

    private static void BindPostedProperties(ViewModel vm, Dictionary<string, object?> payload)
    {
        if (payload.Count == 0)
            return;

        foreach (var (prop, attr) in ViewModel.GetMetadata(vm.GetType()).BindWithMeta)
        {
            if (!prop.CanWrite || attr.Direction == BindDirection.OneWay)
                continue;

            var name = attr.Name ?? prop.Name;
            if (payload.TryGetValue(name, out var value))
                prop.SetValue(vm, ValueConverter.ConvertValue(value, prop.PropertyType));
        }
    }

    private async Task ExecuteCommandAsync(ViewModel vm, string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            return;

        var command = ViewModel.GetMetadata(vm.GetType()).Commands
            .FirstOrDefault(x => string.Equals(x.Attribute.Name ?? x.Method.Name, commandName, StringComparison.OrdinalIgnoreCase));

        if (command == default)
            throw new InvalidOperationException($"Command '{commandName}' was not found on ViewModel '{vm.GetType().Name}'. Add [Command] to a parameterless method, or set data-cs-command to the command name.");

        if (command.Method.GetParameters().Length > 0)
            throw new InvalidOperationException($"Command '{commandName}' must not declare parameters. Bind request data to properties, then read them inside the ViewModel method.");

        var result = command.Method.Invoke(vm, null);
        if (result is Task task)
            await task;
        else if (result is ValueTask valueTask)
            await valueTask;
    }

    private static string? GetCommandName(HttpContext ctx, Dictionary<string, object?> payload)
    {
        if (payload.TryGetValue(CodeSpiritDefaults.CommandParamKey, out var command) || payload.TryGetValue(CodeSpiritDefaults.CommandAltParamKey, out command))
            return command?.ToString();

        if (ctx.Request.Query.TryGetValue(CodeSpiritDefaults.CommandParamKey, out var queryCommand) ||
            ctx.Request.Query.TryGetValue(CodeSpiritDefaults.CommandAltParamKey, out queryCommand))
            return queryCommand.FirstOrDefault();

        return null;
    }

    private static async Task<Dictionary<string, object?>> ReadPayloadAsync(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method) && !HttpMethods.IsPut(request.Method) && !HttpMethods.IsPatch(request.Method))
            return [];

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            return form.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value.FirstOrDefault());
        }

        if (request.ContentLength == 0)
            return [];

        var payload = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(request.Body, CodeSpiritDefaults.JsonOptions);
        if (payload is null)
            return [];

        return payload.ToDictionary(kvp => kvp.Key, kvp => ConvertJsonElement(kvp.Value));
    }

    private static Dictionary<string, object?> ToDictionary(RouteValueDictionary route)
    {
        return route.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static async Task WriteJson(HttpContext ctx, object data)
    {
        ctx.Response.ContentType = CodeSpiritDefaults.ContentTypeJsonUtf8;
        await JsonSerializer.SerializeAsync(ctx.Response.Body, data, CodeSpiritDefaults.JsonOptions);
    }

    private static async Task WriteError(HttpContext ctx, int code, string msg)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = CodeSpiritDefaults.ContentTypeJsonUtf8;
        await JsonSerializer.SerializeAsync(ctx.Response.Body, new { error = msg });
    }
}
