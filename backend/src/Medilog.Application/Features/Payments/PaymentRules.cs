namespace Medilog.Application.Features.Payments;

/// <summary>
/// Feature-local constants for payment input shape. Limits that already exist
/// in the shared paging contract are reused, never redefined.
/// </summary>
public static class PaymentRules
{
    /// <summary>Matches the F2 search box limit.</summary>
    public const int SearchMaxLength = 100;

    /// <summary>Default number of payable appointments returned to the picker.</summary>
    public const int DefaultPayableLimit = 50;

    /// <summary>Maximum number of payable appointments returned to the picker.</summary>
    public const int MaxPayableLimit = 100;
}
