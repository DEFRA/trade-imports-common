# Defra.TradeImports.SMB.CompressedSerializer

This library provides a **high-performance JSON message serializer** for **SlimMessageBus** that transparently applies **GZip compression with Base64 encoding** for large messages, while keeping the hot path allocation-free for small payloads.

It is intended for service-to-service messaging scenarios where payload sizes can vary significantly and bandwidth efficiency matters.

---

## Key features

- Implements `IMessageSerializer`, `IMessageSerializer<string>`, and `IMessageSerializerProvider`
- Uses **System.Text.Json** with web defaults
- Automatically compresses payloads larger than **256 KB**
- Signals compression via the `Content-Encoding` header
- Zero extra allocations on the hot path (uncompressed messages)
- Fully compatible with SlimMessageBus transports supporting byte[] or string payloads

---

## Compression behavior

| Payload size | Behavior |
|--------------|----------|
| ≤ 256 KB     | Sent as plain UTF-8 JSON |
| > 256 KB     | GZip-compressed and Base64-encoded |

When compression is applied, the following header is added:

