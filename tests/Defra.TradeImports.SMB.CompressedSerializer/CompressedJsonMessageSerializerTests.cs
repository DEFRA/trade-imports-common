using System.IO.Compression;
using System.Text;
using System.Text.Json;

using FluentAssertions;

namespace Defra.TradeImports.SMB.CompressedSerializer.Tests;

public class CompressedJsonMessageSerializerTests
{
    [Fact]
    public void Encode_WhenPayloadIsUnderThreshold_ReturnsJsonWithoutContentEncoding()
    {
        var message = new { name = "Sosig" };

        var result = new CompressedJsonMessageSerializer().Encode(
            message.GetType(),
            message,
            compressionThreshold: 1024
        );

        result.content.Should().Be(JsonSerializer.Serialize(message, message.GetType()));
        result.contentEncoding.Should().BeNull();
    }

    [Fact]
    public void Encode_WhenPayloadIsOverThreshold_ReturnsCompressedBase64AndGzipEncoding()
    {
        var message = new { name = new string('a', 500) };
        var expectedJson = JsonSerializer.Serialize(message, message.GetType());

        var result = new CompressedJsonMessageSerializer().Encode(
            message.GetType(),
            message,
            compressionThreshold: 10
        );

        result.contentEncoding.Should().Be("gzip, base64");
        DecompressFromBase64(result.content).Should().Be(expectedJson);
    }

    private static string DecompressFromBase64(string base64Payload)
    {
        var compressedBytes = Convert.FromBase64String(base64Payload);

        using var compressedStream = new MemoryStream(compressedBytes);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();

        gzipStream.CopyTo(decompressedStream);

        return Encoding.UTF8.GetString(decompressedStream.ToArray()).TrimStart('\uFEFF');
    }
}