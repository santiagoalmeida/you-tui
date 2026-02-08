using System.Net.Sockets;

var socketPath = "/tmp/you-tui-daemon.sock";
Console.WriteLine($"Testing connection to {socketPath}...");

try
{
    using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    var endpoint = new UnixDomainSocketEndPoint(socketPath);
    Console.WriteLine("Connecting...");
    await socket.ConnectAsync(endpoint);
    Console.WriteLine("✓ Connected successfully!");
    socket.Close();
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Failed: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
