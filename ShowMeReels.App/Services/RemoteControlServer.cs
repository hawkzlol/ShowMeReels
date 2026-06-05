using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShowMeReels.App.Services;

public sealed class RemoteControlServer : IRemoteControlServer
{
    public const int DefaultPort = 18777;

    private readonly int _port;
    private CancellationTokenSource? _cancellationTokenSource;
    private TcpListener? _listener;
    private Task? _listenTask;

    public RemoteControlServer(int port = DefaultPort)
    {
        _port = port;
        Url = $"http://127.0.0.1:{port}/";
    }

    public event EventHandler<RemoteControlCommandEventArgs>? CommandReceived;

    public string Url { get; }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        _cancellationTokenSource?.Dispose();
    }

    public void Start()
    {
        if (_listenTask is not null)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _listenTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
        AppDiagnostics.Log($"Remote control server listening at {Url}");
    }

    public static string BuildRemotePage()
    {
        return """
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
              <title>ShowMeReels Remote</title>
              <style>
                * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
                html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: #050608; color: #f8fafc; font-family: sans-serif; }
                main { height: 100%; display: grid; grid-template-rows: 1fr minmax(84px, 22vh); gap: 14px; padding: max(16px, env(safe-area-inset-top)) 16px max(16px, env(safe-area-inset-bottom)); }
                .top { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; min-height: 0; }
                button { width: 100%; height: 100%; border: 1px solid #2f3745; border-radius: 18px; background: #121822; color: #f8fafc; font-size: clamp(68px, 24vh, 190px); line-height: 1; touch-action: manipulation; user-select: none; }
                .down { font-size: clamp(44px, 12vh, 92px); }
                button:active { background: #263142; transform: scale(0.992); }
              </style>
            </head>
            <body>
              <main>
                <section class="top" aria-label="Primary controls">
                  <button type="button" data-command="up" aria-label="Scroll up">^</button>
                  <button type="button" data-command="pause" aria-label="Pause or play">||</button>
                </section>
                <button class="down" type="button" data-command="down" aria-label="Scroll down">v</button>
              </main>
              <script>
                async function send(command) {
                  try {
                    await fetch("/" + command, { method: "POST", cache: "no-store" });
                  } catch {
                  }
                }

                for (const button of document.querySelectorAll("button[data-command]")) {
                  button.addEventListener("click", () => send(button.dataset.command));
                }
              </script>
            </body>
            </html>
            """;
    }

    public static int? ParseDirection(string requestPath)
    {
        string normalizedPath = requestPath.Split('?', 2)[0].Trim('/').ToLowerInvariant();
        return normalizedPath switch
        {
            "up" => -1,
            "down" => 1,
            _ => null,
        };
    }

    public static RemoteControlCommand? ParseCommand(string requestPath)
    {
        string normalizedPath = requestPath.Split('?', 2)[0].Trim('/').ToLowerInvariant();
        return normalizedPath switch
        {
            "pause" => RemoteControlCommand.TogglePlayPause,
            _ => null,
        };
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception exception)
            {
                AppDiagnostics.Log($"Remote control accept failed: {exception.Message}");
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);

        try
        {
            string? requestLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string path = parts.Length >= 2 ? parts[1] : "/";

            while (!cancellationToken.IsCancellationRequested)
            {
                string? headerLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(headerLine))
                {
                    break;
                }
            }

            int? direction = ParseDirection(path);
            if (direction.HasValue)
            {
                CommandReceived?.Invoke(this, new RemoteControlCommandEventArgs(direction.Value));
                await WriteResponseAsync(stream, "ok", "text/plain; charset=utf-8", cancellationToken);
                return;
            }

            RemoteControlCommand? command = ParseCommand(path);
            if (command.HasValue)
            {
                CommandReceived?.Invoke(this, new RemoteControlCommandEventArgs(command.Value));
                await WriteResponseAsync(stream, "ok", "text/plain; charset=utf-8", cancellationToken);
                return;
            }

            if (path == "/" || path.StartsWith("/?", StringComparison.Ordinal))
            {
                await WriteResponseAsync(stream, BuildRemotePage(), "text/html; charset=utf-8", cancellationToken);
                return;
            }

            await WriteResponseAsync(stream, "not found", "text/plain; charset=utf-8", cancellationToken, statusCode: "404 Not Found");
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException)
        {
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        string body,
        string contentType,
        CancellationToken cancellationToken,
        string statusCode = "200 OK")
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string headers =
            $"HTTP/1.1 {statusCode}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);

        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }
}
