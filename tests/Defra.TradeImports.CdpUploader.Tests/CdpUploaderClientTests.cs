using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.CdpUploader.Tests;

public class CdpUploaderClientTests
{
    private readonly StubHandler _handler = new();

    private CdpUploaderClient CreateClient(
        string? callbackUrl = "https://service.dev.cdp-int.defra.cloud/callback"
    ) =>
        new(
            new HttpClient(_handler) { BaseAddress = new Uri("http://cdp-uploader:7337") },
            Options.Create(
                new CdpUploaderOptions
                {
                    BaseUrl = "http://cdp-uploader:7337",
                    CallbackUrl = callbackUrl,
                }
            )
        );

    private static InitiateUploadRequest Request(string? callback = null) =>
        new()
        {
            Redirect = "/",
            S3Bucket = "bucket",
            S3Path = "folder",
            Callback = callback,
            Metadata = new Dictionary<string, string> { ["reference"] = "ref-1" },
            MaxFileSize = 1000,
        };

    [Fact]
    public async Task Initiate_PostsTheRequestAndReturnsTheUpload()
    {
        _handler.Respond(
            HttpStatusCode.Created,
            """{"uploadId":"u1","uploadUrl":"/upload-and-scan/u1","statusUrl":"http://cdp-uploader:7337/status/u1"}"""
        );

        var upload = await CreateClient().Initiate(Request(), CancellationToken.None);

        upload.UploadId.Should().Be("u1");
        upload.UploadUrl.Should().Be("/upload-and-scan/u1");
        _handler.Last!.Method.Should().Be(HttpMethod.Post);
        _handler.Last.RequestUri!.AbsolutePath.Should().Be("/initiate");

        var body = JsonDocument.Parse(_handler.LastBody!).RootElement;
        body.GetProperty("redirect").GetString().Should().Be("/");
        body.GetProperty("s3Bucket").GetString().Should().Be("bucket");
        body.GetProperty("s3Path").GetString().Should().Be("folder");
        body.GetProperty("maxFileSize").GetInt64().Should().Be(1000);
        body.GetProperty("metadata").GetProperty("reference").GetString().Should().Be("ref-1");
        body.TryGetProperty("downloadUrls", out _).Should().BeFalse();
        body.TryGetProperty("mimeTypes", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Initiate_WithoutACallback_UsesTheConfiguredOne()
    {
        _handler.Respond(
            HttpStatusCode.Created,
            """{"uploadId":"u1","uploadUrl":"/upload-and-scan/u1"}"""
        );

        await CreateClient().Initiate(Request(), CancellationToken.None);

        JsonDocument
            .Parse(_handler.LastBody!)
            .RootElement.GetProperty("callback")
            .GetString()
            .Should()
            .Be("https://service.dev.cdp-int.defra.cloud/callback");
    }

    [Fact]
    public async Task Initiate_WithACallback_KeepsIt()
    {
        _handler.Respond(
            HttpStatusCode.Created,
            """{"uploadId":"u1","uploadUrl":"/upload-and-scan/u1"}"""
        );

        await CreateClient().Initiate(Request("https://other/callback"), CancellationToken.None);

        JsonDocument
            .Parse(_handler.LastBody!)
            .RootElement.GetProperty("callback")
            .GetString()
            .Should()
            .Be("https://other/callback");
    }

    [Fact]
    public async Task Initiate_DownloadRequest_NeedsNoUploadUrl()
    {
        _handler.Respond(
            HttpStatusCode.Accepted,
            """{"uploadId":"u1","statusUrl":"http://cdp-uploader/status/u1"}"""
        );

        var upload = await CreateClient()
            .Initiate(
                new InitiateUploadRequest
                {
                    DownloadUrls = ["https://files/1"],
                    S3Bucket = "bucket",
                },
                CancellationToken.None
            );

        upload.UploadUrl.Should().BeNull();
        JsonDocument
            .Parse(_handler.LastBody!)
            .RootElement.TryGetProperty("redirect", out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Initiate_NeedsExactlyOneOfRedirectOrDownloadUrls()
    {
        var act = () =>
            CreateClient()
                .Initiate(
                    new InitiateUploadRequest
                    {
                        Redirect = "/",
                        DownloadUrls = ["https://files/1"],
                        S3Bucket = "bucket",
                    },
                    CancellationToken.None
                );

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Initiate_WhenTheUploaderRejectsIt_ThrowsWithTheStatusAndBody()
    {
        _handler.Respond(
            HttpStatusCode.BadRequest,
            """{"message":"No permission to write to bucket"}"""
        );

        var act = () => CreateClient().Initiate(Request(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<CdpUploaderException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        thrown.Which.Message.Should().Contain("No permission to write to bucket");
    }

    [Fact]
    public async Task Initiate_WhenTheUploaderIsUnreachable_Throws()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var act = () => CreateClient().Initiate(Request(), CancellationToken.None);

        (await act.Should().ThrowAsync<CdpUploaderException>()).Which.StatusCode.Should().BeNull();
    }

    [Fact]
    public async Task Upload_SendsTheBytesAsABinaryUploadToTheUploadPath()
    {
        _handler.Respond(HttpStatusCode.Redirect, "");

        await CreateClient()
            .Upload(
                "http://localhost:7337/upload-and-scan/u1",
                "hello"u8.ToArray(),
                "report.pdf",
                cancellationToken: CancellationToken.None
            );

        _handler
            .Last!.RequestUri!.ToString()
            .Should()
            .Be("http://cdp-uploader:7337/upload-and-scan/u1");
        _handler.LastContentType.Should().Be("application/octet-stream");
        _handler.Last.Headers.GetValues("x-filename").Should().Equal("report.pdf");
        _handler.LastBody.Should().Be("hello");
    }

    [Fact]
    public async Task Upload_WithAContentType_DeclaresIt()
    {
        _handler.Respond(HttpStatusCode.Redirect, "");

        await CreateClient()
            .Upload("/upload-and-scan/u1", "%PDF"u8.ToArray(), "report.pdf", "application/pdf");

        _handler.LastContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Upload_EncodesFileNamesThatAreNotValidHeaderValues()
    {
        _handler.Respond(HttpStatusCode.Redirect, "");

        await CreateClient()
            .Upload(
                "/upload-and-scan/u1",
                new MemoryStream([1]),
                "résumé.pdf",
                cancellationToken: CancellationToken.None
            );

        _handler.Last!.Headers.GetValues("x-filename").Should().Equal("r%C3%A9sum%C3%A9.pdf");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Upload_WhenRejected_Throws(HttpStatusCode statusCode)
    {
        _handler.Respond(statusCode, "nope");

        var act = () =>
            CreateClient()
                .Upload("/upload-and-scan/u1", [1], "f", cancellationToken: CancellationToken.None);

        (await act.Should().ThrowAsync<CdpUploaderException>())
            .Which.StatusCode.Should()
            .Be(statusCode);
    }

    [Fact]
    public async Task GetStatus_ReturnsTheUploadAndItsFile()
    {
        _handler.Respond(
            HttpStatusCode.OK,
            """
            {"uploadStatus":"ready","metadata":{"reference":"ref-1","count":2},
             "form":{"file":{"fileId":"f1","filename":"a.pdf","fileStatus":"complete","contentLength":14,
                             "s3Bucket":"bucket","s3Key":"folder/u1/f1","checksumSha256":"abc"},
                     "note":"text field"},
             "numberOfRejectedFiles":0}
            """
        );

        var status = await CreateClient().GetStatus("u1", CancellationToken.None);

        _handler.Last!.RequestUri!.AbsolutePath.Should().Be("/status/u1");
        status!.IsReady.Should().BeTrue();
        status.GetMetadata("reference").Should().Be("ref-1");
        status.GetMetadata("count").Should().Be("2");
        status.GetMetadata("missing").Should().BeNull();
        var file = status.GetFile()!;
        file.IsComplete.Should().BeTrue();
        file.IsRejected.Should().BeFalse();
        file.S3Key.Should().Be("folder/u1/f1");
        file.ContentLength.Should().Be(14);
    }

    [Fact]
    public async Task GetStatus_ReadsRejectedFilesAndMultipleFilesPerField()
    {
        _handler.Respond(
            HttpStatusCode.OK,
            """
            {"uploadStatus":"ready","form":{"files":[
              {"fileId":"f1","fileStatus":"rejected","hasError":true,"errorCode":"FILE_VIRUS","errorMessage":"The selected file contains a virus"},
              {"fileId":"f2","fileStatus":"complete","s3Key":"k2"}]},"numberOfRejectedFiles":1}
            """
        );

        var files = (await CreateClient().GetStatus("u1", CancellationToken.None))!.GetFiles();

        files.Should().HaveCount(2);
        files[0].IsRejected.Should().BeTrue();
        files[0].ErrorCode.Should().Be(CdpUploaderErrorCodes.FileVirus);
        CdpUploaderErrorCodes.IsFileRejection(files[0].ErrorCode).Should().BeTrue();
        files[1].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_ForAnUnknownUpload_ReturnsNull()
    {
        _handler.Respond(HttpStatusCode.NotFound, "");

        (await CreateClient().GetStatus("u1", CancellationToken.None)).Should().BeNull();
    }

    [Theory]
    [InlineData(CdpUploaderErrorCodes.FileVirus, true)]
    [InlineData(CdpUploaderErrorCodes.FileEmpty, true)]
    [InlineData(CdpUploaderErrorCodes.FileTooLarge, true)]
    [InlineData(CdpUploaderErrorCodes.FileInvalidType, true)]
    [InlineData(CdpUploaderErrorCodes.FileUploadFailed, false)]
    [InlineData(CdpUploaderErrorCodes.FileDownloadFailed, false)]
    [InlineData(null, false)]
    public void IsFileRejection_OnlyForProblemsWithTheFileItself(string? code, bool expected) =>
        CdpUploaderErrorCodes.IsFileRejection(code).Should().Be(expected);

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "";

        public HttpRequestMessage? Last { get; private set; }

        public string? LastBody { get; private set; }

        public string? LastContentType { get; private set; }

        public Exception? Throw { get; set; }

        public void Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Last = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            LastContentType = request.Content?.Headers.ContentType?.MediaType;

            if (Throw is not null)
                throw Throw;

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
