using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Defra.TradeImports.SQS.Endpoints.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static void MapDeadLetterQueueEndpoints(this IEndpointRouteBuilder app, string queueName, string dqlQueueName, string pattern = "admin/dlq", string? policyName = null, params string[] tags)
    {
	    var route = app.MapPost($"{pattern}/redrive", async (
		    [FromServices] ISqsDeadLetterService deadLetterService,
		    CancellationToken cancellationToken) =>
	    {
		    return Execute(async () =>
		    {
				if (
					!await deadLetterService.Redrive(
						dqlQueueName,
						queueName,
						cancellationToken
					)
				)
				{
					return Results.InternalServerError();
				}

				return Results.Accepted();
			});

			
		})
		.WithName("Redrive")
		.WithTags(tags)
			.WithSummary("Initiates redrive of messages from the dead letter queue")
			.WithDescription("Redrives all messages on the resource events dead letter queue")
			.Produces(StatusCodes.Status202Accepted)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status405MethodNotAllowed)
			.ProducesProblem(StatusCodes.Status500InternalServerError);

	    if (!string.IsNullOrEmpty(policyName))
	    {
            route.RequireAuthorization(policyName);
		}


	    route = app.MapPost($"{pattern}/remove-message", async (
			    string messageId,
			    [FromServices] ISqsDeadLetterService deadLetterService,
			    CancellationToken cancellationToken) =>
		    {
			    return Execute(async () =>
			    {
					var result = await deadLetterService.Remove(
						messageId,
						dqlQueueName,
						cancellationToken
					);

					return Results.Content(result, "text/plain; charset=utf-8");
				});
		    })
		    .WithName("RemoveMessage")
		    .WithTags(tags)
		    .WithSummary("Initiates removal of message from the dead letter queue")
		    .WithDescription(
			    "Attempts to find and remove a message on the resource events dead letter queue by message ID"
		    )
		    .Produces(StatusCodes.Status200OK)
		    .ProducesProblem(StatusCodes.Status401Unauthorized)
		    .ProducesProblem(StatusCodes.Status403Forbidden)
		    .ProducesProblem(StatusCodes.Status405MethodNotAllowed)
		    .ProducesProblem(StatusCodes.Status500InternalServerError);

	    if (!string.IsNullOrEmpty(policyName))
	    {
		    route.RequireAuthorization(policyName);
	    }

	    route = app.MapPost($"{pattern}/drain", async (
			    [FromServices] ISqsDeadLetterService deadLetterService,
			    CancellationToken cancellationToken) =>
		    {
			    return Execute(async () =>
			    {
				    if (!await deadLetterService.Drain(dqlQueueName, cancellationToken))
				    {
					    return Results.InternalServerError();
				    }

				    return Results.Ok();
			    });
		    })
		    .WithName("Drain")
		    .WithTags(tags)
		    .WithSummary("Initiates drain of all messages from the dead letter queue")
		    .WithDescription("Drains all messages on the resource events dead letter queue")
		    .Produces(StatusCodes.Status200OK)
		    .ProducesProblem(StatusCodes.Status401Unauthorized)
		    .ProducesProblem(StatusCodes.Status403Forbidden)
		    .ProducesProblem(StatusCodes.Status405MethodNotAllowed)
		    .ProducesProblem(StatusCodes.Status500InternalServerError);

	    if (!string.IsNullOrEmpty(policyName))
	    {
		    route.RequireAuthorization(policyName);
	    }

	    route = app.MapGet($"{pattern}/count", async (
			    [FromServices] ISqsDeadLetterService deadLetterService,
			    CancellationToken cancellationToken) =>
		    {
			    return Execute(async () =>
			    {
					var deadLetterQueueCount = await deadLetterService.GetCount(dqlQueueName, cancellationToken);
					return Results.Ok(new { DeadLetterQueueCount = deadLetterQueueCount });
				});
		    })
			.WithName("Count")
		    .WithTags("Admin")
		    .WithSummary("Gets the count of messages on the resource events dead letter queue")
		    .WithDescription("Gets the count of messages on the resource events dead letter queue")
		    .Produces(StatusCodes.Status200OK)
		    .ProducesProblem(StatusCodes.Status401Unauthorized)
		    .ProducesProblem(StatusCodes.Status403Forbidden)
		    .ProducesProblem(StatusCodes.Status500InternalServerError);

	    if (!string.IsNullOrEmpty(policyName))
	    {
		    route.RequireAuthorization(policyName);
	    }
	}


    private static Task<IResult> Execute(Func<Task<IResult>> func)
    {
	    try
	    {
		   return func();
	    }
	    catch (Exception)
	    {
		    return Task.FromResult(Results.InternalServerError());
	    }
	}
}
