using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using JobBoard.Domain.Enums;

namespace JobBoard.Api.Features.Imports;

/// <summary>
/// Maps a <see cref="RawExternalJob"/> onto our job model. Pure and deterministic so
/// it can be unit-tested without touching the network or the database.
/// </summary>
public static partial class ExternalJobMapper
{
    /// <summary>Longest description we store; feeds can send megabytes of HTML.</summary>
    public const int MaxDescriptionLength = 8_000;

    /// <summary>Fallback when the feed's category matches nothing in our taxonomy.</summary>
    public const string FallbackCategorySlug = "virtual-assistance";

    public static ImportedJobDraft Map(RawExternalJob raw, DateTime now)
    {
        var title = Clean(raw.Title, 200);
        var company = Clean(raw.Company, 160);
        var sourceUrl = string.IsNullOrWhiteSpace(raw.Url) ? null : raw.Url.Trim();
        var payMin = ResolvePayMin(raw.Salary);

        return new ImportedJobDraft(
            Source: raw.Source,
            SourceId: raw.SourceId,
            Title: title.Length == 0 ? "Untitled remote role" : title,
            Company: company.Length == 0 ? "Unknown company" : company,
            Description: BuildDescription(raw, now),
            CategorySlug: ResolveCategory(raw),
            JobType: ResolveJobType(raw.JobType, $"{raw.Title} {raw.JobType}"),
            ExperienceLevel: ResolveExperience(raw.ExperienceLevel, $"{raw.Title} {raw.ExperienceLevel}"),
            PayType: PayType.Hourly,
            PayMin: payMin,
            PayMax: ResolvePayMax(raw.Salary, payMin),
            Currency: ResolveCurrency(raw.Salary),
            Skills: ResolveSkills(raw),
            SourceUrl: sourceUrl,
            PublishedAt: raw.PublishedAt ?? now);
    }

    /// <summary>
    /// Feeds return HTML; our job descriptions are plain text. Strips tags, decodes
    /// entities and appends the attribution link the feeds ask for.
    /// </summary>
    public static string BuildDescription(RawExternalJob raw, DateTime now)
    {
        var text = ToPlainText(raw.DescriptionHtml);
        if (text.Length > MaxDescriptionLength)
        {
            text = text[..MaxDescriptionLength].TrimEnd() + "…";
        }

        var attribution = new StringBuilder();
        attribution.Append('\n').Append('\n');
        attribution.Append(CultureInfo.InvariantCulture, $"Published {raw.PublishedAt?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? now.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(raw.Company))
        {
            attribution.Append(CultureInfo.InvariantCulture, $" · {raw.Company}");
        }

        attribution.Append(CultureInfo.InvariantCulture, $" · via {raw.Source}");
        if (!string.IsNullOrWhiteSpace(raw.Url))
        {
            attribution.Append("\nOriginal posting: ").Append(raw.Url);
        }

        return (text + attribution.ToString()).Trim();
    }

    /// <summary>HTML → plain text: block elements become newlines, entities are decoded.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutScripts = ScriptOrStyle().Replace(html, " ");
        var withBreaks = BlockBreak().Replace(withoutScripts, "\n");
        var withoutTags = Tags().Replace(withBreaks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);

        return Clean(decoded, MaxDescriptionLength * 2);
    }

    // ---------- individual field resolvers (unit-tested individually) ----------

