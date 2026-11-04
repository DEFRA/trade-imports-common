namespace Defra.TradeImports.SMB.CompressedSerializer;

public interface ICompressedJsonMessageEncoder
{
    (string content, string? contentEncoding) Encode(
        Type messageType,
        object message,
        int compressionThreshold
    );
}