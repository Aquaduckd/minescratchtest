using MineSharp.Server;

namespace MineSharp.Server;

/// <summary>
/// Server entry point.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ServerConfiguration();
        
        // TODO: Parse command line arguments for configuration
        
        var server = new Server(configuration);
        
        Console.WriteLine("Starting MineSharp server...");
        await server.StartAsync();
        
        Console.WriteLine("Server started. Press Ctrl+C to stop.");
        
        // Wait for shutdown signal
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down server...");
            server.Stop();
        }
    }
}
