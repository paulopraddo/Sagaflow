using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Sagaflow.Payments.Api.Domain;

public sealed record ChargeResult(bool Approved, string? DeclineReason)
{
    public static readonly ChargeResult Success = new(true, null);
    public static ChargeResult Declined(string reason) => new(false, reason);
}

public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct);
}

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    /// <summary>Charges above this amount are declined — a deterministic way to trigger the saga's compensation.</summary>
    [Range(0, double.MaxValue)]
    public decimal DeclineAmountAbove { get; set; } = 1000m;

    /// <summary>Probability (0..1) of a random decline, to exercise failures under load.</summary>
    [Range(0, 1)]
    public double RandomDeclineRate { get; set; }

    /// <summary>Artificial latency, useful to watch a saga time out when set above its payment timeout.</summary>
    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(200);
}

/// <summary>Stands in for a real PSP. Behavior is driven entirely by <see cref="PaymentGatewayOptions"/>.</summary>
public sealed class SimulatedPaymentGateway(IOptionsMonitor<PaymentGatewayOptions> options, TimeProvider clock) : IPaymentGateway
{
    public async Task<ChargeResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct)
    {
        var settings = options.CurrentValue;
        await Task.Delay(settings.Latency, clock, ct);

        if (amount > settings.DeclineAmountAbove)
        {
            return ChargeResult.Declined($"Card declined: amount {amount:0.00} exceeds limit {settings.DeclineAmountAbove:0.00}");
        }

#pragma warning disable CA5394 // Not security sensitive: simulated failures only.
        if (settings.RandomDeclineRate > 0 && Random.Shared.NextDouble() < settings.RandomDeclineRate)
#pragma warning restore CA5394
        {
            return ChargeResult.Declined("Card declined: issuer unavailable");
        }

        return ChargeResult.Success;
    }
}
