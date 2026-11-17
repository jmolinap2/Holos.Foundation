using Abp.Application.Services;
using Abp.Dependency;
using Abp.Localization;
using Abp.UI;
using Castle.Core;
using Castle.DynamicProxy;
using Holos.Foundation.Domain;

namespace Holos.Foundation.Application;

/// <summary>Fuente de localización con la que se traducen las excepciones de negocio. La registra la aplicación.</summary>
public class LocalizableExceptionOptions
{
    public string SourceName { get; set; } = string.Empty;
}

/// <summary>
/// Convierte la clave y los argumentos de una <see cref="ILocalizableException"/> en el texto del idioma de la petición.
/// </summary>
public class LocalizableExceptionTranslator : ISingletonDependency
{
    private readonly ILocalizationManager _localization;
    private readonly LocalizableExceptionOptions _options;

    public LocalizableExceptionTranslator(ILocalizationManager localization, LocalizableExceptionOptions options)
    {
        _localization = localization;
        _options = options;
    }

    public string Translate(ILocalizableException exception)
    {
        var text = _localization.GetSource(_options.SourceName).GetString(exception.LocalizationKey);
        var arguments = exception.LocalizationArguments;
        return arguments == null || arguments.Length == 0 ? text : string.Format(text, arguments);
    }
}

/// <summary>
/// Traduce en la frontera de todo servicio de aplicación las excepciones de negocio que solo llevan una clave: quien llama
/// (API, job u otro servicio) recibe siempre una <see cref="UserFriendlyException"/> en el idioma de la petición.
/// </summary>
public class LocalizableExceptionInterceptor : AbpInterceptorBase, ITransientDependency
{
    private readonly LocalizableExceptionTranslator _translator;

    public LocalizableExceptionInterceptor(LocalizableExceptionTranslator translator) => _translator = translator;

    /// <summary>Aplica el interceptor a cada servicio de aplicación que se registre a partir de ahora.</summary>
    public static void Register(IIocManager iocManager, string localizationSourceName)
    {
        iocManager.IocContainer.Register(Castle.MicroKernel.Registration.Component
            .For<LocalizableExceptionOptions>()
            .Instance(new LocalizableExceptionOptions { SourceName = localizationSourceName }));
        // La librería no es un módulo ABP, así que su traductor no se registra por convención: se registra aquí.
        iocManager.Register<LocalizableExceptionTranslator>(DependencyLifeStyle.Singleton);
        iocManager.Register<LocalizableExceptionInterceptor>(DependencyLifeStyle.Transient);
        iocManager.Register<AbpAsyncDeterminationInterceptor<LocalizableExceptionInterceptor>>(DependencyLifeStyle.Transient);

        iocManager.IocContainer.Kernel.ComponentRegistered += (key, handler) =>
        {
            if (typeof(IApplicationService).IsAssignableFrom(handler.ComponentModel.Implementation))
            {
                handler.ComponentModel.Interceptors.Add(
                    new InterceptorReference(typeof(AbpAsyncDeterminationInterceptor<LocalizableExceptionInterceptor>)));
            }
        };
    }

    public override void InterceptSynchronous(IInvocation invocation)
    {
        try
        {
            invocation.Proceed();
        }
        catch (Exception exception) when (exception is ILocalizableException)
        {
            throw Translate(exception);
        }
    }

    protected override async Task InternalInterceptAsynchronous(IInvocation invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        try
        {
            proceedInfo.Invoke();
            await ((Task)invocation.ReturnValue!).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ILocalizableException)
        {
            throw Translate(exception);
        }
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        try
        {
            proceedInfo.Invoke();
            return await ((Task<TResult>)invocation.ReturnValue!).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ILocalizableException)
        {
            throw Translate(exception);
        }
    }

    private UserFriendlyException Translate(Exception exception) =>
        new(_translator.Translate((ILocalizableException)exception), exception);
}
