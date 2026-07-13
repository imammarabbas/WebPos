using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public class ProductCacheService
{
    private readonly ConcurrentDictionary<string, Product> _catalogue = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceProvider _serviceProvider;
    private int _productCount;

    public ProductCacheService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public int ProductCount => _productCount;

    public async Task LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        WebPosDbContext dbContext = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();

        List<Product> products = await dbContext.Products
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        ConcurrentDictionary<string, Product> refreshedCatalogue = new(StringComparer.OrdinalIgnoreCase);

        foreach (Product product in products)
        {
            refreshedCatalogue[$"id:{product.Id}"] = product;

            if (!string.IsNullOrWhiteSpace(product.Sku))
            {
                refreshedCatalogue[$"sku:{product.Sku}"] = product;
            }

            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                refreshedCatalogue[$"barcode:{product.Barcode}"] = product;
            }
        }

        _catalogue.Clear();

        foreach (KeyValuePair<string, Product> entry in refreshedCatalogue)
        {
            _catalogue[entry.Key] = entry.Value;
        }

        _productCount = products.Count;
    }

    public bool TryGetBySku(string sku, out Product? product)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            product = null;
            return false;
        }

        return _catalogue.TryGetValue($"sku:{sku}", out product);
    }

    public bool TryGetByBarcode(string barcode, out Product? product)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            product = null;
            return false;
        }

        return _catalogue.TryGetValue($"barcode:{barcode}", out product);
    }

    public bool TryGetById(Guid id, out Product? product)
    {
        return _catalogue.TryGetValue($"id:{id}", out product);
    }
}
