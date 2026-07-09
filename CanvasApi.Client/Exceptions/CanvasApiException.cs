namespace CanvasApi.Client.Exceptions;

public class CanvasApiException : Exception
{
    public HttpResponseMessage Response { get; }
    public HttpStatusCode? StatusCode => this.Response?.StatusCode;
    public bool? IsSuccessful => this.Response.IsSuccessStatusCode;

    public JObject ApiResultObject { get; private set; }
    public string ResultMessage { get; private set; }

    internal CanvasApiException(HttpResponseMessage response, string message = null, Exception innterException = null) : base(message, innterException)
    {
        this.Response = response;
        this.ParseApiResult();
    }

    // Read synchronously: a constructor can't be async, so the prior ParseApiResult().Wait() was sync-over-async.
    // The response content is already buffered (default HttpCompletionOption.ResponseContentRead), so ReadAsStream()
    // completes without blocking on network I/O.
    private void ParseApiResult()
    {
        using var stream = this.Response.Content.ReadAsStream();
        using var reader = new System.IO.StreamReader(stream);
        var msg = reader.ReadToEnd();
        try
        {
            this.ApiResultObject = JObject.Parse(msg);
        }
        catch (JsonReaderException)
        {
            this.ApiResultObject = null;
            this.ResultMessage = msg;
        }
        catch (Exception ex)
        {
            this.ResultMessage = ex.Message;
        }
    }
}