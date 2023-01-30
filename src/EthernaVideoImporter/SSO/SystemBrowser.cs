using IdentityModel.OidcClient.Browser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.SSO
{
    public class SystemBrowser : IBrowser
    {
        public int Port { get; }
        private readonly string path;

        public SystemBrowser(int? port = null, string? path = null)
        {
            this.path = path ?? string.Empty;

            if (!port.HasValue)
                Port = GetRandomUnusedPort();
            else
                Port = port.Value;
        }

        private int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            using var listener = new LoopbackHttpListener(Port, path);
            OpenBrowser(options.StartUrl);

            try
            {
                var result = await listener.WaitForCallbackAsync().ConfigureAwait(false);
                if (String.IsNullOrWhiteSpace(result))
                    return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "Empty response." };

                return new BrowserResult { Response = result, ResultType = BrowserResultType.Success };
            }
            catch (TaskCanceledException ex)
            {
                return new BrowserResult { ResultType = BrowserResultType.Timeout, Error = ex.Message };
            }
            catch (Exception ex)
            {
                return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
            }
        }

        public static void OpenBrowser(string? url)
        {
            url ??= string.Empty;
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&", StringComparison.InvariantCultureIgnoreCase);
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
                else
                    throw;
            }
        }
    }

    public class LoopbackHttpListener : IDisposable
    {
        const int DefaultTimeout = 60 * 5; // 5 mins (in seconds)
        private bool isDisposed;

        readonly IWebHost host;
        readonly TaskCompletionSource<string> _source = new();
        readonly string _url;

        public string Url => _url;

        // Constructors and dispose.
        public LoopbackHttpListener(int port, string? path = null)
        {
            path ??= String.Empty;
            if (path.StartsWith("/", StringComparison.InvariantCultureIgnoreCase))
                path = path[1..];

            _url = $"http://127.0.0.1:{port}/{path}";

            host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(_url)
                .Configure(Configure)
                .Build();
            host.Start();
        }

#pragma warning disable CA1063 // Causated only by the Task.Delay
        public void Dispose()
#pragma warning restore CA1063 // Implement IDisposable Correctly
        {
            Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                Dispose(true);
                GC.SuppressFinalize(this);
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            if (disposing)
                host.Dispose();

            isDisposed = true;
        }

        // Public Methods.
        void Configure(IApplicationBuilder app)
        {
            app.Run(async ctx =>
            {
                if (ctx.Request.Method == "GET")
                    await SetResultAsync(ctx.Request.QueryString.Value ?? "", ctx).ConfigureAwait(false);
                else
                    ctx.Response.StatusCode = 405;
            });
        }

        // Private Methods.
        private async Task SetResultAsync(string value, HttpContext ctx)
        {
            _source.TrySetResult(value);

            try
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("<h1>You can now return to the application.</h1>").ConfigureAwait(false);
                await ctx.Response.Body.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("<h1>Invalid request.</h1>").ConfigureAwait(false);
                await ctx.Response.Body.FlushAsync().ConfigureAwait(false); ;
            }
        }

        public Task<string> WaitForCallbackAsync(int timeoutInSeconds = DefaultTimeout)
        {
            Task.Run(async () =>
            {
                await Task.Delay(timeoutInSeconds * 1000).ConfigureAwait(false);
                _source.TrySetCanceled();
            });

            return _source.Task;
        }

    }
}
