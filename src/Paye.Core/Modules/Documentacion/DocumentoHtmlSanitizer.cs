using System.Text;
using System.Text.RegularExpressions;

namespace Paye.Core.Modules.Documentacion;

/// <summary>
/// Sanitizador HTML de lista blanca en servidor usando APIs nativas de .NET 10
/// (100% compatible con Windows 11 Smart App Control / WDAC y con la regla 4 de AGENTS.md).
/// En el cliente se complementa con DOMPurify al pegar y al renderizar.
/// Prohibido script, iframe, object, style, link, eventos on* y URLs externas.
/// </summary>
public static partial class DocumentoHtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "strong", "b", "em", "i", "u", "s",
        "ul", "ol", "li",
        "h1", "h2", "h3", "h4",
        "blockquote", "span", "div",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td",
        "hr", "sub", "sup", "img"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "hr", "img"
    };

    [GeneratedRegex(@"<\s*(script|iframe|object|embed|style|link|meta|form|input|button|textarea|select|svg|math|applet|base|head|frame|frameset)\b[^>]*>[\s\S]*?<\s*/\s*\1\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousPairedTagsRegex();

    [GeneratedRegex(@"<\s*/?\s*(script|iframe|object|embed|style|link|meta|form|input|button|textarea|select|svg|math|applet|base|head|frame|frameset)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousSingleTagsRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentsRegex();

    [GeneratedRegex(@"<\s*(/?)\s*([a-zA-Z0-9]+)((?:\s+[^>]*?)?)\s*(/?)\s*>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"([a-zA-Z0-9_\-:]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'=<>`]+))", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlAttributeRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\s]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeClassNameRegex();

    [GeneratedRegex(@"^[1-9][0-9]?$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSpanCountRegex();

    [GeneratedRegex(@"^/images/[a-zA-Z0-9_\-/]+\.(png|jpg|jpeg|webp|svg)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafeLocalImagePathRegex();

    [GeneratedRegex(@"^data:image/(png|jpeg|jpg|webp);base64,[A-Za-z0-9+/=\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafeDataUriImageRegex();

    public static bool IsValidLogoOrImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var trimmed = url.Trim();

        if (trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.Contains("//", StringComparison.Ordinal) ||
            trimmed.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        if (SafeLocalImagePathRegex().IsMatch(trimmed))
        {
            return true;
        }

        if (trimmed.Length <= 500_000 && SafeDataUriImageRegex().IsMatch(trimmed))
        {
            return true;
        }

        return false;
    }

    public static string SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var withoutDangerous = DangerousPairedTagsRegex().Replace(text, string.Empty);
        withoutDangerous = DangerousSingleTagsRegex().Replace(withoutDangerous, string.Empty);
        withoutDangerous = HtmlTagRegex().Replace(withoutDangerous, string.Empty);
        return withoutDangerous.Trim();
    }

    public static string SanitizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var cleaned = HtmlCommentsRegex().Replace(html, string.Empty);
        cleaned = DangerousPairedTagsRegex().Replace(cleaned, string.Empty);
        cleaned = DangerousSingleTagsRegex().Replace(cleaned, string.Empty);

        cleaned = HtmlTagRegex().Replace(cleaned, match =>
        {
            var isClosing = match.Groups[1].Value == "/";
            var tagName = match.Groups[2].Value.ToLowerInvariant();
            var rawAttrs = match.Groups[3].Value;

            if (!AllowedTags.Contains(tagName))
            {
                return string.Empty;
            }

            if (isClosing)
            {
                return VoidTags.Contains(tagName) ? string.Empty : $"</{tagName}>";
            }

            var sanitizedAttrs = SanitizeAttributes(tagName, rawAttrs, out var rejectEntireTag);
            if (rejectEntireTag)
            {
                return string.Empty;
            }

            return VoidTags.Contains(tagName)
                ? $"<{tagName}{sanitizedAttrs} />"
                : $"<{tagName}{sanitizedAttrs}>";
        });

        return cleaned.Trim();
    }

    private static string SanitizeAttributes(string tagName, string rawAttrs, out bool rejectEntireTag)
    {
        rejectEntireTag = false;
        if (string.IsNullOrWhiteSpace(rawAttrs))
        {
            if (string.Equals(tagName, "img", StringComparison.Ordinal))
            {
                rejectEntireTag = true;
            }
            return string.Empty;
        }

        var sb = new StringBuilder();
        var hasValidImgSrc = false;

        foreach (Match attrMatch in HtmlAttributeRegex().Matches(rawAttrs))
        {
            var attrName = attrMatch.Groups[1].Value.ToLowerInvariant();
            var attrValue = attrMatch.Groups[2].Success ? attrMatch.Groups[2].Value
                : attrMatch.Groups[3].Success ? attrMatch.Groups[3].Value
                : attrMatch.Groups[4].Value;

            if (attrName.StartsWith("on", StringComparison.Ordinal) ||
                string.Equals(attrName, "style", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(attrName, "class", StringComparison.Ordinal))
            {
                var trimmedClass = attrValue.Trim();
                if (SafeClassNameRegex().IsMatch(trimmedClass))
                {
                    sb.Append(" class=\"").Append(trimmedClass).Append('"');
                }
                continue;
            }

            if ((string.Equals(tagName, "td", StringComparison.Ordinal) ||
                 string.Equals(tagName, "th", StringComparison.Ordinal)) &&
                (string.Equals(attrName, "colspan", StringComparison.Ordinal) ||
                 string.Equals(attrName, "rowspan", StringComparison.Ordinal)))
            {
                var trimmedSpan = attrValue.Trim();
                if (SafeSpanCountRegex().IsMatch(trimmedSpan))
                {
                    sb.Append(' ').Append(attrName).Append("=\"").Append(trimmedSpan).Append('"');
                }
                continue;
            }

            if (string.Equals(tagName, "img", StringComparison.Ordinal))
            {
                if (string.Equals(attrName, "src", StringComparison.Ordinal))
                {
                    var trimmedSrc = attrValue.Trim();
                    if (!string.IsNullOrEmpty(trimmedSrc) && IsValidLogoOrImageUrl(trimmedSrc))
                    {
                        sb.Append(" src=\"").Append(trimmedSrc).Append('"');
                        hasValidImgSrc = true;
                    }
                }
                else if (string.Equals(attrName, "alt", StringComparison.Ordinal))
                {
                    var safeAlt = SanitizeText(attrValue).Replace("\"", "&quot;", StringComparison.Ordinal);
                    sb.Append(" alt=\"").Append(safeAlt).Append('"');
                }
            }
        }

        if (string.Equals(tagName, "img", StringComparison.Ordinal) && !hasValidImgSrc)
        {
            rejectEntireTag = true;
            return string.Empty;
        }

        return sb.ToString();
    }
}
