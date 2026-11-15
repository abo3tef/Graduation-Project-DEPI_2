namespace Medilog.Domain.Enums;

/// <summary>
/// Lifecycle of a payment (Q-21). Only a <see cref="Paid"/> payment counts
/// towards revenue; <see cref="Paid"/> is final.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Partial,
    Paid
}
