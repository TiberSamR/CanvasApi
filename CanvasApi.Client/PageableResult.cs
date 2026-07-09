// ReSharper disable InconsistentNaming
namespace CanvasApi.Client;

internal class PageableResult<TResult> : IEnumerable<TResult>, IAsyncEnumerable<TResult>
{
    protected TResult[] InitialBuffer { get; set; }
    protected readonly CanvasApiClient _ApiClient;
    protected readonly PageLinks _initialPageLinks;

    internal PageableResult(
        IEnumerable<TResult> initialBuffer,
        CanvasApiClient apiClient,
        PageLinks pageLinks
    ) : this(apiClient, pageLinks)
    {
        this.InitialBuffer = initialBuffer?.ToArray() ?? Array.Empty<TResult>();
    }

    internal PageableResult(
        CanvasApiClient apiClient,
        PageLinks pageLinks
    )
    {
        this._initialPageLinks = pageLinks ?? throw new ArgumentNullException(nameof(pageLinks), "Paging details not supplied.");
        this._ApiClient = apiClient;
    }

    [Obsolete("Synchronous enumeration blocks a thread-pool thread on every page fetch after the first (sync-over-async), which can starve the pool under load. Consume without blocking via await foreach, or the ToListAsync()/ToArrayAsync() extensions.")]
    public virtual IEnumerator<TResult> GetEnumerator() =>
        new PageableResultsEnumerator<TResult>(this.InitialBuffer)
        {
            Links = this._initialPageLinks,
            ApiClient = this._ApiClient
        };

#pragma warning disable CS0618 // the synchronous enumerator is the intended (deprecated) fallback for IEnumerable consumers
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
#pragma warning restore CS0618

    // Non-blocking page streaming: page 1 is already buffered, subsequent pages are awaited. Preferred path.
    public virtual IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        this._ApiClient
            .PageStreamAsync<TResult, IEnumerable<TResult>>(this.InitialBuffer, this._initialPageLinks, static data => data, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
}

/// <summary>
/// Pagable Results proocess when a conversion has to occur.
/// </summary>
/// <typeparam name="TResult">Enumerable Type - expected to return from API</typeparam>
/// <typeparam name="TApiResult">Result type provided by the API</typeparam>
internal class PageableResult<TResult, TApiResult> : PageableResult<TResult>
{
    private Func<TApiResult, IEnumerable<TResult>> Factory;

    internal PageableResult(
        TApiResult apiResult,
        CanvasApiClient apiClient,
        PageLinks pageLinks,
        Func<TApiResult, IEnumerable<TResult>> factory
    ) : base(apiClient, pageLinks)
    {
        this.Factory = factory;
        this.InitialBuffer = this.Factory(apiResult).ToArray();
    }

    [Obsolete("Synchronous enumeration blocks a thread-pool thread on every page fetch after the first (sync-over-async). Consume without blocking via await foreach, or the ToListAsync()/ToArrayAsync() extensions.")]
    public override IEnumerator<TResult> GetEnumerator() =>
        new PageableResultsEnumerator<TResult, TApiResult>(this.InitialBuffer, this.Factory)
        {
            Links = this._initialPageLinks,
            ApiClient = this._ApiClient,
        };

    public override IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        this._ApiClient
            .PageStreamAsync<TResult, TApiResult>(this.InitialBuffer, this._initialPageLinks, this.Factory, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
}

/// <summary>
/// Enumerator for page results where the TApiType is the IEnumerable<typeparamref name="TResult"/>
/// </summary>
/// <typeparam name="TResult"></typeparam>
internal class PageableResultsEnumerator<TResult> : PageableResultsEnumerator<TResult, IEnumerable<TResult>>
{
    internal PageableResultsEnumerator(TResult[] buffer) : base(buffer, data => data) { }
}

internal class PageableResultsEnumerator<TResult, TApiResult> : IEnumerator<TResult>
{
    internal TResult[] Buffer { get; set; }
    internal PageLinks Links { get; set; }

    private Func<TApiResult, IEnumerable<TResult>> Factory { get; }
    internal CanvasApiClient ApiClient { get; init; }

    int bufferIdx = -1;

    internal PageableResultsEnumerator(TApiResult buffer, Func<TApiResult, IEnumerable<TResult>> factory): this(factory)
    {
        this.Buffer = this.Factory(buffer).ToArray();
        this.bufferIdx = -1;
    }

    internal PageableResultsEnumerator(IEnumerable<TResult> buffer, Func<TApiResult, IEnumerable<TResult>> factory) : this(factory)
    {
        this.Buffer = buffer?.ToArray() ?? Array.Empty<TResult>();
        this.bufferIdx = -1;
    }

    internal PageableResultsEnumerator(Func<TApiResult, IEnumerable<TResult>> factory) => this.Factory = factory;

    public TResult Current => this.bufferIdx >= 0 && this.bufferIdx < this.Buffer.Length ? this.Buffer[this.bufferIdx] : default(TResult);
    object IEnumerator.Current => this.Current;

    public void Dispose()
    {
        // Nothing to dispose
    }

    public bool MoveNext() => ++this.bufferIdx < this.Buffer.Length ? true : this.MoveNextPage();

    public void Reset() => this.GetPage(this.Links.First ?? this.Links.OriginalUrl, true);
    private bool MoveNextPage() => this.GetPage(this.Links.Next);

    protected virtual IEnumerable<TResult> CallApi(string url, out PageLinks newPageLinks, Func<TApiResult, IEnumerable<TResult>> factory)
    {
        newPageLinks = new PageLinks(this.Links.OriginalUrl);

        var apiCall = this.ApiClient.ApiOperation<TApiResult, object>(HttpMethod.Get, this.Links.Next, null, newPageLinks);
        apiCall.Wait();

        return factory(apiCall.Result);
    }

    private bool GetPage(string url, bool isReset = false)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        var apiCall = this.CallApi(url, out var newPageLinks, this.Factory);

        this.Buffer = apiCall.ToArray();

        this.Links = newPageLinks;
        this.bufferIdx = -1;

        return isReset ? true : this.MoveNext();
    }
}
