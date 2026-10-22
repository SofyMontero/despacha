using Despacha.Api.Shared.Logging;

namespace Despacha.Api.Integrations.WhatsApp;

public interface IWhatsAppSender
{
    Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneE164,
        string template,
        IReadOnlyList<string> parameters,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppSendResult(bool Sent, string Status);

/// <summary>Único emisor registrado hasta la Fase 6: no envía nada a Meta.</summary>
internal sealed class DisabledWhatsAppSender(ILogger<DisabledWhatsAppSender> logger) : IWhatsAppSender
{
    public Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneE164,
        string template,
        IReadOnlyList<string> parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "WhatsApp deshabilitado: plantilla {Template} para {Phone} no enviada",
            template,
            LogMasking.MaskPhone(phoneE164));

        return Task.FromResult(new WhatsAppSendResult(false, "disabled"));
    }
}

public static class IntegrationsServiceCollectionExtensions
{
    public static IServiceCollection AddDespachaIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IWhatsAppSender, DisabledWhatsAppSender>();
        return services;
    }
}
