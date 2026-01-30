using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Serialization.SystemTextJson;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.TradeImports.SMB.CompressedSerializer;

//// <summary>
/// Implementation of <see cref="IMessageSerializer"/> using <see cref="CompressedJsonMessageSerializer"/>.
/// </summary>
public class CompressedJsonMessageSerializer
	: IMessageSerializer,
		IMessageSerializer<string>,
		IMessageSerializerProvider
{
	private static readonly string GzipBase64 = "gzip, base64";
	private const int CompressionThreshold = 256 * 1000;

	/// <summary>
	/// <see cref="JsonSerializerOptions"/> options for the JSON serializer. By default adds <see cref="ObjectToInferredTypesConverter"/> converter.
	/// </summary>
	public JsonSerializerOptions Options { get; set; }

	public CompressedJsonMessageSerializer(JsonSerializerOptions? options = null)
	{
		Options = options ?? CreateDefaultOptions();
	}

	public JsonSerializerOptions CreateDefaultOptions()
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
		{
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			AllowTrailingCommas = true,
		};
		options.Converters.Add(new ObjectToInferredTypesConverter());
		return options;
	}

	#region Implementation of IMessageSerializer

	public object Deserialize(
		Type messageType,
		IReadOnlyDictionary<string, object> headers,
		byte[] payload,
		object transportMessage
	)
	{
		return !TryGetGzip(headers) 
			? DeserializeUncompressed(payload, messageType) // HOT PATH: no compression
			: DeserializeGzip(payload, messageType);  // COLD PATH: gzip
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetGzip(IReadOnlyDictionary<string, object> headers)
	{
		if (!headers.TryGetValue("Content-Encoding", out var value))
			return false;

		// Avoid ToString() allocations if already string
		if (value is string s)
		{
			if (string.Equals(s, GzipBase64, StringComparison.Ordinal))
				return true;

			throw new NotImplementedException(
				$"Only '{GzipBase64}' content encoding is supported, passed: {s}"
			);
		}

		// Rare case
		var str = value?.ToString();
		if (string.Equals(str, GzipBase64, StringComparison.Ordinal))
			return true;

		throw new NotImplementedException(
			$"Only '{GzipBase64}' content encoding is supported, passed: {str}"
		);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private object DeserializeUncompressed(byte[] payload, Type messageType)
	{
		using var stream = new MemoryStream(payload, writable: false);
		return JsonSerializer.Deserialize(stream, messageType, Options)!;
	}

	private object DeserializeGzip(byte[] payload, Type messageType)
	{
		using var compressed = new MemoryStream(payload, writable: false);
		using var gzip = new GZipStream(
			compressed,
			CompressionMode.Decompress,
			leaveOpen: false
		);

		return JsonSerializer.Deserialize(gzip, messageType, Options)!;
	}

	public byte[] Serialize(
		Type messageType,
		IDictionary<string, object> headers,
		object message,
		object transportMessage
	)
	{
		// Serialize directly to UTF-8 bytes (no string allocation)
		byte[] utf8Json = JsonSerializer.SerializeToUtf8Bytes(
			message,
			messageType,
			Options);

		// HOT PATH: no compression
		if (utf8Json.Length <= CompressionThreshold)
			return utf8Json;

		headers["Content-Encoding"] = GzipBase64;

		using var memoryStream = new MemoryStream();

		using (var gzip = new GZipStream(
			       memoryStream,
			       CompressionLevel.Fastest,
			       leaveOpen: true))
		{
			gzip.Write(utf8Json, 0, utf8Json.Length);
		}

		// Single allocation for final payload
		return memoryStream.ToArray();
	}

	#endregion

	#region Implementation of IMessageSerializer<string>

	string IMessageSerializer<string>.Serialize(
		Type messageType,
		IDictionary<string, object> headers,
		object message,
		object transportMessage
	)
	{
		// Serialize once to string (needed anyway for string transport)
		string json = JsonSerializer.Serialize(message, messageType, Options);

		// HOT PATH: no compression
		if (json.Length <= CompressionThreshold)
			return json;

		headers["Content-Encoding"] = GzipBase64;

		using var memoryStream = new MemoryStream();

		// Leave stream open so we can read after dispose
		using (var gzip = new GZipStream(
			       memoryStream,
			       CompressionLevel.Fastest,
			       leaveOpen: true))
		{
			using var writer = new StreamWriter(
				gzip,
				Encoding.UTF8,
				bufferSize: 1024,
				leaveOpen: true);

			writer.Write(json);
		}

		// No extra copy besides Base64 (unavoidable)
		return Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
	}

	public object Deserialize(
		Type messageType,
		IReadOnlyDictionary<string, object> headers,
		string payload,
		object transportMessage
	)
	{
		return !TryGetGzip(headers)
			? JsonSerializer.Deserialize(payload, messageType, Options)! // HOT PATH: no compression
			: DeserializeGzip(Convert.FromBase64String(payload), messageType);  // COLD PATH: gzip
	}

	#endregion

	#region Implementation of IMessageSerializerProvider

	public IMessageSerializer GetSerializer(string path) => this;

	#endregion
}
