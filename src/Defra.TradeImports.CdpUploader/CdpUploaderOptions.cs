using System.ComponentModel.DataAnnotations;

namespace Defra.TradeImports.CdpUploader;

public class CdpUploaderOptions
{
    public const string SectionName = "CdpUploader";

    /// <summary>e.g. <c>https://cdp-uploader.{env}.cdp-int.defra.cloud</c>, or <c>http://localhost:7337</c> locally.</summary>
    [Required]
    public string BaseUrl { get; set; } = null!;

    /// <summary>
    /// Optional: where the uploader posts scan results, used by <see cref="ICdpUploaderClient"/> when
    /// an initiate request has no callback of its own. In production the uploader only accepts
    /// <c>cdp-int.defra.cloud</c> hosts, i.e. the calling service's internal address.
    /// </summary>
    public string? CallbackUrl { get; set; }

    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 100;
}
