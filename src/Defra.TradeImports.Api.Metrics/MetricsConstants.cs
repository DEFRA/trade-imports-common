namespace Defra.TradeImports.Api.Metrics;

public static class MetricsConstants
{
    public static class InstrumentNames
    {
        public const string RequestReceived = nameof(RequestReceived);
        public const string RequestFaulted = nameof(RequestFaulted);
        public const string RequestDuration = nameof(RequestDuration);
    }
    
    public static class RequestTags
    {
        public const string Service = nameof(Service);
        public const string HttpMethod = nameof(HttpMethod);
        public const string RequestPath = nameof(RequestPath);
        public const string StatusCode = nameof(StatusCode);
        public const string ExceptionType = nameof(ExceptionType);
    }
}