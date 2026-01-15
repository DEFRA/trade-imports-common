using FluentAssertions;
using SlimMessageBus.Host.Serialization;

namespace Defra.TradeImports.SMB.CompressedSerializer.Tests;

public class ToStringSerializerTests
{
    private readonly ToStringSerializer _toStringSerializer = new();

    [Fact]
    public void Deserialize_String_Returns_String()
    {
        ((IMessageSerializer<string>)_toStringSerializer)
            .Deserialize(null!, null!, "sosig", null!)
            .Should()
            .Be("sosig");
    }

    [Fact]
    public void Deserialize_Byte_ThrowsNotImplementedException()
    {
        ((IMessageSerializer<byte[]>)_toStringSerializer)
            .Deserialize(null!, null!, "sosig"u8.ToArray(), null!)
            .Should()
            .Be("sosig");
    }
}
