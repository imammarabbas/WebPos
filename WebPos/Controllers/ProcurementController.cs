using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/procurement")]
[Authorize]
[TenantAuthorize]
public sealed class ProcurementController(IProcurementService procurementService) : ControllerBase
{
    private readonly IProcurementService _procurementService =
        procurementService ?? throw new ArgumentNullException(nameof(procurementService));

    [HttpPost("milk-collection")]
    [ProducesResponseType(typeof(RecordMilkCollectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordMilkCollectionResult>> RecordMilkCollection(
        [FromBody] RecordMilkCollectionRequest request,
        CancellationToken cancellationToken)
    {
        RecordMilkCollectionResult result =
            await _procurementService.RecordMilkCollectionAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("supplier-payment")]
    [ProducesResponseType(typeof(RecordSupplierPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordSupplierPaymentResult>> RecordSupplierPayment(
        [FromBody] RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken)
    {
        RecordSupplierPaymentResult result =
            await _procurementService.RecordSupplierPaymentAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("customer-payment")]
    [ProducesResponseType(typeof(RecordCustomerPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordCustomerPaymentResult>> RecordCustomerPayment(
        [FromBody] RecordCustomerPaymentRequest request,
        CancellationToken cancellationToken)
    {
        RecordCustomerPaymentResult result =
            await _procurementService.RecordCustomerPaymentAsync(request, cancellationToken);
        return Ok(result);
    }
}
