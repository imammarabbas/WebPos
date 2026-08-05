using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
[TenantAuthorize]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    private readonly ICategoryService _categoryService =
        categoryService ?? throw new ArgumentNullException(nameof(categoryService));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.ListAsync(cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] UpsertCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{categoryId:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryDto>> Update(
        Guid categoryId,
        [FromBody] UpsertCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.UpdateAsync(categoryId, request, cancellationToken));
    }
}

