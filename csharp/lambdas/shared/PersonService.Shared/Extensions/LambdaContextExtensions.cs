using Amazon.Lambda.Core;

namespace PersonService.Shared.Extensions;

public static class LambdaContextExtensions
{
    public static CancellationTokenSource GetCancellationTokenSource(this ILambdaContext context, TimeSpan beforeAbort = default)
    {
        const double PercentOfRemaining = 0.0025;
        var cts = new CancellationTokenSource();
        var remaining = context.RemainingTime;

        if (beforeAbort == default)
        {
            beforeAbort = TimeSpan.FromSeconds(remaining.TotalSeconds * PercentOfRemaining);
        }

        if (beforeAbort > remaining)
        {
            cts.CancelAfter(remaining.Subtract(beforeAbort));
        }

        return cts;
    }
}