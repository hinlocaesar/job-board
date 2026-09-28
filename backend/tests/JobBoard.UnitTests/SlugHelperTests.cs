using FluentAssertions;
using JobBoard.Api.Common;

namespace JobBoard.UnitTests;

public sealed class SlugHelperTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("Senior Vue.js Developer", "senior-vue-js-developer")]
    [InlineData("Top 5 Jobs 2026", "top-5-jobs-2026")]
    [InlineData("already-slugged", "already-slugged")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("Multiple   Spaces  Between", "multiple-spaces-between")]
    [InlineData("FULL_TIME", "full-time")]
    [InlineData("Trailing punctuation...", "trailing-punctuation")]
    [InlineData("---weird---start---end---", "weird-start-end")]
    [InlineData("a--b", "a-b")]
    public void Slugify_Normalises_Common_Inputs(string value, string expected)
        => SlugHelper.Slugify(value).Should().Be(expected);

    [Theory]
    [InlineData("Café Déjà Vu", "cafe-deja-vu")]
    [InlineData("Ünïcödé Tëst", "unicode-test")]
    public void Slugify_Strips_Diacritics(string value, string expected)
        => SlugHelper.Slugify(value).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("___")]
    public void Slugify_Falls_Back_To_Item_For_Values_Without_Content(string? value)
        => SlugHelper.Slugify(value!).Should().Be("item");

    [Fact]
    public void Slugify_Never_Leaves_Edge_Or_Double_Dashes()
    {
        var slug = SlugHelper.Slugify("  -- Hello,  -- World! --  ");

        slug.Should().Be("hello-world");
        slug.Should().NotStartWith("-");
        slug.Should().NotEndWith("-");
        slug.Should().NotContain("--");
    }

    [Fact]
    public void WithPrefix_Prefixes_The_Slug()
        => SlugHelper.WithPrefix("j", "my-job").Should().Be("j-my-job");
}
