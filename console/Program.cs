using System;               // Console
using System.Net.Http;       // HttpClient
using System.Threading;      // Thread
using System.Threading.Tasks; // Task / async support
using System.Runtime.CompilerServices; // UnsafeAccessor
using Azure.ResourceManager; // ArmClient
using Azure.ResourceManager.Avs; // AVS specific resource types
using Azure.Core; // TokenCredential
using Azure.Core.Pipeline; // HttpPipelinePosition
// Removed JSON fallback; using only Azure SDK path
using System.Runtime.InteropServices; // RuntimeInformation, OSPlatform, Architecture

public static class WasiMainWrapper
{
    public static async Task<int> MainAsync(string[] args)
    {

        // Acquire subscription Id
        string? subscriptionId = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            Console.Error.WriteLine("Subscription Id must be provided as first argument or AZURE_SUBSCRIPTION_ID env var.");
            return 1;
        }

        // RAW_TEST: perform a direct HttpClient request before acquiring credential
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAW_TEST")))
        {
            using var rawHttpClient = new HttpClient();
            try
            {
                var rawUrl = Environment.GetEnvironmentVariable("RAW_URL");
                if (string.IsNullOrWhiteSpace(rawUrl))
                {
                    rawUrl = $"https://management.azure.com/subscriptions/{subscriptionId}?api-version=2024-09-01";
                }
                var rawReq = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                rawReq.Headers.Accept.ParseAdd("application/json");
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAW_ADD_AUTH")))
                {
                    var tok = Environment.GetEnvironmentVariable("AZURE_TOKEN");
                    if (string.IsNullOrWhiteSpace(tok))
                    {
                        Console.WriteLine("[RAW_TEST] RAW_ADD_AUTH set but AZURE_TOKEN missing/empty; skipping Authorization header.");
                    }
                    else
                    {
                        rawReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
                        Console.WriteLine("[RAW_TEST] Added Authorization header (token length=" + tok.Length + ")");
                    }
                }
                Console.WriteLine($"[RAW_TEST] Sending raw GET {rawUrl} with only Accept header");
                var resp = await rawHttpClient.SendAsync(rawReq);
                Console.WriteLine($"[RAW_TEST] Status: {(int)resp.StatusCode} {resp.StatusCode}");
                foreach (var h in resp.Headers)
                {
                    Console.WriteLine($"[RAW_TEST] H: {h.Key}: {string.Join(",", h.Value)}");
                }
                foreach (var h in resp.Content.Headers)
                {
                    Console.WriteLine($"[RAW_TEST] CH: {h.Key}: {string.Join(",", h.Value)}");
                }
                var body = await resp.Content.ReadAsStringAsync();
                if (body.Length > 500) body = body.Substring(0, 500) + "...";
                Console.WriteLine("[RAW_TEST] Body snippet:\n" + body);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[RAW_TEST] Exception: " + ex);
                return 1;
            }
        }

        TokenCredential credential;
        try
        {
            credential = new AzureTokenCredential();
            Console.WriteLine("Using AzureTokenCredential (AZURE_TOKEN, 24h expiry)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize AzureTokenCredential: {ex.Message}");
            return 1;
        }

        using var httpClient = new HttpClient();
        var armOptions = new ArmClientOptions
        {
            Transport = new Azure.Core.Pipeline.HttpClientTransport(httpClient)
        };
    // Final strip: keep only Authorization (Host implicit). Then log the final header set.
    armOptions.AddPolicy(new FinalStripHeadersPolicy(), HttpPipelinePosition.BeforeTransport);
    armOptions.AddPolicy(new HeaderLoggingPolicy(), HttpPipelinePosition.BeforeTransport);
        var arm = new ArmClient(credential, subscriptionId, armOptions);
        Console.WriteLine($"(Azure SDK) Listing AVS Private Clouds in subscription {subscriptionId}...");
        if (subscriptionId.Contains("\n") || subscriptionId.Contains("\r"))
        {
            Console.Error.WriteLine("[Warning] Subscription ID contains newline characters; this will cause invalid headers.");
        }
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
