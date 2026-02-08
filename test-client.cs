using System.Net.Sockets;

var socketPath = "/tmp/you-tui-daemon.sock";
try
{
    using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    var endpoint = new UnixDomainSocketEndPoint(socketPath);
    await socket.ConnectAsync(endpoint);
    Console.WriteLine("✓ Connected to daemon!");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Failed: {ex.Message}");
}
