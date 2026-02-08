using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using YouTui.Shared.Models;

namespace YouTui.Daemon.Services;

public class DaemonServer : IDisposable
{
    private readonly string _socketPath;
    private readonly CommandHandler _commandHandler;
    private Socket? _serverSocket;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public DaemonServer(string socketPath, CommandHandler commandHandler)
    {
        _socketPath = socketPath;
        _commandHandler = commandHandler;
    }

    public Task StartAsync()
    {
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var endpoint = new UnixDomainSocketEndPoint(_socketPath);
        _serverSocket.Bind(endpoint);
        _serverSocket.Listen(10);

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenForConnectionsAsync(_cts.Token));

        Console.WriteLine($"Daemon listening on {_socketPath}");
        return Task.CompletedTask;
    }

    private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _serverSocket!.AcceptAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(clientSocket, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting connection: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new NetworkStream(clientSocket);
            using var reader = new StreamReader(stream, new UTF8Encoding(false)); // No BOM
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true }; // No BOM

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;

                try
                {
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    };
                    
                    var command = JsonSerializer.Deserialize<DaemonCommand>(line, options);
                    if (command == null) continue;

                    var response = await _commandHandler.HandleCommandAsync(command);
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                }
                catch (Exception ex)
                {
                    var errorResponse = new DaemonResponse
                    {
                        Status = "error",
                        Message = ex.Message
                    };
                    var errorJson = JsonSerializer.Serialize(errorResponse);
                    await writer.WriteLineAsync(errorJson);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            clientSocket.Close();
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_listenTask != null)
            await _listenTask;

        _serverSocket?.Close();
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _serverSocket?.Dispose();
        _cts?.Dispose();
    }
}
