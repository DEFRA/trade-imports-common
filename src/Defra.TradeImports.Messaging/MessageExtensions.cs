using Amazon.SQS.Model;

using Defra.TradeImports.Messaging.Encoding;

using System.IO.Compression;

namespace Defra.TradeImports.Messaging
{
    public static class MessageExtensions
    {
        public static string GetDecodedBody(this Message message)
        {
            if (!message.MessageAttributes?.ContainsKey(MessageHeaders.ContentEncoding) == true)
            {
                return message.Body;
            }

            var contentEncoding = message.MessageAttributes![MessageHeaders.ContentEncoding].StringValue;
            if (contentEncoding != EncodingTypes.GzipBase64)
            {
                throw new NotSupportedException($"Content encoding of {message.MessageAttributes![MessageHeaders.ContentEncoding].StringValue} is not supported, currently only {EncodingTypes.GzipBase64} is supported");
            }

            var compressedBytes = Convert.FromBase64String(message.Body);
            using var compressedStream = new MemoryStream(compressedBytes);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream, System.Text.Encoding.UTF8);

            return reader.ReadToEnd();
        }
    }
}
