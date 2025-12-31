using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Api.Auth;

public sealed class AclTicketCache : IAclTicketCache
{
	// Atomically replaced on change; immutable for lock-free reads.
	private ImmutableDictionary<string, IAclTicketCache.CachedClient> _map =
		ImmutableDictionary<string, IAclTicketCache.CachedClient>.Empty;

	public AclTicketCache(IOptions<AclOptions> monitor)
	{
		BuildAndSwap(monitor.Value);
	}

	public bool TryGet(string clientId, out IAclTicketCache.CachedClient cached)
		=> _map.TryGetValue(clientId, out cached);

	private void BuildAndSwap(AclOptions options)
	{
		var builder = ImmutableDictionary.CreateBuilder<string, IAclTicketCache.CachedClient>(StringComparer.Ordinal);

		foreach (var kvp in options.Clients)
		{
			var clientId = kvp.Key;
			var client = kvp.Value;

			// Build immutable claims/principal once per client
			var claims = new List<Claim>(1 + (client.Scopes?.Length ?? 0))
			{
				new(ClaimTypes.Name, clientId)
			};

			if (client.Scopes is { Length: > 0 })
			{
				foreach (var scope in client.Scopes)
				{
					if (!string.IsNullOrEmpty(scope))
						claims.Add(new Claim(Claims.Scope, scope));
				}
			}

			var identity = new ClaimsIdentity(claims, BasicAuthenticationHandler.SchemeName);
			var principal = new ClaimsPrincipal(identity);

			// Ticket is effectively immutable after construction; safe to reuse.
			var ticket = new AuthenticationTicket(principal, BasicAuthenticationHandler.SchemeName);

			builder[clientId] = new IAclTicketCache.CachedClient(client.Secret, ticket);
		}

		// Atomic swap
		_map = builder.ToImmutable();
	}
}