using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.CdpUploader;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICdpUploaderClient"/>, bound to the <c>CdpUploader</c> section by default.
    /// The uploader is internal to CDP, so no egress proxy is configured. Returns the HTTP client
    /// builder so callers can add handlers, e.g. <c>.AddHeaderPropagation()</c> for the trace header.
    /// </summary>
    public static IHttpClientBuilder AddCdpUploaderClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = CdpUploaderOptions.SectionName
    )
    {
        services
            .AddOptions<CdpUploaderOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .AddHttpClient<ICdpUploaderClient, CdpUploaderClient>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<CdpUploaderOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    // A successful upload answers 302 to the initiate request's redirect; it is not followed.
                    AllowAutoRedirect = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                }
            );
    }
}
