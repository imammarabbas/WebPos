using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Product catalog list/create/update for the Windows terminal.</summary>
public sealed class ProductAdminApiClient(IApiClient apiClient, ILogger<ProductAdminApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<ProductAdminApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<IReadOnlyList<ProductDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<ProductDto> products =
                await _apiClient.GetProductsAsync(cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<ProductDto>>.Ok(products);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to list products ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<IReadOnlyList<ProductDto>>.Fail(MapAuthError(ex));
        }
    }

    public async Task<Result<PagedProductResult>> SearchAsync(
        string? search = null,
        int page = 1,
        int pageSize = 50,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PagedProductResult result = await _apiClient
                .SearchProductsAsync(search, page, pageSize, lowStockOnly, cancellationToken)
                .ConfigureAwait(false);
            return Result<PagedProductResult>.Ok(result);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to search products ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<PagedProductResult>.Fail(MapAuthError(ex));
        }
    }

    public async Task<Result<IReadOnlyList<ProductDto>>> ListBulkParentsAsync(
        string? search = null,
        int take = 50,
        Guid? includeId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<ProductDto> parents = await _apiClient
                .GetBulkParentsAsync(search, take, includeId, cancellationToken)
                .ConfigureAwait(false);
            return Result<IReadOnlyList<ProductDto>>.Ok(parents);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to list bulk parents ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<IReadOnlyList<ProductDto>>.Fail(MapAuthError(ex));
        }
    }

    public async Task<Result<ProductDto>> GetAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ProductDto product =
                await _apiClient.GetProductAsync(productId, cancellationToken).ConfigureAwait(false);
            return Result<ProductDto>.Ok(product);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to get product ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<ProductDto>.Fail(MapAuthError(ex));
        }
    }

    public async Task<Result<ProductDto>> CreateAsync(
        UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ProductDto created =
                await _apiClient.CreateProductAsync(request, cancellationToken).ConfigureAwait(false);
            return Result<ProductDto>.Ok(created);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to create product ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<ProductDto>.Fail(MapAuthError(ex));
        }
    }

    public async Task<Result<ProductDto>> UpdateAsync(
        Guid productId,
        UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ProductDto updated =
                await _apiClient.UpdateProductAsync(productId, request, cancellationToken)
                    .ConfigureAwait(false);
            return Result<ProductDto>.Ok(updated);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to update product ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<ProductDto>.Fail(MapAuthError(ex));
        }
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<CategoryDto> categories =
                await _apiClient.GetCategoriesAsync(cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<CategoryDto>>.Ok(categories);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to list categories ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            return Result<IReadOnlyList<CategoryDto>>.Fail(MapAuthError(ex));
        }
    }

    private static string MapAuthError(WebPosClientException ex)
    {
        if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return "Owner or Manager role required for product catalog.";
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? "Product API failed." : ex.Message;
    }
}
