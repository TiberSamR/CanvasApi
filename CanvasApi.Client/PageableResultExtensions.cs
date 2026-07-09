namespace CanvasApi.Client;

/// <summary>
/// Non-blocking materialisation for Canvas paged results. A paged endpoint returns a <see cref="PageableResult{TResult}"/>
/// (typed to the caller as <see cref="IEnumerable{T}"/>) that fetches page 2..N lazily; consuming it with a plain
/// <c>ToArray()</c>/<c>foreach</c> blocks a thread-pool thread on each of those fetches. These extensions await the
/// pages instead. When the sequence isn't actually paged (already materialised), they fall back to a synchronous copy.
/// </summary>
public static class PageableResultExtensions
{
    public static async ValueTask<List<TResult>> ToListAsync<TResult>(this IEnumerable<TResult> source, CancellationToken cancellationToken = default)
    {
        if (source is IAsyncEnumerable<TResult> asyncSource)
        {
            var list = new List<TResult>();
            await foreach (var item in asyncSource.WithCancellation(cancellationToken))
                list.Add(item);

            return list;
        }

        return source?.ToList() ?? new List<TResult>();
    }

    public static async ValueTask<TResult[]> ToArrayAsync<TResult>(this IEnumerable<TResult> source, CancellationToken cancellationToken = default) =>
        (await source.ToListAsync(cancellationToken)).ToArray();
}
