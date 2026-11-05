using System.IO.Compression;

namespace Defra.TradeImports.Messaging.Encoding
{
    public class GZipEncoder : IMessageEncoder
    {
        public (string content, string? contentEncoding) Encode(
            string message,
            int compressionThresholdBytes        
            )
        {
            if (compressionThresholdBytes < 0)
            {
                throw new ArgumentException("Value must be greater than 0", nameof(compressionThresholdBytes));
            }

            // HOT PATH: no compression
            if (System.Text.Encoding.UTF8.GetByteCount(message) <= compressionThresholdBytes)
                return (message, null);

            using var memoryStream = new MemoryStream();

            // Leave stream open so we can read after dispose
            using (var gzip = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
            {
                using var writer = new StreamWriter(
                    gzip,
                    System.Text.Encoding.UTF8,
                    bufferSize: 1024,
                    leaveOpen: true
                );

                writer.Write(message);
            }
            
            // No extra copy besides Base64 (unavoidable)
            return (
                Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length),
                EncodingTypes.GzipBase64
            );
        }
    }
}
