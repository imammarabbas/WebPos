using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WebPos.Client.Sdk.Security;

namespace WebPos.Client.Sdk;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IApiClient"/> using <see cref="IHttpClientFactory"/> with the
    /// <c>X-Api-Version</c> header applied to every outgoing request.
    /// </summary>
    public static IServiceCollection AddWebPosSdk(
        this IServiceCollection services,
        Action<ApiClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<ApiClientOptions> optionsBuilder = services.AddOptions<ApiClientOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        RegisterTypedClient(services);
        return services;
    }

    /// <summary>
    /// Registers the SDK and binds <see cref="ApiClientOptions"/> from the <c>WebPosSdk</c> configuration section.
    /// </summary>
    public static IServiceCollection AddWebPosSdk(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ApiClientOptions>(configuration.GetSection(ApiClientOptions.SectionName));
        RegisterTypedClient(services);
        return services;
    }

    private static void RegisterTypedClient(IServiceCollection services)
    {
        services.TryAddSingleton<
            IEnrollmentCertificateAccessor,
            EmptyEnrollmentCertificateAccessor>();
        services.AddTransient<EnrollmentAuthorizationHandler>();

        services.AddHttpClient<IApiClient, ApiClient>((serviceProvider, client) =>
            {
                ApiClientOptions options = serviceProvider
                    .GetRequiredService<IOptions<ApiClientOptions>>()
                    .Value;

                string baseAddress = string.IsNullOrWhiteSpace(options.BaseAddress)
                    ? "http://localhost:8080/"
                    : options.BaseAddress.Trim();

                if (!baseAddress.EndsWith('/'))
                {
                    baseAddress += "/";
                }

                client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);

                int timeoutSeconds = options.TimeoutSeconds <= 0 ? 15 : options.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                string apiVersion = string.IsNullOrWhiteSpace(options.ApiVersion)
                    ? "1.0.0"
                    : options.ApiVersion.Trim();

                client.DefaultRequestHeaders.Remove(ApiClient.ApiVersionHeaderName);
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    ApiClient.ApiVersionHeaderName,
                    apiVersion);
            })
            .AddHttpMessageHandler<EnrollmentAuthorizationHandler>();
    }
}