    public static JobType ResolveJobType(string? raw, string? haystack = null)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return value switch
        {
            "part_time" or "parttime" or "contractor" => JobType.PartTime,
            "contract" or "fixed_term" or "fixedterm" or "c_temporary" or "temporary" => JobType.Contract,
            "freelance" or "freelancer" => JobType.Freelance,
            "internship" or "intern" or "student" => JobType.Internship,
            "full_time" or "fulltime" or "permanent" or "employee" => JobType.FullTime,
            _ => InferJobTypeFromText(haystack),
        };
    }

    public static ExperienceLevel ResolveExperience(string? raw, string? haystack = null)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("lead") || value.Contains("head") || value.Contains("director") || value.Contains("principal"))
            return ExperienceLevel.Lead;
        if (value.Contains("senior") || value.Contains("sr") || value.Contains("manager") || value.Contains("staff"))
            return ExperienceLevel.Senior;
        if (value.Contains("junior") || value.Contains("jr") || value.Contains("entry") || value.Contains("intern") || value.Contains("graduate"))
            return ExperienceLevel.Junior;
        if (value.Contains("mid") || value.Contains("intermediate") || value.Contains("regular"))
            return ExperienceLevel.Mid;

        return InferExperienceFromText(haystack);
    }

    /// <summary>Maps a feed category/industry onto one of our category slugs.</summary>
    public static string ResolveCategory(RawExternalJob raw)
    {
        var haystack = $"{raw.Category} {string.Join(' ', raw.Tags)}".ToLowerInvariant();
        if (haystack.Length == 0)
            return FallbackCategorySlug;

        foreach (var (slug, keywords) in CategoryKeywords)
        {
            if (keywords.Any(keyword => haystack.Contains(keyword, StringComparison.Ordinal)))
                return slug;
        }

        return FallbackCategorySlug;
    }

    /// <summary>Best-effort salary parsing; feeds mostly leave this empty (=> "Negotiable").</summary>
    public static decimal ResolvePayMin(string? salary)
    {
        var match = SalaryNumber().Match(salary ?? string.Empty);
        return match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    /// <summary>
    /// Upper bound of a salary range. A single number (or the resolved minimum) is
    /// repeated so the UI can render "from $X" without special-casing.
    /// </summary>
    public static decimal ResolvePayMax(string? salary, decimal payMin)
    {
        if (string.IsNullOrWhiteSpace(salary))
            return payMin;

        var numbers = SalaryNumber().Matches(salary).Select(m => m.Value)
            .Select(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m)
            .Where(v => v > 0)
            .ToList();

        if (numbers.Count < 2)
            return payMin;

        return numbers.Max();
    }

    public static string ResolveCurrency(string? salary)
    {
        if (string.IsNullOrWhiteSpace(salary))
            return "USD";

        if (salary.Contains('$')) return "USD";
        if (salary.Contains('€')) return "EUR";
        if (salary.Contains('£')) return "GBP";
        if (salary.Contains("AUD", StringComparison.OrdinalIgnoreCase)) return "AUD";
        if (salary.Contains("CAD", StringComparison.OrdinalIgnoreCase)) return "CAD";
        if (salary.Contains("PHP", StringComparison.OrdinalIgnoreCase) || salary.Contains("₱")) return "PHP";
        if (salary.Contains("INR", StringComparison.OrdinalIgnoreCase) || salary.Contains("₹")) return "INR";
        return "USD";
    }

    /// <summary>Feed tags, de-duplicated, trimmed and capped for the skill chips.</summary>
    public static IReadOnlyList<string> ResolveSkills(RawExternalJob raw)
    {
        var skills = new List<string>();
        foreach (var tag in raw.Tags)
        {
            var cleaned = Clean(tag, 40);
            if (cleaned.Length == 0)
                continue;

            // Feeds use commas/semicolans inside a single "tags" string.
            foreach (var part in cleaned.Split([',', ';', '|', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var name = Clean(part, 40);
                if (name.Length >= 2 && !skills.Contains(name, StringComparer.OrdinalIgnoreCase))
                    skills.Add(name);
            }
        }

        return skills.Take(8).ToList();
    }

    // ---------- internals ----------

    private static readonly (string Slug, string[] Keywords)[] CategoryKeywords =
    [
        ("web-development", ["software", "developer", "engineer", "web", "frontend", "backend", "full stack", "fullstack", "devops", "cloud", "qa", "test", "api", "mobile", "ios", "android", "react", "python", "java", "javascript", "typescript", "golang", "rust", "php", "ruby", "wordpress", "magento", "shopify", "erp", "crm", "saas", "programming", "code", "linux", "security", "dev"]),
        ("graphic-design", ["design", "ux", "ui", "graphic", "illustrat", "brand", "logo", "figma", "visual", "creative", "art"]),
        ("writing-content", ["writer", "writing", "content", "copywriter", "editor", "editorial", "journalis", "proofread", "translation", "localization", "seo writer"]),
        ("virtual-assistance", ["virtual assistant", "executive assistant", "personal assistant", "admin", "recruit", "hr", "human resources", "operations", "operations manager", "project manager", "accountant", "bookkeep", "finance", "legal", "assistant"]),
        ("customer-support", ["customer", "support", "success", "helpdesk", "help desk", "service desk", "technical support", "chat"]),
        ("digital-marketing", ["marketing", "seo", "sem", "ppc", "ads", "advertis", "growth", "social media", "email marketing", "crm marketing", "affiliate"]),
        ("video-animation", ["video", "animation", "motion", "editor", "editing", "vfx", "3d", "youtube", "audio", "podcast"]),
        ("data-analytics", ["data", "analyst", "analytics", "data analyst", "scientist", "machine learning", "ai", "ml", "business intelligence", "bi ", "statistics", "research"]),
        ("accounting-finance", ["accounting", "accountant", "bookkeep", "finance", "financial", "tax", "audit", "payroll", "controller", "fp&a"]),
    ];

    private static JobType InferJobTypeFromText(string? text)
    {
        var value = (text ?? string.Empty).ToLowerInvariant();
        if (value.Contains("intern")) return JobType.Internship;
        if (value.Contains("contract") || value.Contains("contractor")) return JobType.Contract;
        if (value.Contains("freelance")) return JobType.Freelance;
        if (value.Contains("part time") || value.Contains("part-time")) return JobType.PartTime;
        return JobType.FullTime;
    }

    private static ExperienceLevel InferExperienceFromText(string? text)
    {
        var value = (text ?? string.Empty).ToLowerInvariant();
        if (value.Contains("principal") || value.Contains("director") || value.Contains(" head of ")) return ExperienceLevel.Lead;
        if (value.Contains("senior") || value.Contains(" sr ") || value.Contains("staff")) return ExperienceLevel.Senior;
        if (value.Contains("junior") || value.Contains(" jr ") || value.Contains("entry level") || value.Contains("graduate"))
            return ExperienceLevel.Junior;
        return ExperienceLevel.Mid;
    }

    /// <summary>Collapses whitespace and trims to a maximum length.</summary>
    private static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = Whitespace().Replace(WebUtility.HtmlDecode(value), " ").Trim();
        return cleaned.Length > maxLength ? cleaned[..maxLength].TrimEnd() : cleaned;
    }

    [GeneratedRegex(@"(?is)<(script|style)[^>]*>.*?</\1>")]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"(?i)</?(p|div|br|li|ul|ol|h[1-6]|tr|section|article|blockquote)\b[^>]*>")]
    private static partial Regex BlockBreak();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex SalaryNumber();
}
