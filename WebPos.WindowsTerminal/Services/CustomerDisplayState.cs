namespace WebPos.WindowsTerminal.Services;

public sealed record CustomerDisplayLine(
    string Description,
    decimal Quantity,
    decimal LineTotal);

public sealed record CustomerDisplaySnapshot(
    string CustomerName,
    string InvoiceNo,
    IReadOnlyList<CustomerDisplayLine> Items,
    decimal NetAmount,
    decimal Payment,
    string Phase,
    bool IsWalkIn = true,
    decimal CurrentSaleTotal = 0m,
    decimal PreviousDue = 0m,
    decimal PaymentReceived = 0m,
    decimal ChangeReturn = 0m,
    decimal OnAccount = 0m,
    decimal AccountCredit = 0m,
    decimal NewCustomerBalance = 0m);

public sealed class CustomerDisplayState
{
    private readonly object _gate = new();
    private CustomerDisplaySnapshot _current = Empty;

    public static CustomerDisplaySnapshot Empty { get; } = new(
        CustomerName: "WALK IN CUSTOMER",
        InvoiceNo: "—",
        Items: Array.Empty<CustomerDisplayLine>(),
        NetAmount: 0m,
        Payment: 0m,
        Phase: "selling",
        IsWalkIn: true);

    public event Action? Changed;

    public CustomerDisplaySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Publish(CustomerDisplaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            _current = snapshot;
        }

        Changed?.Invoke();
    }

    public void Reset() => Publish(Empty);
}
