using System.ComponentModel.DataAnnotations;

namespace Defra.TradeImports.Messaging.Consuming;

/// <summary>One consumer's queue and polling settings. Bindable from configuration.</summary>
public class SqsConsumerOptions
{
    [Required]
    public string QueueUrl { get; set; } = null!;

    /// <summary>When false the consumer starts, logs that it is disabled and stops.</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 10)]
    public int MaxMessages { get; set; } = 10;

    /// <summary>Long-poll wait.</summary>
    [Range(0, 20)]
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// How long a received message stays hidden. A message whose consumer is still running when it
    /// expires is delivered again, so it must cover the slowest consume.
    /// </summary>
    [Range(0, 43200)]
    public int VisibilityTimeoutSeconds { get; set; } = 60;
}
