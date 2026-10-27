namespace ShopFlow.Application.Abstractions;

public sealed record ChargeResult(bool Succeeded, string? ProviderReference, string? FailureReason);

public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(Guid orderId, decimal amount, string paymentToken, CancellationToken cancellationToken);
}
