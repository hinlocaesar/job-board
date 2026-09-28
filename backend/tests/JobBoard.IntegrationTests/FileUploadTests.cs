using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>Server-side upload rules: pdf/doc/docx/rtf only, extension must match the content type.</summary>
public sealed class FileUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FileUploadTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static MultipartFormDataContent Upload(string fileName, string contentType, string content = "%PDF-1.4\n% integration test\n")
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", fileName);
        return multipart;
    }

    [Fact]
    public async Task Uploading_A_Pdf_Resume_Succeeds()
    {
        var worker = await _factory.CreateWorkerAsync();
        using var content = Upload("resume.pdf", "application/pdf");

        var response = await worker.Client.PostAsync("/api/files/resume", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"a pdf resume must be accepted: {await response.Content.ReadAsStringAsync()}");
        var file = await response.ReadNodeAsync();
        Guid.Parse(file["id"]!.GetValue<string>()).Should().NotBeEmpty();
        file["fileName"]!.GetValue<string>().Should().Be("resume.pdf");
        file["contentType"]!.GetValue<string>().Should().Be("application/pdf");
        file["sizeBytes"]!.GetValue<long>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Uploading_A_Txt_File_Is_Rejected()
    {
        var worker = await _factory.CreateWorkerAsync();
        using var content = Upload("notes.txt", "text/plain", "just some plain text");

        var response = await worker.Client.PostAsync("/api/files/resume", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).Should().Be("file_type_not_allowed");
    }

    [Fact]
    public async Task Uploading_With_An_Extension_That_Does_Not_Match_The_Content_Type_Is_Rejected()
    {
        var worker = await _factory.CreateWorkerAsync();
        using var content = Upload("resume.txt", "application/pdf");

        var response = await worker.Client.PostAsync("/api/files/resume", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).Should().Be("file_type_not_allowed");
    }

    [Fact]
    public async Task Uploading_Without_A_Token_Returns_401()
    {
        var anonymous = _factory.CreateClient();
        using var content = Upload("resume.pdf", "application/pdf");

        var response = await anonymous.PostAsync("/api/files/resume", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
