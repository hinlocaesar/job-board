using FluentAssertions;
using JobBoard.Api.Features.Imports;
using JobBoard.Domain.Enums;
using Xunit;

namespace JobBoard.UnitTests;

public class ExternalJobMapperTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static RawExternalJob Raw(
        string title = "Frontend Engineer",
        string company = "Acme",
        string? category = null,
        IReadOnlyList<string>? tags = null,
        string? jobType = null,
        string? level = null,
        string? salary = null,
        string? html = null,
        string? url = null,
        DateTime? publishedAt = null) =>
        new(
            Source: "jobicy",
            SourceId: "42",
            Title: title,
            Company: company,
            DescriptionHtml: html,
            Category: category,
            Tags: tags ?? [],
            JobType: jobType,
            ExperienceLevel: level,
            Salary: salary,
            Url: url,
            PublishedAt: publishedAt);

    [Theory]
    [InlineData("full_time", JobType.FullTime)]
    [InlineData("Full-Time", JobType.FullTime)]
    [InlineData("permanent", JobType.FullTime)]
    [InlineData("part_time", JobType.PartTime)]
    [InlineData("contract", JobType.Contract)]
    [InlineData("Freelance", JobType.Freelance)]
    [InlineData("internship", JobType.Internship)]
    public void Maps_known_job_types(string raw, JobType expected) =>
        ExternalJobMapper.ResolveJobType(raw).Should().Be(expected);

    [Theory]
    [InlineData("A Marketing Internship", JobType.Internship)]
    [InlineData("Contract Python Developer", JobType.Contract)]
    [InlineData("Freelance Illustrator", JobType.Freelance)]
    [InlineData("Part Time Barista", JobType.PartTime)]
    [InlineData("Backend Engineer", JobType.FullTime)]
    public void Falls_back_to_the_title_when_the_feed_type_is_unknown(string title, JobType expected) =>
        ExternalJobMapper.ResolveJobType(null, title).Should().Be(expected);

    [Theory]
    [InlineData("Senior", ExperienceLevel.Senior)]
    [InlineData("Junior", ExperienceLevel.Junior)]
    [InlineData("Mid", ExperienceLevel.Mid)]
    [InlineData("Lead", ExperienceLevel.Lead)]
    [InlineData("Manager", ExperienceLevel.Senior)]
    public void Maps_experience_levels(string raw, ExperienceLevel expected) =>
        ExternalJobMapper.ResolveExperience(raw).Should().Be(expected);

    [Theory]
    [InlineData("Principal Engineer", ExperienceLevel.Lead)]
    [InlineData("Staff Software Engineer", ExperienceLevel.Senior)]
    [InlineData("Graduate Developer", ExperienceLevel.Junior)]
    [InlineData("Marketing Manager", ExperienceLevel.Mid)]
    public void Infers_experience_from_the_title(string title, ExperienceLevel expected) =>
        ExternalJobMapper.ResolveExperience(null, title).Should().Be(expected);

    [Theory]
    [InlineData("Software Development", "web-development")]
    [InlineData("Design", "graphic-design")]
    [InlineData("Customer Support & Success", "customer-support")]
    [InlineData("Writing & Content", "writing-content")]
    [InlineData("Marketing", "digital-marketing")]
    [InlineData("Data", "data-analytics")]
    [InlineData("Accounting", "accounting-finance")]
    [InlineData("Virtual Assistance", "virtual-assistance")]
    public void Maps_feed_categories_to_our_taxonomy(string category, string expected) =>
        ExternalJobMapper.ResolveCategory(Raw(category: category)).Should().Be(expected);

    [Fact]
    public void Falls_back_to_virtual_assistance_for_unknown_categories() =>
        ExternalJobMapper.ResolveCategory(Raw(category: "Something else entirely"))
            .Should().Be(ExternalJobMapper.FallbackCategorySlug);

    [Fact]
    public void Uses_tags_to_pick_a_category_when_the_category_is_missing() =>
        ExternalJobMapper.ResolveCategory(Raw(category: null, tags: ["python", "django"]))
            .Should().Be("web-development");

    [Fact]
    public void Converts_html_to_plain_text()
    {
        var text = ExternalJobMapper.ToPlainText(
            "<h2>About</h2><p>We need a <strong>Vue</strong> dev.</p><script>alert(1)</script><ul><li>Remote</li></ul>");

        text.Should().NotContain("<");
        text.Should().NotContain("alert(1)");
        text.Should().Contain("About");
        text.Should().Contain("Vue");
    }

    [Fact]
    public void Decodes_html_entities() =>
        ExternalJobMapper.ToPlainText("<p>Fast &amp; secure &mdash; 100%</p>")
            .Should().Contain("Fast & secure").And.Contain("100%");

    [Fact]
    public void Truncates_very_long_descriptions()
    {
        var raw = Raw(html: new string('a', 50_000));
        ExternalJobMapper.Map(raw, Now).Description.Length
            .Should().BeLessThanOrEqualTo(ExternalJobMapper.MaxDescriptionLength + 200);
    }

    [Fact]
    public void Appends_attribution_with_the_original_link()
    {
        var description = ExternalJobMapper.Map(
            Raw(company: "Acme", html: "<p>Hello</p>", url: "https://jobicy.com/jobs/42", publishedAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            Now).Description;

        description.Should().StartWith("Hello");
        description.Should().Contain("Published 1 Sep 2026");
        description.Should().Contain("Acme");
        description.Should().Contain("via jobicy");
        description.Should().Contain("Original posting: https://jobicy.com/jobs/42");
    }

    [Fact]
    public void Omits_the_original_link_when_the_feed_gives_none() =>
        ExternalJobMapper.Map(Raw(html: "<p>Hi</p>"), Now).Description
            .Should().NotContain("Original posting");

    [Theory]
    [InlineData("$30 - $45 per hour", 30, 45, "USD")]
    [InlineData("€40", 40, 40, "EUR")]
    [InlineData("£25 an hour", 25, 25, "GBP")]
    public void Parses_salary_ranges(string salary, double min, double max, string currency)
    {
        var draft = ExternalJobMapper.Map(Raw(salary: salary), Now);
        draft.PayMin.Should().Be((decimal)min);
        draft.PayMax.Should().Be((decimal)max);
        draft.Currency.Should().Be(currency);
    }

    [Fact]
    public void Treats_missing_salary_as_negotiable()
    {
        var draft = ExternalJobMapper.Map(Raw(salary: null), Now);
        draft.PayMin.Should().Be(0);
        draft.PayMax.Should().Be(0);
        draft.Currency.Should().Be("USD");
    }

    [Fact]
    public void Splits_deduplicates_and_caps_skill_tags()
    {
        var draft = ExternalJobMapper.Map(
            Raw(tags: ["Vue.js, TypeScript", "vue.js", "Node", "React", "GraphQL", "AWS", "Docker", "CI", "Testing", "Jest"]),
            Now);

        draft.Skills.Should().HaveCount(8);
        draft.Skills.Should().OnlyHaveUniqueItems();
        draft.Skills.Select(s => s.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
        draft.Skills.Should().Contain("Vue.js").And.Contain("TypeScript");
    }

    [Fact]
    public void Never_returns_a_blank_title_or_company()
    {
        var draft = ExternalJobMapper.Map(Raw(title: "  ", company: ""), Now);
        draft.Title.Should().Be("Untitled remote role");
        draft.Company.Should().Be("Unknown company");
    }

    [Fact]
    public void Uses_the_feed_publish_date_and_defaults_to_now()
    {
        var published = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);
        ExternalJobMapper.Map(Raw(publishedAt: published), Now).PublishedAt.Should().Be(published);
        ExternalJobMapper.Map(Raw(publishedAt: null), Now).PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Carries_the_provenance_fields_used_for_deduplication()
    {
        var draft = ExternalJobMapper.Map(Raw(), Now);
        draft.Source.Should().Be("jobicy");
        draft.SourceId.Should().Be("42");
    }
}
