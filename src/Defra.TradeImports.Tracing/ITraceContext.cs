namespace Defra.TradeImports.Tracing;

public interface ITraceContext
{
    string? TraceId { get; }
}