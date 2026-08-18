// SPDX-License-Identifier: MIT

using FatSecretMcp.Auth;

namespace FatSecretMcp.Tests;

[TestFixture]
public class OAuth1SignerTests
{
    // From the OAuth Core 1.0 spec's worked example (hueniverse.com/oauth), reused because it's the
    // one independent, known-correct HMAC-SHA1 OAuth1 signature anyone can check this test against.
    [Test]
    public void Sign_MatchesOAuthCoreSpecExample()
    {
        var parameters = new Dictionary<string, string> {
            ["oauth_consumer_key"] = "dpf43f3p2l4k3l03",
            ["oauth_token"] = "nnch734d00sl2jdk",
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = "1191242096",
            ["oauth_nonce"] = "kllo9940pd9333jh",
            ["oauth_version"] = "1.0",
            ["file"] = "vacation.jpg",
            ["size"] = "original",
        };

        var signature = OAuth1Signer.Sign(
            "GET", "http://photos.example.net/photos", parameters,
            "kd94hf93k423kf44", "pfkkdhi9sl3r4s00");

        Assert.That(signature, Is.EqualTo("tR3+Ty81lMeYAr/Fid0kMTYa/WM="));
    }

    [Test]
    public void Sign_WithoutTokenSecret_StillSigns()
    {
        var parameters = new Dictionary<string, string> {
            ["oauth_consumer_key"] = "key",
            ["oauth_nonce"] = "abc123",
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = "1000000000",
            ["oauth_version"] = "1.0",
        };

        var signature = OAuth1Signer.Sign("GET", "https://example.com/resource", parameters, "consumersecret");

        // Not a known test vector - just proving the 2-legged (no token secret) path signs without throwing
        // and is deterministic for identical inputs.
        var signatureAgain = OAuth1Signer.Sign("GET", "https://example.com/resource", parameters, "consumersecret");
        Assert.That(signature, Is.EqualTo(signatureAgain));
        Assert.That(signature, Is.Not.Empty);
    }

    [Test]
    public void Sign_DifferentHttpMethod_ProducesDifferentSignature()
    {
        var parameters = new Dictionary<string, string> {
            ["oauth_consumer_key"] = "key",
            ["oauth_nonce"] = "abc123",
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = "1000000000",
            ["oauth_version"] = "1.0",
        };

        var getSignature = OAuth1Signer.Sign("GET", "https://example.com/resource", parameters, "secret");
        var postSignature = OAuth1Signer.Sign("POST", "https://example.com/resource", parameters, "secret");

        // Regression guard for the FatSecret docs bug we hit: request_token genuinely requires GET, not the
        // POST the docs describe, and it only mattered because GET/POST produce different signatures.
        Assert.That(getSignature, Is.Not.EqualTo(postSignature));
    }

    [TestCase("hello", "hello")]
    [TestCase("hello world", "hello%20world")]
    [TestCase("a!b", "a%21b")]
    [TestCase("a*b", "a%2Ab")]
    [TestCase("a'b", "a%27b")]
    [TestCase("a(b)", "a%28b%29")]
    [TestCase("a&b=c", "a%26b%3Dc")]
    [TestCase("a.b-c_d~e", "a.b-c_d~e")]
    public void PercentEncode_MatchesRfc3986(string input, string expected)
    {
        Assert.That(OAuth1Signer.PercentEncode(input), Is.EqualTo(expected));
    }

    [Test]
    public void BuildBaseParams_OmitsTokenWhenNotProvided()
    {
        var parameters = OAuth1Signer.BuildBaseParams("consumerKey");

        Assert.That(parameters, Does.Not.ContainKey("oauth_token"));
        Assert.That(parameters["oauth_consumer_key"], Is.EqualTo("consumerKey"));
        Assert.That(parameters["oauth_signature_method"], Is.EqualTo("HMAC-SHA1"));
        Assert.That(parameters["oauth_version"], Is.EqualTo("1.0"));
    }

    [Test]
    public void BuildBaseParams_IncludesTokenWhenProvided()
    {
        var parameters = OAuth1Signer.BuildBaseParams("consumerKey", "someToken");

        Assert.That(parameters["oauth_token"], Is.EqualTo("someToken"));
    }

    [Test]
    public void BuildBaseParams_NonceIsUniquePerCall()
    {
        var first = OAuth1Signer.BuildBaseParams("consumerKey");
        var second = OAuth1Signer.BuildBaseParams("consumerKey");

        Assert.That(first["oauth_nonce"], Is.Not.EqualTo(second["oauth_nonce"]));
    }

    [Test]
    public void BuildBaseParams_TimestampIsCurrentUnixTime()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var parameters = OAuth1Signer.BuildBaseParams("consumerKey");
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var timestamp = long.Parse(parameters["oauth_timestamp"]);
        Assert.That(timestamp, Is.InRange(before, after));
    }
}
