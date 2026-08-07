using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Security;

namespace WebPos.Controllers;

[ApiController]
[Route("api/terminal-enrollment")]
public sealed class TerminalEnrollmentController(
    EnrollmentService enrollmentService,
    IKeyProvider keyProvider) : ControllerBase
{
    private readonly EnrollmentService _enrollmentService =
        enrollmentService
        ?? throw new ArgumentNullException(nameof(enrollmentService));
    private readonly IKeyProvider _keyProvider =
        keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));

    /// <summary>
    /// Anonymous: Terminals pull the API's current enrollment public key so restore/Docker
    /// machines do not need a pre-synced appsettings.json.
    /// </summary>
    [HttpGet("public-key")]
    [ProducesResponseType(typeof(EnrollmentPublicKeyDto), StatusCodes.Status200OK)]
    public ActionResult<EnrollmentPublicKeyDto> GetPublicKey()
    {
        return Ok(new EnrollmentPublicKeyDto
        {
            PublicKeyPem = _keyProvider.GetPublicKeyPem()
        });
    }

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
        (EnrollmentCertificateDto? certificate, string? failureReason) =
            await _enrollmentService.TryEnrollAsync(request, cancellationToken);

        return certificate is null
            ? Problem(
                detail: failureReason ?? "Terminal enrollment failed.",
                statusCode: StatusCodes.Status401Unauthorized)
            : Ok(certificate);
    }
}
