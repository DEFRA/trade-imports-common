using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Defra.TradeImports.Messaging;

/// <summary>
/// Local AWS emulation (floci, see https://floci.io) settings, bound from the configuration root.
/// When <see cref="UseFloci"/> is not true the AWS SDK's default credential and endpoint resolution
/// is used, as it is when deployed to CDP.
/// </summary>
[ExcludeFromCodeCoverage]
public class FlociOptions
{
    [ConfigurationKeyName("AWS_ACCESS_KEY_ID")]
    public string? AccessKeyId { get; init; }

    [ConfigurationKeyName("AWS_REGION")]
    public string? AwsRegion { get; init; }

    [ConfigurationKeyName("AWS_SECRET_ACCESS_KEY")]
    public string? SecretAccessKey { get; init; }

    [ConfigurationKeyName("SNS_ENDPOINT")]
    public string? SnsEndpoint { get; init; }

    [ConfigurationKeyName("SQS_ENDPOINT")]
    public string? SqsEndpoint { get; init; }

    [ConfigurationKeyName("S3_ENDPOINT")]
    public string? S3Endpoint { get; init; }

    [ConfigurationKeyName("USE_FLOCI")]
    public bool? UseFloci { get; init; } = false;

    /// <summary>The region, defaulting to eu-west-2.</summary>
    public string Region => string.IsNullOrWhiteSpace(AwsRegion) ? "eu-west-2" : AwsRegion;

    /// <summary>floci accepts any credentials; "test" is used when none are configured.</summary>
    public Amazon.Runtime.BasicAWSCredentials Credentials() =>
        new(AccessKeyId ?? "test", SecretAccessKey ?? "test");
}
