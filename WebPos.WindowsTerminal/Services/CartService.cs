using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class CartService
{
    private readonly List<CartLine> _lines = [];

    public event Action? Changed;

    public IReadOnlyList<CartLine> Lines => _lines;

    public long SubtotalPaisa => _lines.Sum(line => line.LineTotalPaisa);

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
            _lines.Add(new CartLine
            {
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

    public void SetUnitPrice(Guid productId, long unitPricePaisa)
    {
        CartLine? line = _lines.FirstOrDefault(entry => entry.Product.ProductId == productId);
        if (line is null)
        {
            return;
        }

        if (unitPricePaisa < 0)
        {
            return;
        }

        line.UnitPriceOverridePaisa =
            unitPricePaisa == line.Product.UnitPricePaisa ? null : unitPricePaisa;
        Changed?.Invoke();
    }

    public void SetLineTotal(Guid productId, long totalPaisa)
    {
        CartLine? line = _lines.FirstOrDefault(entry => entry.Product.ProductId == productId);
        if (line is null || line.Quantity <= 0 || totalPaisa < 0)
        {
            return;
        }

        long unitPricePaisa = (long)Math.Round(totalPaisa / line.Quantity, MidpointRounding.AwayFromZero);
        SetUnitPrice(productId, unitPricePaisa);
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
