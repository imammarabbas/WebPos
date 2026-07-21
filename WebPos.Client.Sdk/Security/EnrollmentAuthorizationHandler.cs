using System.Net.Http.Headers;

namespace WebPos.Client.Sdk.Security;

public sealed class EnrollmentAuthorizationHandler(
    IEnrollmentCertificateAccessor certificateAccessor)
    : DelegatingHandler
{
    private readonly IEnrollmentCertificateAccessor _certificateAccessor =
        certificateAccessor
        ?? throw new ArgumentNullException(nameof(certificateAccessor));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? token = _certificateAccessor.Token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
