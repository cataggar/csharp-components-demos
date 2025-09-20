using System;               // Console
using System.Net.Http;       // HttpClient
using System.Threading;      // Thread
using System.Threading.Tasks; // Task / async support
using System.Runtime.CompilerServices; // UnsafeAccessor
using Azure.Identity; // DefaultAzureCredential
using Azure.ResourceManager; // ArmClient
using Azure.ResourceManager.Avs; // AVS specific resource types
// Removed JSON fallback; using only Azure SDK path
using System.Runtime.InteropServices; // RuntimeInformation, OSPlatform, Architecture

public static class WasiMainWrapper
{
    public static async Task<int> MainAsync(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI")))
        {
            Console.WriteLine("Running in WASI environment (OS platform check)");
        }
        else
        {
            Console.WriteLine("Running in non-WASI environment (OS platform check)");
        }

        if (RuntimeInformation.ProcessArchitecture == Architecture.Wasm)
        {
            Console.WriteLine("Running in WASI environment (architecture check)");
        }
        else
        {
            Console.WriteLine("Running in non-WASI environment (architecture check)");
        }

        if (OperatingSystem.IsWasi())
        {
            Console.WriteLine("Running in WASI environment (OSPlatform.IsWasi check)");
        }
        else
        {
            Console.WriteLine("Running in non-WASI environment (OSPlatform.IsWasi check)");
        }


        // Acquire subscription Id
        string? subscriptionId = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            Console.Error.WriteLine("Subscription Id must be provided as first argument or AZURE_SUBSCRIPTION_ID env var.");
            return 1;
        }

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true,
            ExcludeAzureCliCredential = false,
            ExcludeManagedIdentityCredential = false
        });

        using var httpClient = new HttpClient();
        var armOptions = new ArmClientOptions
        {
            Transport = new Azure.Core.Pipeline.HttpClientTransport(httpClient)
        };
        var arm = new ArmClient(credential, subscriptionId, armOptions);
        Console.WriteLine($"(Azure SDK) Listing AVS Private Clouds in subscription {subscriptionId}...");
        var subscription = arm.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));
        await foreach (AvsPrivateCloudResource pc in subscription.GetAvsPrivateCloudsAsync())
        {
            Console.WriteLine($"- {pc.Data.Name} (Location: {pc.Data.Location}, Sku: {pc.Data.Sku?.Name})");
        }
        return 0;
    }

    public static int Main(string[] args)
    {
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}
