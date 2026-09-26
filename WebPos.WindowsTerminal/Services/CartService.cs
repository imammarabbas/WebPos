using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class CartService
{
    private readonly List<CartLine> _lines = [];

    public event Action? Changed;

    public const int MaxLines = 24;

    public IReadOnlyList<CartLine> Lines => _lines;

    public long SubtotalPaisa => _lines.Sum(line => line.LineTotalPaisa);

    public decimal TotalQuantity => _lines.Sum(line => line.Quantity);

    public bool IsEmpty => _lines.Count == 0;

    public void Add(SalesProductDto product, decimal quantity = 1m)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity <= 0)
        {
            return;
        }

        CartLine? existing = _lines.FirstOrDefault(
            line => line.Product.ProductId == product.ProductId);

        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            if (_lines.Count >= MaxLines)
            {
                return;
            }

            _lines.Add(new CartLine
            {
                LineId = Guid.NewGuid(),
                Product = product,
                Quantity = quantity
            });
        }

        Changed?.Invoke();
    }

    public void SetQuantity(Guid productId, decimal quantity)
    {
        CartLine? line = _lines.FirstOrDefault(entry => entry.Product.ProductId == productId);
        if (line is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            _lines.Remove(line);
        }
        else
        {
            line.Quantity = quantity;
        }

        Changed?.Invoke();
    }

    public bool SetUnitPrice(Guid productId, long unitPricePaisa)
    {
        CartLine? line = _lines.FirstOrDefault(entry => entry.Product.ProductId == productId);
        if (line is null)
        {
            return false;
        }

        if (unitPricePaisa < 0)
        {
            return false;
        }

        line.UnitPriceOverridePaisa =
            unitPricePaisa == line.Product.UnitPricePaisa ? null : unitPricePaisa;
        Changed?.Invoke();
        return true;
    }

    public bool SetLineTotal(Guid productId, long totalPaisa)
    {
        CartLine? line = _lines.FirstOrDefault(entry => entry.Product.ProductId == productId);
        if (line is null || line.Quantity <= 0 || totalPaisa < 0)
        {
            return false;
        }

        long unitPricePaisa = (long)Math.Round(totalPaisa / line.Quantity, MidpointRounding.AwayFromZero);
        return SetUnitPrice(productId, unitPricePaisa);
    }

    public void Remove(Guid productId)
    {
        CartLine? line = _lines.FirstOrDefault(entry => entry.Product.ProductId == productId);
        if (line is null)
        {
            return;
        }

        _lines.Remove(line);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_lines.Count == 0)
        {
            return;
        }

        _lines.Clear();
        Changed?.Invoke();
    }
}
