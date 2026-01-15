using Microsoft.AspNetCore.Authentication;

namespace Defra.TradeImports.Api.Auth;

public interface IAclTicketCache
{
	bool TryGet(string clientId, out CachedClient cached);

	public readonly record struct CachedClient(string Secret, AuthenticationTicket Ticket);
}