using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebPos.Core.Security;

namespace WebPos.Controllers;

[ApiController]
[Route("api/terminal-enrollment")]
public sealed class TerminalEnrollmentController(
    EnrollmentService enrollmentService) : ControllerBase
{
    private readonly EnrollmentService _enrollmentService =
        enrollmentService
        ?? throw new ArgumentNullException(nameof(enrollmentService));

    [HttpPost]
    [EnableRateLimiting("terminal-enrollment")]
    [ProducesResponseType(
        typeof(EnrollmentCertificateDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EnrollmentCertificateDto>> Enroll(
        [FromBody] EnrollTerminalRequest request,
        CancellationToken cancellationToken)
    {
        EnrollmentCertificateDto? certificate =
            await _enrollmentService.EnrollAsync(request, cancellationToken);

        return certificate is null
            ? Problem(
                detail: "Terminal enrollment failed.",
                statusCode: StatusCodes.Status401Unauthorized)
            : Ok(certificate);
    }
}
