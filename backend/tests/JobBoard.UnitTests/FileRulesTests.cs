using FluentAssertions;
using JobBoard.Domain.Abstractions;

namespace JobBoard.UnitTests;

public sealed class FileRulesTests
{
    [Theory]
    [InlineData("application/pdf", "resume.pdf")]
    [InlineData("application/msword", "resume.doc")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "resume.docx")]
    [InlineData("application/rtf", "cover.rtf")]
    [InlineData("text/rtf", "cover.rtf")]
    [InlineData("application/pdf", "Resume.PDF")] // extension match is case-insensitive
    [InlineData("application/pdf", "my-cv.v2.pdf")]
    public void IsAllowed_Accepts_Supported_Types_With_Matching_Extension(string contentType, string fileName)
    {
        FileRules.IsAllowed(contentType, fileName, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("text/plain", "notes.txt")]   // .txt is not allowed at all
    [InlineData("image/png", "logo.png")]     // images are not allowed
    [InlineData("application/pdf", "resume.txt")] // extension does not match the content type
    [InlineData("application/msword", "resume.docx")]
    [InlineData("application/pdf", "resume.rtf")]
    [InlineData("application/pdf", "")]       // empty file name
    [InlineData("", "resume.pdf")]            // empty content type
    [InlineData("application/PDF", "resume.doc")] // wrong extension for (case-insensitive) type
    public void IsAllowed_Rejects_Disallowed_Or_Mismatched_Input(string contentType, string fileName)
    {
        FileRules.IsAllowed(contentType, fileName, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Allowed_Types_Cover_Exactly_Pdf_Doc_Docx_And_Rtf()
    {
        // Both application/rtf and text/rtf map to .rtf, so the raw value list has 5 entries.
        FileRules.AllowedContentTypes.Values
            .Should().HaveCount(5)
            .And.Contain(".rtf");
        FileRules.AllowedContentTypes.Values.Distinct()
            .Should().BeEquivalentTo(".pdf", ".doc", ".docx", ".rtf");
        FileRules.MaxSizeBytes.Should().Be(5 * 1024 * 1024);
    }
}
