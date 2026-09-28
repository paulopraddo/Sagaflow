using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Payments;
using Sagaflow.Payments.Api.Data;
using Sagaflow.Payments.Api.Domain;

namespace Sagaflow.Payments.Api.Consumers;

public sealed class ProcessPaymentConsumer(
    PaymentsDbContext db,
    IPaymentGateway gateway,
    TimeProvider clock,
    ILogger<ProcessPaymentConsumer> logger) : IConsumer<ProcessPayment>
{
    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        var command = context.Message;
        var ct = context.CancellationToken;

        // Never charge the same order twice: a redelivered command replays the recorded outcome.
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == command.OrderId, ct);
        if (payment is null)
        {
            var result = await gateway.ChargeAsync(command.CustomerId, command.Amount, ct);
            payment = Payment.FromCharge(command.OrderId, command.CustomerId, command.Amount, result, clock.GetUtcNow());
            db.Payments.Add(payment);

            logger.LogInformation(
                "Payment for order {OrderId} of {Amount} {Outcome}",
                command.OrderId, command.Amount, result.Approved ? "approved" : $"declined ({result.DeclineReason})");
        }

        await (payment.Status == PaymentStatus.Failed
            ? context.Publish(new PaymentFailed(payment.OrderId, payment.Reason ?? "Declined"))
            : context.Publish(new PaymentSucceeded(payment.OrderId, payment.Id)));

        await db.SaveChangesAsync(ct);
    }
}
