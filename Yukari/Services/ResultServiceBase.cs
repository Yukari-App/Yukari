using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Enums;
using Yukari.Exceptions;
using Yukari.Models.Common;
using Yukari.Services.UI;

namespace Yukari.Services;

internal abstract class ResultServiceBase
{
    private readonly ILogger _logger;
    private readonly ILocalizationService _localizationService;

    protected ResultServiceBase(ILogger logger, ILocalizationService localizationService)
    {
        _logger = logger;
        _localizationService = localizationService;
    }

    // Wraps all public operations: catches OperationCanceledException, ComicSourceDisabledException,
    // network errors, and generic exceptions — converting them into Result<T>.
    // No public method in derived services needs its own try/catch.
    protected async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> action,
        string errorTitle,
        CancellationToken ct = default
    )
    {
        try
        {
            return await action(ct);
        }
        catch (Exception ex)
        {
            var failure = HandleException(ex, errorTitle);
            return failure.Kind switch
            {
                ResultKind.Cancelled => Result<T>.Cancelled(),
                ResultKind.ComicSourceDisabled => Result<T>.ComicSourceDisabled(
                    failure.Error!,
                    failure.ErrorTitle!
                ),
                _ => Result<T>.Failure(failure.Error!, failure.ErrorTitle!),
            };
        }
    }

    protected async Task<Result> ExecuteAsync(Func<Task<Result>> action, string errorTitle)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return HandleException(ex, errorTitle);
        }
    }

    protected static bool IsNetworkError(Exception ex)
    {
        return ex is HttpRequestException
            || ex is SocketException
            || ex.InnerException is HttpRequestException;
    }

    private Result HandleException(Exception ex, string errorTitle)
    {
        switch (ex)
        {
            case OperationCanceledException:
                return Result.Cancelled();
            case ComicSourceDisabledException disabledEx:
                return Result.ComicSourceDisabled(disabledEx.Message, errorTitle);
            case var _ when IsNetworkError(ex):
                _logger.LogWarning(ex, "Network error in {Operation}", errorTitle);
                return Result.Failure(
                    _localizationService.GetString("NoInternetConnectionOrSourceIsOffline"),
                    errorTitle
                );
            default:
                _logger.LogError(
                    ex,
                    "Unhandled error in {ServiceName} — {ErrorTitle}",
                    GetType().Name,
                    errorTitle
                );
                var message = ex.InnerException?.Message ?? ex.Message;
                return Result.Failure(message, errorTitle);
        }
    }
}
