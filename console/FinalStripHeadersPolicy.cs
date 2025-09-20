using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;

/// <summary>
/// Final stripping policy run just before transport. Removes every header except Authorization (and Host if present).
/// Placed at HttpPipelinePosition.BeforeTransport so no later policy re-adds headers.
/// </summary>
public sealed class FinalStripHeadersPolicy : HttpPipelinePolicy
{
    private static readonly HashSet<string> _keep = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Host" // synthesized by transport if needed
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
            Console.Error.WriteLine("[FinalStripHeadersPolicy] Error stripping headers: " + ex.Message);
        }
    }
}
