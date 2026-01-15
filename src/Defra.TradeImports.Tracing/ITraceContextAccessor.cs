namespace Defra.TradeImports.Tracing;

public interface ITraceContextAccessor
{
    /// <summary>
    /// Gets or sets the current context.
    /// </summary>
    ITraceContext? Context { get; set; }
}
