namespace Defra.TradeImports.Messaging;

public class MessagingOptions
{
    /// <summary>
    /// Meter the publish and consume metrics are recorded on. Use the meter your EMF exporter
    /// already exports so they reach CloudWatch.
    /// </summary>
    public string MeterName { get; set; } = "Defra.TradeImports.Messaging";
}
