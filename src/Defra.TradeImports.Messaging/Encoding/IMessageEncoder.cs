namespace Defra.TradeImports.Messaging.Encoding;

public interface IMessageEncoder
{
    (string content, string? contentEncoding) Encode(
        string message,
        int compressionThresholdBytes
    );
}