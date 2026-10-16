using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using CodeSpirit.Core.Mvvm;

namespace CodeSpirit.Core;

public abstract class ViewModel
{
    private static readonly ConcurrentDictionary<Type, VmMetadata> _metaCache = new();

    public sealed class VmMetadata
    {
        public List<PropertyInfo> BindProps { get; } = new();
        public List<(PropertyInfo Property, BindAttribute Attribute)> BindWithMeta { get; } = new();
        public List<(PropertyInfo Property, FromQueryAttribute Attribute)> FromQueryProps { get; } = new();
        public List<(PropertyInfo Property, FromRouteAttribute Attribute)> FromRouteProps { get; } = new();
        public List<(MethodInfo Method, CommandAttribute Attribute)> Commands { get; } = new();
    }

    public static VmMetadata GetMetadata(Type vmType) =>
        _metaCache.GetOrAdd(vmType, key =>
        {
            var meta = new VmMetadata();
            foreach (var prop in key.GetProperties())
            {
                if (prop.GetCustomAttribute<BindAttribute>() is { } ba)
                {
                    meta.BindProps.Add(prop);
                    meta.BindWithMeta.Add((prop, ba));
                }
                if (prop.GetCustomAttribute<FromQueryAttribute>() is { } fq)
                    meta.FromQueryProps.Add((prop, fq));
                if (prop.GetCustomAttribute<FromRouteAttribute>() is { } fr)
                    meta.FromRouteProps.Add((prop, fr));
            }
            foreach (var method in key.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.GetCustomAttribute<CommandAttribute>() is { } cmd)
                    meta.Commands.Add((method, cmd));
            }
            return meta;
        });

    public static string GetRoute(Type viewModelType)
    {
        var attr = viewModelType.GetCustomAttribute<Page.PageDirectiveAttribute>();
        if (!string.IsNullOrWhiteSpace(attr?.Route))
            return attr.Route;

        var name = viewModelType.Name;
        if (name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase))
            name = name[..^"ViewModel".Length];

        return "/" + string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }

    /// <summary>
    /// Validation errors keyed by field name.
    /// Use <see cref="AddModelError"/> to populate.
    /// </summary>
    public Dictionary<string, List<string>> ModelState { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when no validation errors have been recorded.
    /// </summary>
    public bool IsValid => ModelState.Count == 0;

    /// <summary>
    /// Adds a validation error for the specified field.
    /// </summary>
    public void AddModelError(string field, string message)
    {
        if (!ModelState.TryGetValue(field, out var messages))
            ModelState[field] = messages = new List<string>();
        messages.Add(message);
    }

    /// <summary>
    /// HTTP context for the current request.
    /// </summary>
    public HttpContext Http => Ctx.HttpContext;

    /// <summary>
    /// Services available for dependency injection.
    /// </summary>
    public IServiceProvider Services => Ctx.Services;

    /// <summary>
    /// Route parameters from the URL.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Route => Ctx.Route;

    /// <summary>
    /// POST body payload (JSON deserialized).
    /// </summary>
    public IReadOnlyDictionary<string, object?> Payload => Ctx.Payload;

    // Internal context set by the executor
    public ViewModelContext Ctx { get; set; } = null!;

    /// <summary>
    /// Called once when the page first loads.
    /// Override to perform initialization.
    /// </summary>
    public virtual Task InitAsync() => Task.CompletedTask;

    /// <summary>
    /// Called on every request (GET/POST).
    /// Override to load data.
    /// </summary>
    public virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>
    /// Called after LoadAsync, before rendering.
    /// Override for final processing.
    /// </summary>
    public virtual Task RenderAsync() => Task.CompletedTask;

    /// <summary>
    /// Returns all [Bind] properties as a dictionary for JSON serialization.
    /// </summary>
    public Dictionary<string, object?> ToState()
    {
        var state = new Dictionary<string, object?>();
        var meta = GetMetadata(GetType());
        foreach (var (prop, attr) in meta.BindWithMeta)
        {
            var val = prop.GetValue(this);
            var name = attr.Name ?? prop.Name;
            state[name] = val;
            if (!string.Equals(name, prop.Name, StringComparison.OrdinalIgnoreCase))
            {
                state[prop.Name] = val;
            }
        }
        return state;
    }

    public ViewModelResponse ToResponse()
    {
        var meta = GetMetadata(GetType());
        var bindings = new Dictionary<string, BindingDescriptor>();

        foreach (var (prop, attr) in meta.BindWithMeta)
        {
            var name = attr.Name ?? prop.Name;
            bindings[name] = new BindingDescriptor(name, prop.Name, attr.Direction.ToString());
        }

        var commands = meta.Commands
            .Select(x => x.Attribute.Name ?? x.Method.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<string, string?>? errors = null;
        if (ModelState.Count > 0)
        {
            errors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => (string?)string.Join("; ", kvp.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        return new ViewModelResponse(ToState(), bindings, commands) { Errors = errors };
    }
}

/// <summary>
/// Context passed to ViewModel with request information.
/// </summary>
public record ViewModelContext(
    HttpContext HttpContext,
    IServiceProvider Services,
    Dictionary<string, object?> Route,
    Dictionary<string, object?> Payload);

public record BindingDescriptor(string Name, string Property, string Direction);

public record ViewModelResponse(
    Dictionary<string, object?> State,
    Dictionary<string, BindingDescriptor> Bindings,
    string[] Commands)
{
    public Dictionary<string, string> Regions { get; init; } = [];

    public Dictionary<string, string?>? Errors { get; init; }
}
