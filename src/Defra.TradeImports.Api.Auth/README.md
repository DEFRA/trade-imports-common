# Defra.TradeImports.Api.Auth

This folder provides a small, configuration-driven **Basic Authentication** and **Authorization policy** setup for an ASP.NET Core API.

It is designed for service-to-service access where clients authenticate with a `clientId:secret` pair and are authorized via simple **scope claims**.

## What’s included

- **Basic auth handler** that validates the `Authorization: Basic ...` header and authenticates requests. :contentReference[oaicite:0]{index=0}
- **Config-bound ACL options** defining allowed clients, their secrets, and scopes. :contentReference[oaicite:1]{index=1}
- **Ticket cache** that pre-builds authentication tickets (principal + claims) once per client for low-allocation request handling. 
- **Named authorization policies** (`Read`, `Write`, `Execute`) requiring specific `scope` claims. 
- Central constants for `scope` claim type and scope values. 

---

## How it works

1. The client sends an HTTP request with an `Authorization` header using the Basic scheme:
   - `Authorization: Basic base64(clientId:secret)`
2. `BasicAuthenticationHandler`:
   - Rejects requests without the header or with invalid formatting.
   - Decodes credentials.
   - Looks up the client in `IAclTicketCache`.
   - Compares the presented secret to the configured secret.
   - On success, returns a cached `AuthenticationTicket` containing:
     - `ClaimTypes.Name = clientId`
     - One `scope` claim per configured scope. 
3. Authorization policies require an authenticated user and a `scope` claim matching the required scope. 

---

## Configuration

Configuration is bound from the `Acl` section and validated using data annotations on startup. 

### Example `appsettings.json`

```json
{
  "Acl": {
    "Clients": {
      "trade-imports-client": {
        "Secret": "super-secret-value",
        "Scopes": [ "read", "write" ]
      },
      "automation-client": {
        "Secret": "another-secret",
        "Scopes": [ "read", "execute" ]
      }
    }
  }
}
```

Notes:

Clients is a dictionary keyed by clientId. 

AclOptions

Each client must have a Secret and a list of Scopes. 

AclOptions

Supported scopes are:

read

write

execute 

Scopes

Registration (DI)

Call the extension method during service registration:

builder.Services.AddAuthenticationAuthorization();


This will:

Bind and validate AclOptions from configuration.

Register IAclTicketCache as a singleton.

Register the Basic authentication scheme.

Add authorization policies for Read, Write, and Execute. 

ServiceCollectionExtensions

Using the policies in endpoints
Minimal APIs
app.MapGet("/imports", () => Results.Ok("ok"))
   .RequireAuthorization(PolicyNames.Read);

app.MapPost("/imports", () => Results.Ok("created"))
   .RequireAuthorization(PolicyNames.Write);

app.MapPost("/imports/execute", () => Results.Ok("executed"))
   .RequireAuthorization(PolicyNames.Execute);


Policy names correspond to the scope names (Read, Write, Execute) and require a scope claim with values read, write, execute.

Controllers
[Authorize(Policy = PolicyNames.Read)]
[HttpGet("imports")]
public IActionResult GetImports() => Ok();

Calling the API (client side)

To call an authenticated endpoint:

Build the credential string: clientId:secret

Base64 encode it

Send it as:

Authorization: Basic <base64(clientId:secret)>


Example (conceptual):

clientId = trade-imports-client

secret = super-secret-value

base64("trade-imports-client:super-secret-value") → put this after Basic

The handler expects the Basic prefix and the decoded form must contain a single colon separating clientId and secret. 

BasicAuthenticationHandler

Security and operational notes

Use HTTPS only. Basic authentication transmits credentials (albeit base64-encoded), so TLS is mandatory.

Rotate secrets via configuration updates and standard secret management processes.

Principals/tickets are cached per client:

Improves performance by avoiding per-request claim allocations.

Note that AclTicketCache builds its map from the current AclOptions value at construction time.

Endpoints marked with AllowAnonymous will bypass this handler’s authentication logic. 

BasicAuthenticationHandler