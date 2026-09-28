namespace Sagaflow.Contracts;

/// <summary>
/// Queue names for commands. Commands are sent to exactly one owner, so the sender
/// needs a well-known address; events are published and routed by message type instead.
/// </summary>
public static class Queues
{
    public const string ReserveStock = "inventory-reserve-stock";
    public const string ReleaseStock = "inventory-release-stock";
    public const string ProcessPayment = "payments-process-payment";
    public const string RefundPayment = "payments-refund-payment";

    public static Uri Address(string queue) => new($"queue:{queue}");
}
