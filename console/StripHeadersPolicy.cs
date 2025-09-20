using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;

/// <summary>
/// Removes all request headers except a fixed allowlist (Authorization, Accept, Host) after other policies run.
/// Placed at PerRetry to allow auth policy to add Authorization first.
/// </summary>
public sealed class StripHeadersPolicy : HttpPipelinePolicy
{
    private static readonly HashSet<string> _keep = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Accept",
        "Host" // Typically synthesized; kept for completeness
    };

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        ProcessNext(message, pipeline);
        Strip(message);
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var vt = ProcessNextAsync(message, pipeline);
        if (vt.IsCompletedSuccessfully)
        {
            Strip(message);
            return vt;
        }
        return AwaitAndStripAsync(vt, message);
    }

    private static async ValueTask AwaitAndStripAsync(ValueTask vt, HttpMessage message)
    {
        await vt.ConfigureAwait(false);
        Strip(message);
    }

    private static void Strip(HttpMessage message)
    {
        try
        {
            var headers = message.Request.Headers;
            // Collect names first to avoid modifying while enumerating
            var toRemove = new List<string>();
            foreach (var h in headers)
            {
                if (!_keep.Contains(h.Name))
                {
                    toRemove.Add(h.Name);
                }
            }
            foreach (var name in toRemove)
            {
                headers.Remove(name);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[StripHeadersPolicy] Error stripping headers: " + ex.Message);
        }
    }
}
