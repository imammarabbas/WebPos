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
    string Phase);

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
        Phase: "selling");

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
