namespace WebPos.Core.Abstractions;

/// <summary>
/// Thrown when a till drawer cannot fund a transfer after the shift row is locked,
/// or when the open-shift lock cannot be acquired for the transfer.
/// </summary>
public sealed class InsufficientTillBalanceException : Exception
{
    public InsufficientTillBalanceException(string message)
        : base(message)
    {
    }

    public InsufficientTillBalanceException(
        Guid shiftId,
        long expectedCashPaisa,
        long requestedPaisa)
        : base(
            $"Insufficient till drawer cash. Expected {expectedCashPaisa} paisa, transfer {requestedPaisa}.")
    {
        ShiftId = shiftId;
        ExpectedCashPaisa = expectedCashPaisa;
        RequestedPaisa = requestedPaisa;
    }

    public InsufficientTillBalanceException(string message, Guid? shiftId)
        : base(message)
    {
        ShiftId = shiftId;
    }

    public Guid? ShiftId { get; }

    public long ExpectedCashPaisa { get; }

    public long RequestedPaisa { get; }
}
