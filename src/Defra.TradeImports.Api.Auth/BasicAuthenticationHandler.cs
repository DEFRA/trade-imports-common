using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Encodings.Web;

namespace Defra.TradeImports.Api.Auth;

public sealed class BasicAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder,
	IAclTicketCache cache)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "Basic";
	private const string AuthorizationHeader = "Authorization";

	private static readonly Task<AuthenticateResult> NoResultTask =
		Task.FromResult(AuthenticateResult.NoResult());

	private static readonly Task<AuthenticateResult> FailTask =
		Task.FromResult(AuthenticateResult.Fail("Failed authorization"));

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var endpoint = Context.GetEndpoint();
		if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
			return NoResultTask;

		if (!Request.Headers.TryGetValue(AuthorizationHeader, out var authValues))
			return FailTask;

		var authHeader = authValues.Count > 0 ? authValues[0] : null;
		if (string.IsNullOrEmpty(authHeader))
			return FailTask;

		ReadOnlySpan<char> headerSpan = authHeader.AsSpan();

		if (!headerSpan.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
			return FailTask;

		ReadOnlySpan<char> b64Chars = headerSpan.Slice(6).Trim();
		if (b64Chars.IsEmpty)
			return FailTask;

		byte[]? rentedB64Bytes = null;
		byte[]? rentedDecodedBytes = null;
		char[]? rentedChars = null;

		try
		{
			// base64 chars -> ASCII bytes (pooled)
			rentedB64Bytes = ArrayPool<byte>.Shared.Rent(b64Chars.Length);
			var b64Bytes = rentedB64Bytes.AsSpan(0, b64Chars.Length);

			for (int i = 0; i < b64Chars.Length; i++)
			{
				char c = b64Chars[i];
				if (c > 0x7F) return FailTask;
				b64Bytes[i] = (byte)c;
			}

			// base64 decode -> credential bytes
			int maxDecodedLen = (b64Bytes.Length * 3) / 4 + 3;
			rentedDecodedBytes = ArrayPool<byte>.Shared.Rent(maxDecodedLen);
			var decodedBytes = rentedDecodedBytes.AsSpan(0, maxDecodedLen);

			var status = Base64.DecodeFromUtf8(b64Bytes, decodedBytes, out int consumed, out int written);
			if (status != OperationStatus.Done || consumed != b64Bytes.Length || written == 0)
				return FailTask;

			decodedBytes = decodedBytes.Slice(0, written);

			// UTF8 decode -> chars (pooled)
			int maxCharCount = Encoding.UTF8.GetMaxCharCount(decodedBytes.Length);
			rentedChars = ArrayPool<char>.Shared.Rent(maxCharCount);
			int charCount = Encoding.UTF8.GetChars(decodedBytes, rentedChars);

			var decodedSpan = rentedChars.AsSpan(0, charCount);

			// Parse "clientId:secret"
			int colon = decodedSpan.IndexOf(':');
			if (colon <= 0 || colon == decodedSpan.Length - 1)
				return FailTask;

			var clientIdSpan = decodedSpan.Slice(0, colon);
			var secretSpan = decodedSpan.Slice(colon + 1);

			// clientId must be a string for dictionary lookup
			string clientId = new string(clientIdSpan);

			if (!cache.TryGet(clientId, out var cached))
				return FailTask;

			// Compare secret without allocating a secret string
			if (!cached.Secret.AsSpan().SequenceEqual(secretSpan))
				return FailTask;

			// Return cached ticket (no claim allocations per request)
			return Task.FromResult(AuthenticateResult.Success(cached.Ticket));
		}
		finally
		{
			if (rentedChars is not null) ArrayPool<char>.Shared.Return(rentedChars);
			if (rentedDecodedBytes is not null) ArrayPool<byte>.Shared.Return(rentedDecodedBytes);
			if (rentedB64Bytes is not null) ArrayPool<byte>.Shared.Return(rentedB64Bytes);
		}
	}
}
