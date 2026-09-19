namespace Hodnota.Infrastructure.Tests.Providers;

// A minimal hand-rolled HttpMessageHandler test double, not a mocking-library substitute:
// HttpMessageHandler.SendAsync is protected, so NSubstitute cannot intercept it. Used only for the
// Spotify client (SpotifyAccessTokenProvider/SpotifyApiClient), where hodnota owns the whole HTTP
// exchange (auth, retries, rate-limit handling) — unlike YouTube, where the SDK owns transport and
// only the pure mapping is worth testing directly. See ADR 0011.
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    // The real client disposes each request (and its Content) once SendAsync returns, so a request's
    // body must be captured here, while it is still alive, rather than re-read from Requests later.
    public List<string?> RequestBodies { get; } = [];

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> respond) => _responses.Enqueue(respond);

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(_ => response);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        var respond = _responses.Count > 0
            ? _responses.Dequeue()
            : throw new InvalidOperationException("No more queued responses for TestHttpMessageHandler.");
        return respond(request);
    }

    public static IHttpClientFactory CreateFactory(TestHttpMessageHandler handler, string clientName, Uri baseAddress) =>
        CreateFactory((clientName, new HttpClient(handler, disposeHandler: false) { BaseAddress = baseAddress }));

    public static IHttpClientFactory CreateFactory(params (string Name, HttpClient Client)[] clients) =>
        new NamedClientFactory(clients.ToDictionary(c => c.Name, c => c.Client));

    private sealed class NamedClientFactory(Dictionary<string, HttpClient> clients) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            clients.TryGetValue(name, out var client) ? client : throw new InvalidOperationException($"Unexpected HttpClient name '{name}'.");
    }
}
