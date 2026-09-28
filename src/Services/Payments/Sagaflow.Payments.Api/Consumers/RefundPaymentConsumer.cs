using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Payments;
using Sagaflow.Payments.Api.Data;

namespace Sagaflow.Payments.Api.Consumers;

/// <summary>
/// Compensates a charge that succeeded after the saga had already given up on it (payment timeout).
/// </summary>
public sealed class RefundPaymentConsumer(PaymentsDbContext db, TimeProvider clock, ILogger<RefundPaymentConsumer> logger)
    : IConsumer<RefundPayment>
{
    public async Task Consume(ConsumeContext<RefundPayment> context)
    {
        var command = context.Message;
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == command.OrderId, context.CancellationToken);

        if (payment?.Refund(command.Reason, clock.GetUtcNow()) == true)
        {
            logger.LogInformation("Payment {PaymentId} for order {OrderId} refunded: {Reason}", payment.Id, command.OrderId, command.Reason);
            await context.Publish(new PaymentRefunded(payment.OrderId, payment.Id));
            await db.SaveChangesAsync(context.CancellationToken);
        }
    }
}
