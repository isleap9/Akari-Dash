namespace AkariDash.Framework;

/// <summary>Small functional helpers.</summary>
public static class ObjectExtensions
{
    /// <summary>Applies <paramref name="func"/> to <paramref name="value"/> and returns the result.</summary>
    public static TResult Let<TSource, TResult>(this TSource value, Func<TSource, TResult> func)
        => func(value);

    /// <summary>Applies <paramref name="action"/> to <paramref name="value"/> when it is not null.</summary>
    public static TSource? IfNotNull<TSource>(this TSource? value, Action<TSource> action)
        where TSource : class
    {
        if (value is not null)
        {
            action(value);
        }

        return value;
    }
}
