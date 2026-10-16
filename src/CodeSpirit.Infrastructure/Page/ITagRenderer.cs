namespace CodeSpirit.Infrastructure.Page;

/// <summary>
/// Extensibility interface for custom cs:* tags in CodeSpirit pages.
/// </summary>
public interface ITagRenderer
{
    /// <summary>
    /// Tag name without "cs:" prefix (e.g., "Badge", "Avatar", "RichText").
    /// </summary>
    string TagName { get; }

    /// <summary>
    /// Whether the tag is self-closing (&lt;cs:TagName ... /&gt;) or a container block (&lt;cs:TagName&gt;...&lt;/cs:TagName&gt;).
    /// </summary>
    bool IsSelfClosing { get; }

    /// <summary>
    /// Renders the custom tag into HTML.
    /// </summary>
    /// <param name="rawAttributes">Raw attribute string from the tag.</param>
    /// <param name="innerContent">Inner content if not self-closing.</param>
    /// <param name="state">Current ViewModel state dictionary.</param>
    /// <returns>Rendered HTML output.</returns>
    string Render(string rawAttributes, string innerContent, IReadOnlyDictionary<string, object?> state);
}
