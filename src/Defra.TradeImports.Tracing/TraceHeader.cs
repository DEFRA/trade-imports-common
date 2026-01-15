using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Defra.TradeImports.Tracing;

public class TraceHeader
{
    [ConfigurationKeyName("TraceHeader")]
    [Required]
    public required string Name { get; set; }
}
