using System;               // Console
using System.Net.Http;       // HttpClient
using System.Threading;      // Thread
using System.Threading.Tasks; // Task / async support
using System.Runtime.CompilerServices; // UnsafeAccessor

public static class WasiMainWrapper
{
    public static async Task<int> MainAsync(string[] args)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "dotnet WASI");
        var query="https://www.random.org/integers/?num=1&min=40&max=42&col=1&base=10&format=plain&rnd=new";
        var json = await client.GetStringAsync(query);
        Console.WriteLine(json);

        // ExampleWorld.exports.ExampleWorld.Add(2, int.Parse(json));
        return 0;
    }

    public static int Main(string[] args)
    {
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}
