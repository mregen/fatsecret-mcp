// SPDX-License-Identifier: MIT

namespace FatSecretMcp.Tests.TestSupport;

/// <summary>
/// Captures the last outgoing request and returns a canned response, so OAuth1 client tests can assert
/// on exactly what was sent (method, URL, signed query params) without hitting the real FatSecret API.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(responder(request));
    }
}
