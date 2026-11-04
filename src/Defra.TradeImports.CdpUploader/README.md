# Defra.TradeImports.CdpUploader

A typed client for the [CDP uploader](https://github.com/DEFRA/cdp-uploader). The uploader takes a
file, quarantines it, scans it for malware and moves it to your S3 bucket. It then reports the
result on a callback and on `/status/{uploadId}`.

## Registration

```csharp
services
    .AddCdpUploaderClient(configuration)  // binds the "CdpUploader" section
    .AddHeaderPropagation();               // optional: forwards the trace header
```

```json
"CdpUploader": {
  "BaseUrl": "https://cdp-uploader.{env}.cdp-int.defra.cloud",
  "CallbackUrl": "https://my-service.{env}.cdp-int.defra.cloud/upload-callback",
  "TimeoutSeconds": 100
}
```

`CallbackUrl` is optional. It is used for any initiate request that does not set its own callback.
In production the uploader only calls back to `cdp-int.defra.cloud` hosts, so use your service's
internal address. Automatic redirects are off, because a successful upload answers with a 302.

## Uploading a file from a backend service

```csharp
var upload = await uploader.Initiate(new InitiateUploadRequest
{
    Redirect = "/",                       // required by the uploader, never followed here
    S3Bucket = "my-bucket",               // must be on the uploader's allowlist (#cdp-support)
    S3Path = "documents/123",             // files land at {s3Path}/{uploadId}/{fileId}
    Metadata = { ["reference"] = "123" }, // echoed back on status and callback
    MaxFileSize = 10_000_000,
    MimeTypes = ["application/pdf"],
}, ct);

await uploader.Upload(upload.UploadUrl!, bytesOrStream, "report.pdf", "application/pdf", ct);
```

`Upload` sends a binary (API) upload with an `x-filename` header and the file's content type
(`application/octet-stream` when none is given).

**`MimeTypes` is checked against the content type you declare on the upload, not the type the
uploader detects.** An upload declared as `application/octet-stream` is rejected
(`FILE_INVALID_TYPE`) whenever `MimeTypes` is set. A file declared as `application/pdf` is accepted
even if it is really something else; the result's `DetectedContentType` shows what it actually is,
so check that too if the declared type isn't trusted.

When the uploader runs locally it returns an absolute `uploadUrl`; only its path is used, against
`BaseUrl`.

For files the uploader should fetch itself, set `DownloadUrls` instead of `Redirect`.

## Results

Your callback endpoint receives an `UploadStatusResponse`, the same body `GetStatus(uploadId)`
returns:

```csharp
app.MapPost("/upload-callback", (UploadStatusResponse status) =>
{
    var reference = status.GetMetadata("reference");
    var file = status.GetFile();
    if (file?.IsComplete == true) { /* file.S3Bucket, file.S3Key, file.ChecksumSha256 */ }
    else if (file?.IsRejected == true) { /* file.ErrorCode, file.ErrorMessage */ }
    return Results.Ok(); // anything but 2xx and the uploader calls again
});
```

The uploader sends no credential on the callback. Keep the endpoint on the internal address only,
and check something only you know, e.g. a random token you put in `Metadata`.

`ErrorCode` values are in `CdpUploaderErrorCodes`: `FILE_VIRUS`, `FILE_EMPTY`, `FILE_TOO_LARGE`,
`FILE_INVALID_TYPE`, `FILE_UPLOAD_FAILED` and `FILE_DOWNLOAD_FAILED`.
`CdpUploaderErrorCodes.IsFileRejection(code)` tells a problem with the file itself (sending it again
will fail again) apart from the uploader's own failures.

Errors calling the uploader throw `CdpUploaderException`. Its `StatusCode` is null when the uploader
could not be reached.
