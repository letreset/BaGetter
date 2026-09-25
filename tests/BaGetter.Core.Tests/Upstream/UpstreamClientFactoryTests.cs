using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core.Configuration;
using BaGetter.Core.Entities;
using BaGetter.Core.Feeds;
using BaGetter.Core.Upstream;
using BaGetter.Core.Upstream.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BaGetter.Core.Tests.Upstream;

public class UpstreamClientFactoryTests
{
    public class CreateForFeed : FactsBase
    {
        [Fact]
        public void ReturnsDisabledClientWhenMirrorIsDisabled()
        {
            var feed = MirrorFeed();
            feed.MirrorEnabled = false;

            var result = Target.CreateForFeed(feed);

            Assert.IsType<DisabledUpstreamClient>(result);
        }

        [Fact]
        public async Task SendsBasicAuthHeader()
        {
            var feed = MirrorFeed();
            feed.MirrorAuthType = MirrorAuthenticationType.Basic;
            feed.MirrorAuthUsername = "user";
            feed.MirrorAuthPassword = "password";

            await Target.CreateForFeed(feed).ListPackageVersionsAsync("Package", CancellationToken.None);

            var request = Assert.Single(Handler.Requests);
            Assert.Equal("Basic", request.Headers.Authorization.Scheme);
            Assert.Equal("dXNlcjpwYXNzd29yZA==", request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task SendsBearerAuthHeader()
        {
            var feed = MirrorFeed();
            feed.MirrorAuthType = MirrorAuthenticationType.Bearer;
            feed.MirrorAuthToken = "token";

            await Target.CreateForFeed(feed).ListPackageVersionsAsync("Package", CancellationToken.None);

            var request = Assert.Single(Handler.Requests);
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            Assert.Equal("token", request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task SendsCustomHeadersExceptBlockedOnes()
        {
            var feed = MirrorFeed();
            feed.MirrorAuthType = MirrorAuthenticationType.Custom;
            feed.MirrorAuthCustomHeaders = "{\"X-Api-Key\":\"secret\",\"Cookie\":\"evil\"}";

            await Target.CreateForFeed(feed).ListPackageVersionsAsync("Package", CancellationToken.None);

            var request = Assert.Single(Handler.Requests);
            Assert.Equal(new[] { "secret" }, request.Headers.GetValues("X-Api-Key"));
            Assert.False(request.Headers.Contains("Cookie"));
        }
    }

    public class FactsBase
    {
        protected readonly RecordingHandler Handler = new();
        protected readonly UpstreamClientFactory Target;

        protected FactsBase()
        {
            var options = new Mock<IOptionsSnapshot<BaGetterOptions>>();
            options.Setup(o => o.Value).Returns(new BaGetterOptions());

            Target = new UpstreamClientFactory(
                new FeedSettingsResolver(options.Object),
                new DisabledUpstreamClient(),
                NullLoggerFactory.Instance,
                Handler);
        }

        protected static Feed MirrorFeed()
        {
            return new Feed
            {
                Id = Guid.NewGuid(),
                Slug = "feed",
                MirrorEnabled = true,
                MirrorPackageSource = "https://upstream.test/v3/index.json",
            };
        }
    }

    public class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
