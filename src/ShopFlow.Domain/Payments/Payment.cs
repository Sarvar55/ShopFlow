using ShopFlow.Domain.Common;

namespace ShopFlow.Domain.Payments;

public enum PaymentStatus
{
    Succeeded = 0,
    Declined = 1
}

public sealed class Payment : Entity, IAuditable
{
    private Payment() { } // EF Core

    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Payment Succeeded(Guid orderId, decimal amount, string providerReference, DateTimeOffset at) =>
        new() { OrderId = orderId, Amount = amount, Status = PaymentStatus.Succeeded, ProviderReference = providerReference, CreatedAt = at };

    public static Payment Declined(Guid orderId, decimal amount, string reason, DateTimeOffset at) =>
        new() { OrderId = orderId, Amount = amount, Status = PaymentStatus.Declined, FailureReason = reason, CreatedAt = at };
}
