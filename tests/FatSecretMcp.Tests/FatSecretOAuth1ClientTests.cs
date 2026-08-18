// SPDX-License-Identifier: MIT

using System.Net;
using FatSecretMcp.Auth;
using FatSecretMcp.Tests.TestSupport;

namespace FatSecretMcp.Tests;

[TestFixture]
public class FatSecretOAuth1ClientTests
{
    private static HttpResponseMessage TextResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    [Test]
    public void BuildAuthorizeUrl_PercentEncodesToken()
    {
        var url = FatSecretOAuth1Client.BuildAuthorizeUrl("token with spaces");

        Assert.That(url, Is.EqualTo("https://authentication.fatsecret.com/oauth/authorize?oauth_token=token%20with%20spaces"));
    }

    [Test]
    public async Task GetRequestTokenAsync_SendsGetNotPost()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.OK, "oauth_token=tok&oauth_token_secret=sec&oauth_callback_confirmed=true"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        await client.GetRequestTokenAsync();

        // Regression guard: FatSecret's current docs say request_token uses POST; it actually requires GET.
        Assert.That(handler.LastRequest!.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(
            handler.LastRequest.RequestUri!.ToString(),
            Does.StartWith("https://authentication.fatsecret.com/oauth/request_token?"));
        Assert.That(handler.LastRequest.RequestUri!.Query, Does.Contain("oauth_callback=oob"));
    }

    [Test]
    public async Task GetRequestTokenAsync_ParsesTokenAndSecret()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.OK, "oauth_token=abc123&oauth_token_secret=def456&oauth_callback_confirmed=true"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        var (token, tokenSecret) = await client.GetRequestTokenAsync();

        Assert.That(token, Is.EqualTo("abc123"));
        Assert.That(tokenSecret, Is.EqualTo("def456"));
    }

    [Test]
    public void GetRequestTokenAsync_ThrowsWhenCallbackNotConfirmed()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.OK, "oauth_token=abc123&oauth_token_secret=def456"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        Assert.ThrowsAsync<InvalidOperationException>(async () => await client.GetRequestTokenAsync());
    }

    [Test]
    public async Task GetAccessTokenAsync_ParsesTokenAndSendsVerifier()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.OK, "oauth_token=accessTok&oauth_token_secret=accessSec"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        var (accessToken, accessTokenSecret) = await client.GetAccessTokenAsync("reqTok", "reqSecret", "12345");

        Assert.That(accessToken, Is.EqualTo("accessTok"));
        Assert.That(accessTokenSecret, Is.EqualTo("accessSec"));
        Assert.That(handler.LastRequest!.RequestUri!.Query, Does.Contain("oauth_verifier=12345"));
        Assert.That(handler.LastRequest.RequestUri!.Query, Does.Contain("oauth_token=reqTok"));
    }

    [Test]
    public async Task CallApiAsync_TwoLegged_OmitsOAuthToken()
    {
        var handler = new StubHttpMessageHandler(_ => TextResponse(HttpStatusCode.OK, "{}"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        await client.CallApiAsync(new Dictionary<string, string> { ["method"] = "foods.search" });

        Assert.That(handler.LastRequest!.RequestUri!.Query, Does.Not.Contain("oauth_token="));
        Assert.That(handler.LastRequest.RequestUri!.Query, Does.Contain("method=foods.search"));
    }

    [Test]
    public async Task CallApiAsync_ThreeLegged_IncludesOAuthToken()
    {
        var handler = new StubHttpMessageHandler(_ => TextResponse(HttpStatusCode.OK, "{}"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        await client.CallApiAsync(
            new Dictionary<string, string> { ["method"] = "food_entries.get.v2" },
            accessToken: "userToken",
            accessTokenSecret: "userTokenSecret");

        Assert.That(handler.LastRequest!.RequestUri!.Query, Does.Contain("oauth_token=userToken"));
    }

    [Test]
    public async Task CallApiAsync_ReturnsResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => TextResponse(HttpStatusCode.OK, "{\"result\":\"ok\"}"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        var body = await client.CallApiAsync(new Dictionary<string, string> { ["method"] = "foods.search" });

        Assert.That(body, Is.EqualTo("{\"result\":\"ok\"}"));
    }

    [Test]
    public void CallApiAsync_NonSuccessStatus_ThrowsWithBody()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.BadRequest, "Invalid signature: abc"));
        var client = new FatSecretOAuth1Client(new HttpClient(handler), "consumerKey", "consumerSecret");

        var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.CallApiAsync(new Dictionary<string, string> { ["method"] = "foods.search" }));

        Assert.That(ex!.Message, Does.Contain("Invalid signature: abc"));
    }
}
