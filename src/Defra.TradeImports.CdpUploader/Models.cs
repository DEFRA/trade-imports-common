using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.TradeImports.CdpUploader;

// Contract: https://github.com/DEFRA/cdp-uploader/blob/main/README.md

/// <summary>The body of <c>POST /initiate</c>. Give exactly one of <see cref="Redirect"/> or <see cref="DownloadUrls"/>.</summary>
public sealed class InitiateUploadRequest
{
    /// <summary>
    /// Where the uploader redirects (302) after receiving an upload; relative in production. Required
    /// for browser and API uploads even when, as for a backend caller, the redirect is not followed.
    /// </summary>
    [JsonPropertyName("redirect")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Redirect { get; init; }

    /// <summary>Files the uploader should fetch and scan itself (the API download pattern), instead of being sent them.</summary>
    [JsonPropertyName("downloadUrls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? DownloadUrls { get; init; }

    /// <summary>Destination bucket; must be on the uploader's allowlist for the environment.</summary>
    [JsonPropertyName("s3Bucket")]
    public required string S3Bucket { get; init; }

    /// <summary>Folder within the bucket. Files land at <c>{s3Path}/{uploadId}/{fileId}</c>.</summary>
    [JsonPropertyName("s3Path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? S3Path { get; init; }

    /// <summary>Called with the <see cref="UploadStatusResponse"/> once every file has been scanned.</summary>
    [JsonPropertyName("callback")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Callback { get; init; }

    /// <summary>Returned unchanged on status responses and the callback.</summary>
    [JsonPropertyName("metadata")]
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("mimeTypes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? MimeTypes { get; init; }

    /// <summary>In bytes.</summary>
    [JsonPropertyName("maxFileSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MaxFileSize { get; init; }
}

public sealed class InitiateUploadResponse
{
    [JsonPropertyName("uploadId")]
    public required string UploadId { get; init; }

    /// <summary>
    /// Where to send the file: relative (<c>/upload-and-scan/{uploadId}</c>) in CDP, absolute when the
    /// uploader runs in development. Absent for download requests.
    /// </summary>
    [JsonPropertyName("uploadUrl")]
    public string? UploadUrl { get; init; }

    [JsonPropertyName("statusUrl")]
    public string? StatusUrl { get; init; }
}

/// <summary>The body of <c>GET /status/{uploadId}</c>, and of the scan-results callback.</summary>
public sealed class UploadStatusResponse
{
    [JsonPropertyName("uploadStatus")]
    public string? UploadStatus { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; init; }

    /// <summary>Form parts by field name; a binary upload's file is under <c>file</c>.</summary>
    [JsonPropertyName("form")]
    public Dictionary<string, JsonElement>? Form { get; init; }

    /// <summary>Present once <see cref="UploadStatus"/> is <see cref="UploadStatuses.Ready"/>.</summary>
    [JsonPropertyName("numberOfRejectedFiles")]
    public int? NumberOfRejectedFiles { get; init; }

    [JsonIgnore]
    public bool IsReady => UploadStatus == UploadStatuses.Ready;

    /// <summary>A metadata value as a string (numbers and booleans are converted), or null.</summary>
    public string? GetMetadata(string key)
    {
        if (Metadata is null || !Metadata.TryGetValue(key, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    /// <summary>Every file in the form, across all fields (a field may hold several).</summary>
    public IReadOnlyList<UploadedFile> GetFiles()
    {
        var files = new List<UploadedFile>();

        foreach (var part in Form?.Values ?? Enumerable.Empty<JsonElement>())
        {
            var candidates =
                part.ValueKind == JsonValueKind.Array ? part.EnumerateArray().ToArray() : [part];
            files.AddRange(
                candidates
                    .Where(c =>
                        c.ValueKind == JsonValueKind.Object && c.TryGetProperty("fileId", out _)
                    )
                    .Select(c => c.Deserialize<UploadedFile>()!)
            );
        }

        return files;
    }

    /// <summary>The first file in the form, or null; what a single binary upload produces.</summary>
    public UploadedFile? GetFile() => GetFiles().FirstOrDefault();
}

public sealed class UploadedFile
{
    [JsonPropertyName("fileId")]
    public string? FileId { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    [JsonPropertyName("detectedContentType")]
    public string? DetectedContentType { get; init; }

    /// <summary>See <see cref="FileStatuses"/>.</summary>
    [JsonPropertyName("fileStatus")]
    public string? FileStatus { get; init; }

    [JsonPropertyName("contentLength")]
    public long? ContentLength { get; init; }

    [JsonPropertyName("checksumSha256")]
    public string? ChecksumSha256 { get; init; }

    /// <summary>Set once the file is <see cref="FileStatuses.Complete"/>.</summary>
    [JsonPropertyName("s3Bucket")]
    public string? S3Bucket { get; init; }

    /// <summary>Set once the file is <see cref="FileStatuses.Complete"/>.</summary>
    [JsonPropertyName("s3Key")]
    public string? S3Key { get; init; }

    [JsonPropertyName("hasError")]
    public bool? HasError { get; init; }

    /// <summary>See <see cref="CdpUploaderErrorCodes"/>.</summary>
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("errorParams")]
    public Dictionary<string, JsonElement>? ErrorParams { get; init; }

    /// <summary>Human readable; suitable to show to a user.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; init; }

    [JsonIgnore]
    public bool IsComplete =>
        FileStatus == FileStatuses.Complete && !string.IsNullOrWhiteSpace(S3Key);

    [JsonIgnore]
    public bool IsRejected => FileStatus == FileStatuses.Rejected || HasError == true;
}

public static class UploadStatuses
{
    public const string Initiated = "initiated";
    public const string Pending = "pending";
    public const string Ready = "ready";
}

public static class FileStatuses
{
    public const string Pending = "pending";
    public const string Complete = "complete";
    public const string Rejected = "rejected";
}

/// <summary>The uploader's stable <see cref="UploadedFile.ErrorCode"/> values.</summary>
public static class CdpUploaderErrorCodes
{
    public const string FileVirus = "FILE_VIRUS";
    public const string FileEmpty = "FILE_EMPTY";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string FileInvalidType = "FILE_INVALID_TYPE";
    public const string FileUploadFailed = "FILE_UPLOAD_FAILED";
    public const string FileDownloadFailed = "FILE_DOWNLOAD_FAILED";

    /// <summary>
    /// Whether the file itself is the problem, so sending it again will be rejected again. The
    /// others are the uploader's own failures and worth retrying.
    /// </summary>
    public static bool IsFileRejection(string? code) =>
        code is FileVirus or FileEmpty or FileTooLarge or FileInvalidType;
}
