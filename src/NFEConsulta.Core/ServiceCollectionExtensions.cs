using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NFEConsulta.Services;

namespace NFEConsulta.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSefazNFeService(
        this IServiceCollection services,
        X509Certificate2 certificado,
        TimeSpan? timeout = null,
        bool ignoreServerCertificateErrors = false)
    {
        services
            .AddHttpClient<SefazNFeService>(client =>
            {
                client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                HttpClientHandler handler = new()
                {
                    SslProtocols = SslProtocols.Tls12
                };

                if (ignoreServerCertificateErrors)
                    handler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                handler.ClientCertificates.Add(certificado);
                return handler;
            });

        return services;
    }

    public static IServiceCollection AddSefazStatusService(
        this IServiceCollection services,
        X509Certificate2 certificado,
        TimeSpan? timeout = null,
        bool ignoreServerCertificateErrors = false)
    {
        services
            .AddHttpClient<SefazStatusService>(client =>
            {
                client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                HttpClientHandler handler = new()
                {
                    SslProtocols = SslProtocols.Tls12
                };

                if (ignoreServerCertificateErrors)
                    handler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                handler.ClientCertificates.Add(certificado);
                return handler;
            });

        return services;
    }
}
