using System.Net;

namespace StudioTechBI.Infrastructure.Tests.Clients;

/// <summary>
/// Minimal HttpMessageHandler test double: records the last request and
/// returns a pre-configured response (or invokes a per-call responder).
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string content = "")
        : this(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        })
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return _responder(request);
    }
}
