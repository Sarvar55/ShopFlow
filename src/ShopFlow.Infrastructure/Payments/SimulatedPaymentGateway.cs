using ShopFlow.Application.Abstractions;

namespace ShopFlow.Infrastructure.Payments;

/// <summary>
/// Stand-in for a real payment provider (Stripe, PayPal, a bank API...).
/// The token "tok_declined" simulates a declined card; any other token succeeds.
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public const string DeclinedToken = "tok_declined";

    public Task<ChargeResult> ChargeAsync(Guid orderId, decimal amount, string paymentToken, CancellationToken cancellationToken)
    {
        var result = paymentToken == DeclinedToken
            ? new ChargeResult(false, null, "Card declined by issuer.")
            : new ChargeResult(true, $"pay_{Guid.NewGuid():N}", null);
        return Task.FromResult(result);
    }
}
