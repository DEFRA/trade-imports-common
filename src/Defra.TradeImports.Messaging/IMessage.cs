namespace Defra.TradeImports.Messaging;

/// <summary>A message published to an SNS topic.</summary>
public interface IMessage
{
    /// <summary>Sent as the SNS <c>MessageDeduplicationId</c> (FIFO topics).</summary>
    string DuplicationId { get; }

    /// <summary>
    /// Sent as the SNS <c>MessageGroupId</c> (FIFO topics); messages in one group are delivered in
    /// order. When null, the subject is used, or else a new id per message.
    /// </summary>
    string? MessageGroupId => null;
}
