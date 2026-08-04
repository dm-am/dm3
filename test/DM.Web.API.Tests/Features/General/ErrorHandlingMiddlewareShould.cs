using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// A failure past the point of no return is recorded, not answered.
/// </summary>
/// <remarks>
/// Once the headers are on the wire the status cannot be set: the setter throws, and
/// that second exception escapes the middleware unhandled. The client gets a reset in
/// the middle of the JSON, and the branches that do not log - a validation failure, a
/// mapped HTTP exception - lose the cause entirely. The scenario is reachable: a
/// listing whose queryable materialises while the response is being written can fault
/// after the first flush.
/// </remarks>
public class ErrorHandlingMiddlewareShould : UnitTestBase
{
    [Fact]
    public async Task RaiseNothingFurtherWhenTheResponseHasAlreadyStarted()
    {
        var context = ContextWithStartedResponse(out var feature);

        var act = () => Invoke(context);

        await act.Should().NotThrowAsync(
            "setting the status of a started response throws, and that exception leaves " +
            "the middleware with nobody above it to handle it");
        feature.StatusCodeAssignments.Should().Be(0);
        feature.Aborted.Should().BeTrue(
            "the connection is closed on purpose rather than by a secondary fault");
    }

    private static Task Invoke(HttpContext context)
    {
        var middleware = new ErrorHandlingMiddleware(_ => throw new InvalidOperationException("boom"));
        return middleware.InvokeAsync(
            context,
            NullLogger<ErrorHandlingMiddleware>.Instance,
            new AnonymousIdentityProvider(),
            new FixedCorrelationTokenProvider(),
            null!);
    }

    private static HttpContext ContextWithStartedResponse(out StartedResponseFeature feature)
    {
        feature = new StartedResponseFeature();
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(feature);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        features.Set<IHttpRequestLifetimeFeature>(feature);
        return new DefaultHttpContext(features);
    }

    /// <summary>
    /// Kestrel refuses a status change once the headers are gone, and counts the
    /// attempt so the rule can tell "guarded" from "happened to work".
    /// </summary>
    private sealed class StartedResponseFeature : IHttpResponseFeature, IHttpRequestLifetimeFeature
    {
        private int statusCode = StatusCodes.Status200OK;

        public int StatusCodeAssignments { get; private set; }

        public bool Aborted { get; private set; }

        public int StatusCode
        {
            get => statusCode;
            set
            {
                StatusCodeAssignments++;
                throw new InvalidOperationException(
                    "StatusCode cannot be set, response has already started.");
            }
        }

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public CancellationToken RequestAborted { get; set; } = CancellationToken.None;

        public void Abort() => Aborted = true;
    }

    private sealed class AnonymousIdentityProvider : IIdentityProvider
    {
        // The middleware asks for the identity while answering a request that has
        // none: the provider is the anonymous one, and null is what it holds.
        public IIdentity Current => null!;

        public void Current_set(IIdentity identity)
        {
        }
    }

    private sealed class FixedCorrelationTokenProvider : ICorrelationTokenProvider
    {
        public Guid Current { get; } = Guid.Empty;
    }
}
