using System.Security.Claims;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

public sealed class HttpContextTenantService(
    IHttpContextAccessor httpContextAccessor,
    ICircuitTenantContext circuitTenantContext) : ITenantService
{
    public const string TenantClaimType = "tenant_id";
    public const string JwtTenantClaimType = "tid";

    private readonly IHttpContextAccessor _httpContextAccessor =
        httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    private readonly ICircuitTenantContext _circuitTenantContext =
        circuitTenantContext ?? throw new ArgumentNullException(nameof(circuitTenantContext));

    public Guid TenantId =>
        TryResolveTenantId(out Guid tenantId) ? tenantId : Guid.Empty;

    public bool IsResolved => TryResolveTenantId(out _);

    private bool TryResolveTenantId(out Guid tenantId)
    {
        if (_circuitTenantContext.IsResolved && _circuitTenantContext.TenantId is Guid circuitId)
        {
            tenantId = circuitId;
            return true;
        }

        ClaimsIdentity? identity = _httpContextAccessor.HttpContext?.User.Identities
            .FirstOrDefault(candidate =>
                candidate.IsAuthenticated
                && string.Equals(
                    candidate.AuthenticationType,
                        EnrollmentCertificateValidator.AuthenticationType,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.FindFirst(
                        EnrollmentCertificateValidator.CertificateTypeClaim)?.Value,
                    EnrollmentCertificateValidator.EnrollmentCertificateType,
                    StringComparison.Ordinal));
        Claim? claim = identity?.FindFirst(TenantClaimType);
        if (Guid.TryParse(claim?.Value, out tenantId) && tenantId != Guid.Empty)
        {
            return true;
        }

        Claim? jwtClaim = _httpContextAccessor.HttpContext?.User.Identities
            .Where(candidate => candidate.IsAuthenticated)
            .Select(candidate =>
                candidate.FindFirst(JwtTenantClaimType)
                ?? candidate.FindFirst(TenantClaimType))
            .FirstOrDefault(candidate => candidate is not null);
        return Guid.TryParse(jwtClaim?.Value, out tenantId)
            && tenantId != Guid.Empty;
    }
}

