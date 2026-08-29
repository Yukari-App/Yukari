using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Yukari.Core.Sources.Browser;
using Yukari.Helpers;

namespace Yukari.Services.Sources;

public sealed class BrowserService : IBrowserService
{
    private readonly ILogger<BrowserService> _logger;

    private readonly string WebView2CacheFolder = Path.Join(
        AppDataHelper.GetAppDataPath(),
        "WebView2Cache"
    );
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private WebView2? _webView;
    private CoreWebView2? _coreWebView;
    private IBrowserHost? _host;

    private CancellationTokenSource? _interactionCts;

    public BrowserService(ILogger<BrowserService> logger)
    {
        _logger = logger;
    }

    public void SetHost(IBrowserHost host) => _host = host;

    public async Task<BrowserSession> ResolveChallengeAsync(
        string url,
        TimeSpan? timeout = null,
        string? challengeDetectionScript = null,
        TimeSpan? autoResolveTimeout = null,
        CancellationToken ct = default
    )
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);
        var effectiveAutoResolve = autoResolveTimeout ?? TimeSpan.FromSeconds(10);

        var browserShown = false;

        await _semaphore.WaitAsync(ct);
        try
        {
            _logger.LogDebug(
                "Resolving challenge for {Url} (autoResolve: {AutoResolve}s, total: {Timeout}s)",
                url,
                effectiveAutoResolve.TotalSeconds,
                effectiveTimeout.TotalSeconds
            );

            await EnsureInitializedAsync();
            await NavigateAndWaitAsync(url, ct);

            var stopwatch = Stopwatch.StartNew();

            var resolved = await WaitForChallengeResolutionAsync(
                effectiveAutoResolve,
                challengeDetectionScript,
                ct
            );

            if (!resolved && stopwatch.Elapsed < effectiveTimeout)
            {
                _logger.LogInformation("Challenge not auto-resolved, requesting user interaction");

                _interactionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                if (_host != null)
                {
                    _host.UserCancelledInteraction += OnUserCancelledInteraction;
                    _host.ShowBrowser();
                }
                browserShown = true;

                var remainingTime = effectiveTimeout - stopwatch.Elapsed;
                resolved = await WaitForChallengeResolutionAsync(
                    remainingTime,
                    challengeDetectionScript,
                    _interactionCts.Token
                );
            }

            if (!resolved)
                throw new TimeoutException("The time to solve the challenge has run out.");

            _logger.LogInformation("Challenge resolved successfully for {Url}", url);

            var cookies = await GetCookiesAsync(ct);
            var userAgent = await GetUserAgentAsync();
            return new BrowserSession(cookies, userAgent);
        }
        finally
        {
            if (browserShown && _host != null)
            {
                _host.UserCancelledInteraction -= OnUserCancelledInteraction;
                _host.HideBrowser();
            }
            _interactionCts?.Dispose();
            _interactionCts = null;

            await ClearWebViewAsync();

            _semaphore.Release();
        }
    }

    public async Task<string?> EvaluateScriptAsync(
        string url,
        string script,
        CancellationToken ct = default
    )
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await EnsureInitializedAsync();

            if (_coreWebView!.Source != url)
                await NavigateAndWaitAsync(url, ct);

            var result = await _coreWebView.ExecuteScriptAsync(script);
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing BrowserService");

        _interactionCts?.Cancel();
        await _semaphore.WaitAsync();
        try
        {
            if (_webView != null)
            {
                _host?.DetachBrowser(_webView);
                _webView.Close();
                _webView = null;
            }
            _coreWebView = null;
        }
        finally
        {
            _semaphore.Release();
            _semaphore.Dispose();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_webView != null)
            return;

        if (_host == null)
            throw new InvalidOperationException(
                "BrowserService requires a host. Call SetHost() before using."
            );

        try
        {
            _logger.LogDebug(
                "Initializing WebView2 with cache at {CacheFolder}",
                WebView2CacheFolder
            );

            _webView = new WebView2();
            _host.AttachBrowser(_webView);

            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                null,
                WebView2CacheFolder,
                null
            );

            await _webView.EnsureCoreWebView2Async(env);
            _coreWebView = _webView.CoreWebView2;

            _coreWebView.Settings.IsScriptEnabled = true;
            _coreWebView.Settings.AreDefaultScriptDialogsEnabled = false;

            _logger.LogDebug("WebView2 initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2");
            throw new InvalidOperationException(
                "Failed to initialize WebView2. Ensure WebView2 Runtime is installed.",
                ex
            );
        }
    }

    private async Task NavigateAndWaitAsync(string url, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess || e.WebErrorStatus == CoreWebView2WebErrorStatus.Unknown)
                tcs.TrySetResult(true);
            else if (e.WebErrorStatus != CoreWebView2WebErrorStatus.ConnectionAborted)
                tcs.TrySetException(new Exception($"Navigation failed: {e.WebErrorStatus}"));
        }

        _coreWebView!.NavigationCompleted += NavigationCompleted;
        try
        {
            _coreWebView.Navigate(url);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            await using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task;
            }
        }
        finally
        {
            _coreWebView.NavigationCompleted -= NavigationCompleted;
        }
    }

    private async Task<bool> WaitForChallengeResolutionAsync(
        TimeSpan timeout,
        string? challengeDetectionScript,
        CancellationToken ct
    )
    {
        if (!string.IsNullOrEmpty(challengeDetectionScript))
        {
            _logger.LogDebug("Using custom challenge detection script");

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                ct.ThrowIfCancellationRequested();

                var result = await _coreWebView!.ExecuteScriptAsync(challengeDetectionScript);
                if (result?.Trim('"').ToLowerInvariant() == "true")
                {
                    _logger.LogDebug(
                        "Challenge detected as resolved after {Elapsed}ms",
                        stopwatch.ElapsedMilliseconds
                    );
                    return true;
                }

                await Task.Delay(500, ct);
            }
            return false;
        }

        _logger.LogDebug("Using fallback navigation-based challenge detection");

        // Fallback
        var settledTcs = new TaskCompletionSource<bool>();
        using var settleTimer = new Timer(
            _ => settledTcs.TrySetResult(true),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        );

        void OnNavigationStarting(object? s, CoreWebView2NavigationStartingEventArgs e) =>
            settleTimer.Change(Timeout.Infinite, Timeout.Infinite);

        void OnNavigationCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) =>
            settleTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);

        _coreWebView!.NavigationStarting += OnNavigationStarting;
        _coreWebView.NavigationCompleted += OnNavigationCompleted;

        settleTimer.Change(TimeSpan.FromSeconds(4), Timeout.InfiniteTimeSpan);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await using var _ = timeoutCts.Token.Register(() => settledTcs.TrySetResult(false));

            return await settledTcs.Task;
        }
        finally
        {
            _coreWebView.NavigationStarting -= OnNavigationStarting;
            _coreWebView.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    private async Task ClearWebViewAsync()
    {
        if (_coreWebView == null)
            return;

        try
        {
            var tcs = new TaskCompletionSource<bool>();
            void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) =>
                tcs.TrySetResult(true);

            _coreWebView.NavigationCompleted += OnCompleted;
            try
            {
                _coreWebView.Navigate("about:blank");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using (cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    await tcs.Task;
                }
            }
            finally
            {
                _coreWebView.NavigationCompleted -= OnCompleted;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear WebView");
        }
    }

    private void OnUserCancelledInteraction()
    {
        _logger.LogInformation("User cancelled browser interaction");
        _interactionCts?.Cancel();
    }

    private async Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(CancellationToken ct)
    {
        var cookies = new Dictionary<string, string>();
        if (_coreWebView == null)
            return cookies;

        var manager = _coreWebView.CookieManager;
        var cookieList = await manager.GetCookiesAsync(_coreWebView.Source);
        foreach (var cookie in cookieList)
            cookies[cookie.Name] = cookie.Value;
        return cookies;
    }

    private async Task<string> GetUserAgentAsync()
    {
        if (_coreWebView == null)
            return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0";

        var result = await _coreWebView.ExecuteScriptAsync("navigator.userAgent");
        return result.Trim('"');
    }
}
