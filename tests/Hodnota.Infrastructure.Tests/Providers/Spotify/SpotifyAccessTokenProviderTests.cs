using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Time.Testing;

namespace Hodnota.Infrastructure.Tests.Providers.Spotify;

public class SpotifyAccessTokenProviderTests
{
    private static readonly Uri AccountsBaseAddress = new("https://accounts.spotify.com/");
    private static readonly SpotifyCredentials Credentials = new("client-id", "client-secret", null);

    private static HttpResponseMessage TokenResponse(string accessToken = "token-1", int expiresIn = 3600) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new SpotifyTokenResponse(accessToken, expiresIn)) };

    private static (SpotifyAccessTokenProvider Provider, TestHttpMessageHandler Handler, FakeTimeProvider TimeProvider) NewProvider()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, SpotifyConfiguration.AccountsHttpClientName, AccountsBaseAddress);
        var timeProvider = new FakeTimeProvider();
        return (new SpotifyAccessTokenProvider(factory, Credentials, timeProvider), handler, timeProvider);
    }

    [Fact]
    public async Task GetTokenAsync_FirstCall_PostsClientCredentialsGrantWithBasicAuthHeader()
    {
        var (provider, handler, _) = NewProvider();
        handler.Enqueue(TokenResponse());

        var token = await provider.GetTokenAsync(CancellationToken.None);

        token.Should().Be("token-1");
        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.Should().Be("/api/token");
        request.Headers.Authorization!.Scheme.Should().Be("Basic");
        handler.RequestBodies[0].Should().Be("grant_type=client_credentials");
    }

    [Fact]
    public async Task GetTokenAsync_SecondCallBeforeExpiry_ReusesCachedTokenWithoutASecondRequest()
    {
        var (provider, handler, _) = NewProvider();
        handler.Enqueue(TokenResponse());

        var first = await provider.GetTokenAsync(CancellationToken.None);
        var second = await provider.GetTokenAsync(CancellationToken.None);

        second.Should().Be(first);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetTokenAsync_TokenNearExpiry_RequestsANewOne()
    {
        var (provider, handler, timeProvider) = NewProvider();
        handler.Enqueue(TokenResponse("token-1"));
        handler.Enqueue(TokenResponse("token-2"));
        await provider.GetTokenAsync(CancellationToken.None);

        // Token lasts 3600s with a 60s expiry margin (cached good only up to +3540s) — advancing
        // past that, but still before the raw 3600s, proves the margin is what triggers refresh.
        timeProvider.Advance(TimeSpan.FromSeconds(3550));
        var second = await provider.GetTokenAsync(CancellationToken.None);

        second.Should().Be("token-2");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTokenAsync_ConcurrentCallers_RequestsTheTokenOnlyOnce()
    {
        var (provider, handler, _) = NewProvider();
        handler.Enqueue(TokenResponse());

        var results = await Task.WhenAll(provider.GetTokenAsync(CancellationToken.None), provider.GetTokenAsync(CancellationToken.None));

        results.Should().Equal("token-1", "token-1");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetTokenAsync_TokenEndpointReturnsError_ThrowsStreamingProviderException()
    {
        var (provider, handler, _) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var act = () => provider.GetTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task Invalidate_ThenGetTokenAsync_RequestsANewToken()
    {
        var (provider, handler, _) = NewProvider();
        handler.Enqueue(TokenResponse("token-1"));
        handler.Enqueue(TokenResponse("token-2"));
        await provider.GetTokenAsync(CancellationToken.None);

        provider.Invalidate();
        var second = await provider.GetTokenAsync(CancellationToken.None);

        second.Should().Be("token-2");
        handler.Requests.Should().HaveCount(2);
    }
}
