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
        try
        {
            RecordMilkCollectionResult result =
                await _procurementService.RecordMilkCollectionAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InsufficientCashBalanceException ex)
        {
            return BadRequest(new { error = ex.Message, code = "INSUFFICIENT_FUNDS" });
        }
    }

    [HttpPost("supplier-payment")]
    [ProducesResponseType(typeof(RecordSupplierPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordSupplierPaymentResult>> RecordSupplierPayment(
        [FromBody] RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            RecordSupplierPaymentResult result =
                await _procurementService.RecordSupplierPaymentAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InsufficientCashBalanceException ex)
        {
            return BadRequest(new { error = ex.Message, code = "INSUFFICIENT_FUNDS" });
        }
    }

    [HttpPost("customer-payment")]
    [ProducesResponseType(typeof(RecordCustomerPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordCustomerPaymentResult>> RecordCustomerPayment(
        [FromBody] RecordCustomerPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            RecordCustomerPaymentResult result =
                await _procurementService.RecordCustomerPaymentAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InsufficientCashBalanceException ex)
        {
            return BadRequest(new { error = ex.Message, code = "INSUFFICIENT_FUNDS" });
        }
    }
}
