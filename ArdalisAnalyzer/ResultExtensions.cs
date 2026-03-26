using Ardalis.Result;

// These extensions intentionally access .Value without checking status.
// The #pragma suppress is used so the demo can compile and run,
// but the analyzer WOULD flag every access here as ARDRES001.
#pragma warning disable ARDRES001

public static class ResultExtensions
{
    public static Result<TOut> Then<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> transform)
    {
        var transformed = transform(result.Value);
        return Result<TOut>.Success(transformed);
    }

    public static Result<TOut> ThenBind<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Result<TOut>> next)
    {
        return next(result.Value);
    }

    public static Result<T> Tap<T>(
        this Result<T> result,
        Action<T> action)
    {
        action(result.Value);
        return result;
    }

    public static Result<TOut> Combine<T1, T2, TOut>(
        this Result<T1> first,
        Result<T2> second,
        Func<T1, T2, TOut> combiner)
    {
        var combined = combiner(first.Value, second.Value);
        return Result<TOut>.Success(combined);
    }

    public static T UnwrapOr<T>(
        this Result<T> result,
        T fallback)
    {
        return result.Value ?? fallback;
    }

    public static string Dump<T>(this Result<T> result, string label)
    {
        return $"[{label}] {result.Value}";
    }
}

#pragma warning restore ARDRES001
