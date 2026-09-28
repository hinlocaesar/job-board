using System.Security.Claims;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobBoard.Api.Common;

namespace JobBoard.Api.Common;

public static partial class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("id");

        if (Guid.TryParse(raw, out var id))
            return id;

        throw ApiException.Unauthorized("not_authenticated", "A signed-in user is required.");
    }

    public static string? GetIp(this HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}

public static partial class SlugHelper
{
    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "item";

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var slug = builder.ToString().Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Length == 0 ? "item" : slug;
    }

    public static string WithPrefix(string prefix, string slug) => $"{prefix}-{slug}";
}
