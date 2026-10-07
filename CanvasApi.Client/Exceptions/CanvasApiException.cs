namespace CanvasApi.Client.Exceptions;

public class CanvasApiException : Exception
{
    // Cap on the raw body retained for diagnostics. Canvas error bodies are tiny, but an
    // upstream HTML/CloudFront error page can be enormous and would otherwise be copied
    // verbatim into every log line and support ticket.
    private const int MaxResultMessageLength = 4096;

    // Shorter cap for the synthesized Message, which ends up in log lines and ticket titles.
    private const int MaxMessageExcerptLength = 300;

    private readonly bool hasExplicitMessage;

    public HttpResponseMessage Response { get; }
    public HttpStatusCode? StatusCode => this.Response?.StatusCode;
    public bool? IsSuccessful => this.Response.IsSuccessStatusCode;

    public JObject ApiResultObject { get; private set; }
    public string ResultMessage { get; private set; }

    internal CanvasApiException(HttpResponseMessage response, string message = null, Exception innterException = null) : base(message, innterException)
    {
        this.hasExplicitMessage = !string.IsNullOrWhiteSpace(message);
        this.Response = response;
        this.ParseApiResult();
    }

    // Without a caller-supplied message the framework default is "Exception of type
    // 'CanvasApiException' was thrown." — which is what an on-call engineer reads in a log line
    // or a support-ticket title, and it says nothing at all. Synthesize the upstream status and
    // an excerpt of the body instead, so the Canvas error/error_description is visible up front.
    public override string Message => this.hasExplicitMessage
        ? base.Message
        : $"Canvas API {(int?)this.StatusCode} {this.Response?.ReasonPhrase}: {CanvasApiException.Excerpt(this.ResultMessage, CanvasApiException.MaxMessageExcerptLength) ?? "<empty body>"}";

    // Read synchronously: a constructor can't be async, so the prior ParseApiResult().Wait() was sync-over-async.
    private void ParseApiResult()
    {
        using var stream = this.Response.Content.ReadAsStream();
        using var reader = new System.IO.StreamReader(stream);
        var msg = reader.ReadToEnd();

        // Always retain the RAW body, not just the parsed JObject. ApiResultObject alone is not
        // enough for diagnostics: it is a Newtonsoft JObject, and any consumer that serializes
        // this exception with System.Text.Json (the support-ticket pipeline does) sees each
        // JValue as an empty IEnumerable and writes "[]" — silently discarding the very error
        // text the ticket exists to carry. A plain string survives every serializer.
        this.ResultMessage = CanvasApiException.Excerpt(msg, CanvasApiException.MaxResultMessageLength);

        try
        {
            this.ApiResultObject = JObject.Parse(msg);
        }
        catch (Exception)
        {
            // Not JSON (or not an object) — ResultMessage already carries the body verbatim.
            this.ApiResultObject = null;
        }
    }

    private static string Excerpt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        return trimmed.Length > maxLength ? trimmed[..maxLength] + "… [truncated]" : trimmed;
    }
}
