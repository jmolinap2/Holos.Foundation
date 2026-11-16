using Abp.Logging;

namespace Holos.Foundation.Abp.Domain;

/// <summary>
/// Regla de negocio incumplida expresada con una clave de localización y argumentos opcionales ({0}, {1}...).
/// Se traduce en el borde del servicio de aplicación (ver <c>LocalizableExceptionInterceptor</c>), así el dominio no
/// conoce idiomas. Se registra como advertencia: es una respuesta esperada al usuario, no una falla del sistema.
/// </summary>
public class LocalizableDomainException : Exception, ILocalizableException, IHasLogSeverity
{
    public LocalizableDomainException(string localizationKey, params object[] arguments) : base(localizationKey)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            throw new ArgumentException("La clave de localización es obligatoria.", nameof(localizationKey));

        LocalizationKey = localizationKey;
        LocalizationArguments = arguments ?? Array.Empty<object>();
    }

    public LogSeverity Severity { get; set; } = LogSeverity.Warn;
    public string LocalizationKey { get; }
    public object[] LocalizationArguments { get; }
}
