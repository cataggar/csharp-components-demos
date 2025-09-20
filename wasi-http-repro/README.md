# WASI HttpClient Header Parsing Repro

Minimal reproduction of a response header parsing failure when using .NET 10 (preview) HttpClient under WASI via wasmtime `-S http` against `management.azure.com`.

## Repro Steps

Build:
```
dotnet build ./wasi-http-repro/wasi-http-repro.csproj -c Debug
```
Run success case (example.com):
```
wasmtime run -S http ./wasi-http-repro/bin/Debug/net10.0/wasi-wasm/publish/wasi-http-repro.wasm example
```
Expected: `200 OK` and HTML body snippet.

Run failure case (Azure management metadata):
```
wasmtime run -S http ./wasi-http-repro/bin/Debug/net10.0/wasi-wasm/publish/wasi-http-repro.wasm mgmt
```
Observed: `System.FormatException: The format of value '-1' is invalid.` originating from `System.Net.Http.Headers.HttpHeaderParser.ParseValue` during response header conversion in `System.Net.Http.WasiHttpInterop.ConvertResponseHeaders`.

Override target URL (optional):
```
wasmtime run -S http --env RAW_URL=https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000?api-version=2024-09-01 ./wasi-http-repro/bin/Debug/net10.0/wasi-wasm/publish/wasi-http-repro.wasm custom
```

## Environment
- .NET SDK: (run `dotnet --info`)
- wasmtime: (run `wasmtime --version`) with `-S http` enabling wasi:http preview
- OS: Windows (host) running wasmtime

## Expected Behavior
HttpClient should successfully receive a 200 (or appropriate 4xx/401) response or at minimum not throw a FormatException when parsing standard headers from Azure Front Door / management endpoint.

## Actual Behavior
Throws `System.FormatException: The format of value '-1' is invalid.` before status or headers can be printed for management.azure.com endpoints. Other domains (example.com) succeed.

## Stack Trace (Representative)
```
System.FormatException: The format of value '-1' is invalid.
   at System.Net.Http.Headers.HttpHeaderParser.ParseValue(String, Object, Int32&)
   at System.Net.Http.Headers.HttpHeaders.ParseAndAddValue(HeaderDescriptor, HttpHeaders.HeaderStoreItemInfo, String)
   at System.Net.Http.Headers.HttpHeaders.Add(HeaderDescriptor, String)
   at System.Net.Http.Headers.HttpHeaders.Add(String, String)
   at System.Net.Http.WasiHttpInterop.ConvertResponseHeaders(ITypes.IncomingResponse, HttpResponseMessage)
   at System.Net.Http.WasiRequestWrapper.<SendRequestAsync>d__4.MoveNext()
--- End of stack trace from previous location ---
   at System.Net.Http.WasiHttpHandler.<SendAsync>d__6.MoveNext()
--- End of stack trace from previous location ---
   at System.Net.Http.HttpClient.<<SendAsync>g__Core|83_0>d.MoveNext()
```

## Notes
- Removing Accept header does not avoid the exception.
- Adding Authorization header (Bearer token) does not change failure mode.
- Occurs for multiple management paths: root subscription query, metadata endpoint.

## Minimal Code
See `Program.cs`:
```csharp
using System; using System.Net.Http; using System.Threading.Tasks;
// ... main method sets URL based on arg, sends GET with Accept: application/json.
```

## Issue Template Snippet
Title: WASI HttpClient (WasiHttpHandler) FormatException parsing management.azure.com response header value "-1"

(Include environment, repro steps, and stack trace above.)
