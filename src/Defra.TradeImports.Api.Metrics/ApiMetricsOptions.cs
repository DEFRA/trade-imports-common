using System.ComponentModel.DataAnnotations;

namespace Defra.TradeImports.Api.Metrics;

public class ApiMetricsOptions
{
    [Required]
    public required string MeterName { get; init; } = string.Empty;
    
    [Required]
    public required string[] IgnoredPathPrefixes { get; init; } = [];
}