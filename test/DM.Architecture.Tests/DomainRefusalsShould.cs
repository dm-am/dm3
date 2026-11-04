using System;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A refusal the caller can act on, not one the host reports as its own fault.
/// </summary>
/// <remarks>
/// The error middleware maps HttpException and nothing else, so any other refusal answers 500.
/// Scanned as source: the type is either named in a throw or it is not.
/// </remarks>
public class DomainRefusalsShould
{
    [Fact]
    public void BeThrownAsSomethingTheErrorMiddlewareUnderstands()
    {
        var sources = RepositoryFiles.ProductionSources().ToList();
        sources.Should().HaveCountGreaterThan(200,
            "a file walk that stops matching turns this rule green by checking nothing");

        // Read as code: a paragraph naming the exception is not a throw of it.
        sources
            .Where(file => SourceText.ReadCode(file).Contains("throw new UnauthorizedAccessException", StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .Should().BeEmpty(
                "the middleware maps HttpException and its kin, so anything else answers a " +
                "caller who merely lacks rights with 500 and a critical log line");
    }
}
