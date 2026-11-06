using System.IO.Compression;

namespace Defra.TradeImports.Messaging.Encoding
{
    public partial class GZipEncoder(ILogger<GZipEncoder> logger) : IMessageEncoder
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

            var messageSizeBytes = System.Text.Encoding.UTF8.GetByteCount(message);
            
            // HOT PATH: no compression
            if (messageSizeBytes <= compressionThresholdBytes)
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

            var encoded = Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            var encodedSizeBytes = System.Text.Encoding.UTF8.GetByteCount(encoded);

            LogEncodedMessageSuccessfully(logger, messageSizeBytes, encodedSizeBytes);

            return (encoded, EncodingTypes.GzipBase64);
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Encoded message of {InitialSizeBytes} to {ProcessedSizeBytes} bytes")]
        public static partial void LogEncodedMessageSuccessfully(ILogger logger, long initialSizeBytes, long processedSizeBytes);
    }
}
