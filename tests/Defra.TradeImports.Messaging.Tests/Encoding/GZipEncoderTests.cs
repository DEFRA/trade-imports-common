using System.IO.Compression;
using Defra.TradeImports.Messaging.Encoding;
using FluentAssertions;

namespace Defra.TradeImports.Messaging.Tests.Encoding;

public class GZipEncoderTests
{
    [Fact]
    public void Encode_WithNegativeThreshold_ThrowsArgumentException()
    {
        var encoder = new GZipEncoder();

        var act = () => encoder.Encode("message", -1);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abc", 3)]
    public void Encode_WithMessageAtOrBelowThreshold_ReturnsOriginalMessageAndNoEncoding(
        string message,
        int threshold
    )
    {
        var encoder = new GZipEncoder();

        var (content, contentEncoding) = encoder.Encode(message, threshold);

        content.Should().Be(message);
        contentEncoding.Should().BeNull();
    }

    [Fact]
    public void Encode_WithMessageAboveThreshold_ReturnsGzipBase64ThatRoundTrips()
    {
        var encoder = new GZipEncoder();
        const string message = "This is a message that should be compressed";

        var (content, contentEncoding) = encoder.Encode(message, 5);

        contentEncoding.Should().Be(EncodingTypes.GzipBase64);
        Decode(content).Should().Be(message);
    }

    [Fact]
    public void Encode_WithZeroThresholdAndNonEmptyMessage_CompressesMessage()
    {
        var encoder = new GZipEncoder();

        var (content, contentEncoding) = encoder.Encode("abc", 0);

        content.Should().NotBe("abc");
        contentEncoding.Should().Be(EncodingTypes.GzipBase64);
        Decode(content).Should().Be("abc");
    }

    private static string Decode(string encoded)
    {
        var compressed = Convert.FromBase64String(encoded);
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
