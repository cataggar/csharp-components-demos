using System;
using System.Net.Http;
using System.Threading.Tasks;

// Minimal repro for dotnet/runtime: WasiHttpHandler response header parsing issue with management.azure.com
// Repro steps (after build):
// 1. wasmtime run -S http ./bin/Debug/net10.0/wasi-wasm/publish/wasi-http-repro.wasm example.com
//    -> Expect 200 OK
// 2. wasmtime run -S http ./bin/Debug/net10.0/wasi-wasm/publish/wasi-http-repro.wasm mgmt
//    -> Observed FormatException: value '-1' is invalid (response from https://management.azure.com)
// 3. Optionally set RAW_URL to override.
//
// .NET version: capture via `dotnet --info`
// wasmtime version: `wasmtime --version`

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "example";
        string url = mode switch
        {
            "example" or "example.com" => "https://example.com",
            "mgmt" => "https://management.azure.com/metadata/endpoints?api-version=2020-01-01",
            _ => mode
        };
        var overrideUrl = Environment.GetEnvironmentVariable("RAW_URL");
        if (!string.IsNullOrWhiteSpace(overrideUrl)) url = overrideUrl!;

        using var client = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/json");
        Console.WriteLine($"[REPRO] GET {url}");
        try
        {
            var resp = await client.SendAsync(req);
            Console.WriteLine($"[REPRO] Status: {(int)resp.StatusCode} {resp.StatusCode}");
            foreach (var h in resp.Headers) Console.WriteLine($"[REPRO] H: {h.Key}: {string.Join(",", h.Value)}");
            foreach (var h in resp.Content.Headers) Console.WriteLine($"[REPRO] CH: {h.Key}: {string.Join(",", h.Value)}");
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine("[REPRO] Body snippet:\n" + (body.Length > 500 ? body.Substring(0, 500) + "..." : body));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[REPRO] Exception: " + ex);
            return 1;
        }
    }
}
