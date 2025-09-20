using System;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using System.Threading.Tasks;

/// <summary>
/// Simple pipeline policy that logs outbound request method, URI, and headers to Console.Error.
/// Authorization header value is masked except for the scheme.
/// </summary>
public sealed class HeaderLoggingPolicy : HttpPipelinePolicy
{
    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        ProcessNext(message, pipeline); // allow auth to add headers first
        Log(message);
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var vt = ProcessNextAsync(message, pipeline);
        if (vt.IsCompletedSuccessfully)
        {
            Log(message);
            return vt;
        }
        return AwaitAndLogAsync(vt, message);
    }

    private static async ValueTask AwaitAndLogAsync(ValueTask vt, HttpMessage message)
    {
        await vt.ConfigureAwait(false);
        Log(message);
    }

    private static void Log(HttpMessage message)
    {
        try
        {
            var req = message.Request;
            var sb = new StringBuilder();
            sb.AppendLine("[HeaderLogging] ---> " + req.Method + " " + req.Uri);
            foreach (var header in req.Headers)
            {
                var name = header.Name;
                var value = header.Value; // value is already sanitized inside pipeline
                if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    // Attempt to split scheme and token
                    var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        sb.AppendLine($"Authorization: {parts[0]} ***masked*** (len={parts[1].Length})");
                    }
                    else
                    {
                        sb.AppendLine("Authorization: ***masked***");
                    }
                }
                else
                {
                    // Avoid dumping extremely long values (e.g., user agents should be fine)
                    var display = value.Length > 200 ? value.Substring(0, 200) + "..." : value;
                    sb.AppendLine(name + ": " + display);
                }
            }
            sb.AppendLine("[HeaderLogging] <--- end headers");
            Console.Error.Write(sb.ToString());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[HeaderLogging] Error while logging headers: " + ex.Message);
        }
    }
}
