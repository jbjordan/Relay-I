using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("RelayServer.Tests")]
namespace Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay;
    using System.Net;
    using System.Text;
    using System.Collections.Generic;
    using System.Text.Json;

    public class Program
    {
        private const string RelayConnString = "relay_conn_string";

        // Route table mapping absolute path -> handler function (GET routes)
        private static readonly Dictionary<string, Action<RelayedHttpListenerContext>> Routes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["/" + "hc-noauth" + "/hello"] = HandleHello,
            ["/" + "hc-noauth" + "/health"] = HandleHealth,
            ["/" + "hc-noauth" + "/gameinfo"] = HandleGameInfo,
        };

        public static void Main(string[] args)
        {
            // Catch otherwise unhandled exceptions to prevent process crash.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Logger.Log("FATAL", $"Unhandled: {e.ExceptionObject}", isError: true);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Logger.Log("FATAL", $"Unobserved task exception: {e.Exception}", e.Exception, isError: true);
                e.SetObserved();
            };

            try
            {
                RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Log("FATAL", "RunAsync crashed", ex, isError: true);
            }
        }

        private static async Task RunAsync()
        {
            // Prefer environment variable for secret instead of in-source constant.
            var connectionString = Environment.GetEnvironmentVariable(RelayConnString);

            var listener = new HybridConnectionListener(connectionString);

            // Subscribe to the status events.
            listener.Connecting += (o, e) => { Console.WriteLine("Connecting"); };
            listener.Offline += (o, e) => { Console.WriteLine("Offline"); };
            listener.Online += (o, e) => { Console.WriteLine("Online"); };

            // Provide an HTTP request handler with simple path routing and exception safety
            listener.RequestHandler = (context) =>
            {
                try
                {
                    var path = context.Request.Url.AbsolutePath.TrimEnd('/');
                    if (path.Length == 0) path = "/"; // normalize root
                    var method = context.Request.HttpMethod?.ToUpperInvariant();
                    //Logger.Log("INFO", $"Method : {method} and Path : {path}");

                    // OPTIONs method handling for CORS preflight in browsers
                    if (method != "GET" && method != "POST" && method != "OPTIONS")
                    {
                        context.Response.StatusCode = HttpStatusCode.MethodNotAllowed;
                        context.Response.StatusDescription = "Method Not Allowed";
                        WriteHtml(context, "<h1>405 Method Not Allowed</h1>");
                        return;
                    }

                    if (Routes.TryGetValue(path, out var handler))
                    {
                        try
                        {
                            handler(context); // handler is responsible for closing
                        }
                        catch (Exception routeEx)
                        {
                            Logger.Log("ERROR", $"Handler failure for path '{path}'", routeEx, isError: true);
                            WriteError(context, HttpStatusCode.InternalServerError, "Route handler error");
                        }
                        return;
                    }

                    // 404 if no handler found
                    Console.WriteLine($"Not Found: {path}");
                    context.Response.StatusCode = HttpStatusCode.NotFound;
                    context.Response.StatusDescription = "Not Found";
                    WriteHtml(context, "<h1>404 Not Found</h1>");
                }
                catch (Exception ex)
                {
                    Logger.Log("ERROR", "Request processing failure", ex, isError: true);
                    WriteError(context, HttpStatusCode.InternalServerError, "Unhandled server error");
                }
            };

            try
            {
                await listener.OpenAsync();
                Console.WriteLine("Server listening");
            }
            catch (Exception openEx)
            {
                Logger.Log("ERROR", "Failed to open listener", openEx, isError: true);
                return; // Cannot continue
            }

            try
            {
                await Console.In.ReadLineAsync();
            }
            catch (Exception readEx)
            {
                Logger.Log("WARN", "Console read failed", readEx);
            }

            try
            {
                await listener.CloseAsync();
            }
            catch (Exception closeEx)
            {
                Logger.Log("WARN", "Error closing listener", closeEx);
            }
        }

        // Individual route handlers -----------------------
        private static void HandleHello(RelayedHttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.OK;
            context.Response.StatusDescription = "OK";
            WriteHtml(context, "<h1>Hello World!</h1>");
        }

        private static void HandleHealth(RelayedHttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.OK;
            context.Response.StatusDescription = "OK";
            Logger.Log("INFO", "Health check");
            WriteHtml(context, "<h1>Health Check: OK</h1>");
        }

        private static void HandleGameInfo(RelayedHttpListenerContext context)
        {
            try
            {
                // Read body if present
                string bodyText = string.Empty;
                if (context.Request.HasEntityBody && context.Request.InputStream != null)
                {
                    using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
                    bodyText = reader.ReadToEnd();
                }

                // Log request details
                if (!string.IsNullOrEmpty(bodyText))
                {
                    Logger.Log("INFO", $"{bodyText}");
                }
                else
                {
                    Logger.Log("INFO", "No request body");
                }

                // Optionally parse JSON
                object? parsed = null;
                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    try
                    {
                        parsed = JsonSerializer.Deserialize<object>(bodyText);
                    }
                    catch (Exception jsonEx)
                    {
                        Logger.Log("WARN", $"Invalid JSON body: {jsonEx.Message}");
                    }
                }

                // Respond
                context.Response.StatusCode = HttpStatusCode.OK;
                context.Response.StatusDescription = "OK";
                var innerHtml = new StringBuilder()
                    .Append("<h1>Game Info</h1>")
                    .Append("<p>Body received:</p>")
                    .Append("<pre>")
                    .Append(WebUtility.HtmlEncode(string.IsNullOrEmpty(bodyText) ? "(none)" : bodyText))
                    .Append("</pre>")
                    .ToString();

                WriteHtml(context, innerHtml);
            }
            catch (Exception ex)
            {
                Logger.Log("ERROR", "HandleGameInfo failure", ex, isError: true);
                WriteError(context, HttpStatusCode.InternalServerError, "Failed to process GameInfo");
            }
        }

        // HTML writer helper (keeps consistent headers & closes response)
        private static void WriteHtml(RelayedHttpListenerContext context, string innerHtml)
        {
            if (!context.Response.OutputStream.CanWrite) return;
            var body = $"<!DOCTYPE html><html><head><title>Relay</title></head><body>{innerHtml}</body></html>";
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
            context.Response.Headers["Content-Length"] = bytes.Length.ToString();

            // CORS headers
            context.Response.Headers["Access-Control-Allow-Methods"] = "OPTIONS, GET, HEAD, POST";
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private static void WriteError(RelayedHttpListenerContext context, HttpStatusCode status, string message)
        {
            try
            {
                context.Response.StatusCode = status;
                context.Response.StatusDescription = message;
                WriteHtml(context, $"<h1>{(int)status} {WebUtility.HtmlEncode(message)}</h1>");
            }
            catch (Exception ex)
            {
                Logger.Log("ERROR", "Failed to write error response", ex, isError: true);
                try { context.Response.Close(); } catch { /* ignore */ }
            }
        }
    }
}