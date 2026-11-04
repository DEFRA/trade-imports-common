using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.CdpUploader;

public class CdpUploaderException(
    string message,
    HttpStatusCode? statusCode = null,
    Exception? inner = null
) : Exception(message, inner)
{
    /// <summary>The uploader's response status, or null if no response was received.</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public interface ICdpUploaderClient
{
    /// <summary>
    /// Starts an upload. When the request has no callback, <see cref="CdpUploaderOptions.CallbackUrl"/>
    /// is used.
    /// </summary>
    Task<InitiateUploadResponse> Initiate(
        InitiateUploadRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Sends one file as an API (binary) upload to an initiated upload's <c>uploadUrl</c>. The
    /// uploader quarantines and scans it asynchronously; follow progress with
    /// <see cref="GetStatus"/> or the callback.
    /// </summary>
    /// <param name="uploadUrl">The <c>uploadUrl</c> from <see cref="Initiate"/>; relative or absolute.</param>
    /// <param name="content">The file.</param>
    /// <param name="fileName">Recorded by the uploader as the file's name.</param>
    /// <param name="contentType">
    /// The file's type, sent as the upload's Content-Type (default <c>application/octet-stream</c>).
    /// The uploader checks the initiate request's <c>mimeTypes</c> against this declared type, not
    /// the type it detects, so set it when <c>mimeTypes</c> is used, and compare
    /// <see cref="UploadedFile.DetectedContentType"/> in the result if the declared type is not trusted.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task Upload(
        string uploadUrl,
        Stream content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc cref="Upload(string, Stream, string, string?, CancellationToken)"/>
    Task Upload(
        string uploadUrl,
        byte[] content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>The upload's state; null if the uploader does not know the upload id.</summary>
    Task<UploadStatusResponse?> GetStatus(string uploadId, CancellationToken cancellationToken);
}

/// <summary>
/// Typed client for the CDP uploader (https://github.com/DEFRA/cdp-uploader). Register it with
/// <see cref="ServiceCollectionExtensions.AddCdpUploaderClient"/>, which turns automatic redirects
/// off: a successful upload answers 302 to the initiate request's redirect.
/// </summary>
public class CdpUploaderClient(HttpClient httpClient, IOptions<CdpUploaderOptions> options)
    : ICdpUploaderClient
{
    public async Task<InitiateUploadResponse> Initiate(
        InitiateUploadRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.Redirect is null == request.DownloadUrls is null)
            throw new ArgumentException(
                "Give exactly one of Redirect or DownloadUrls.",
                nameof(request)
            );

        var callback = request.Callback ?? options.Value.CallbackUrl;
        var body = callback == request.Callback ? request : WithCallback(request, callback);

        using var response = await Send(
            () => httpClient.PostAsJsonAsync("/initiate", body, cancellationToken),
            "initiate"
        );
        await EnsureSuccess(response, "initiate", cancellationToken);

        var initiated = await response.Content.ReadFromJsonAsync<InitiateUploadResponse>(
            cancellationToken
        );
        if (initiated is null || string.IsNullOrWhiteSpace(initiated.UploadId))
            throw new CdpUploaderException(
                "CDP uploader initiate returned no uploadId",
                response.StatusCode
            );

        if (request.Redirect is not null && string.IsNullOrWhiteSpace(initiated.UploadUrl))
            throw new CdpUploaderException(
                "CDP uploader initiate returned no uploadUrl",
                response.StatusCode
            );

        return initiated;
    }

    public Task Upload(
        string uploadUrl,
        byte[] content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default
    ) => Send(uploadUrl, new ByteArrayContent(content), fileName, contentType, cancellationToken);

    public Task Upload(
        string uploadUrl,
        Stream content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default
    ) => Send(uploadUrl, new StreamContent(content), fileName, contentType, cancellationToken);

    public async Task<UploadStatusResponse?> GetStatus(
        string uploadId,
        CancellationToken cancellationToken
    )
    {
        using var response = await Send(
            () =>
                httpClient.GetAsync($"/status/{Uri.EscapeDataString(uploadId)}", cancellationToken),
            "status"
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccess(response, "status", cancellationToken);
        return await response.Content.ReadFromJsonAsync<UploadStatusResponse>(cancellationToken);
    }

    private async Task Send(
        string uploadUrl,
        HttpContent content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ToRelative(uploadUrl));
        request.Content = content;
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
        );
        request.Headers.TryAddWithoutValidation("x-filename", ToHeaderSafe(fileName));

        using var response = await Send(
            () => httpClient.SendAsync(request, cancellationToken),
            "upload"
        );

        // Success is the 302 to the redirect. 404 (unknown upload), 403 (a download request) and
        // 422 (upload id already used) are failures, as is anything 5xx.
        if ((int)response.StatusCode >= 400)
            await EnsureSuccess(response, "upload", cancellationToken);
    }

    private static InitiateUploadRequest WithCallback(
        InitiateUploadRequest request,
        string? callback
    ) =>
        new()
        {
            Redirect = request.Redirect,
            DownloadUrls = request.DownloadUrls,
            S3Bucket = request.S3Bucket,
            S3Path = request.S3Path,
            Callback = callback,
            Metadata = request.Metadata,
            MimeTypes = request.MimeTypes,
            MaxFileSize = request.MaxFileSize,
        };

    // In development the uploader returns an absolute uploadUrl built from its own APP_BASE_URL, which
    // may not resolve from the caller; only the path is used, against the configured base address.
    internal static string ToRelative(string uploadUrl) =>
        Uri.TryCreate(uploadUrl, UriKind.Absolute, out var absolute)
        && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            ? absolute.PathAndQuery
            : uploadUrl;

    // Header values must be printable ASCII; anything else is percent-encoded.
    internal static string ToHeaderSafe(string fileName) =>
        fileName.All(c => c is >= ' ' and <= '~') ? fileName : Uri.EscapeDataString(fileName);

    private static async Task<HttpResponseMessage> Send(
        Func<Task<HttpResponseMessage>> send,
        string operation
    )
    {
        try
        {
            return await send();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new CdpUploaderException($"CDP uploader {operation} request failed", inner: ex);
        }
    }

    private static async Task EnsureSuccess(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > 500)
            body = body[..500];

        var message = new StringBuilder(
            $"CDP uploader {operation} returned {(int)response.StatusCode}"
        );
        if (body.Length > 0)
            message.Append(": ").Append(body);

        throw new CdpUploaderException(message.ToString(), response.StatusCode);
    }
}
