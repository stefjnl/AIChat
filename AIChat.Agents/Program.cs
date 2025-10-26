using AIChat.Agents.Testing;

namespace AIChat.Agents;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("AIChat Provider Test Harness");
        Console.WriteLine("============================\n");
        
        try
        {
            await ProviderTestHarness.RunTests();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}