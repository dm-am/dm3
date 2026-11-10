using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>Each method below has a shape a pattern reader misses; the fields and the property, shapes it takes for one.</remarks>
public class SourceTextShould
{
    [Fact]
    public void FindEveryMethodWhateverShapeItsDeclarationTakes()
    {
        const string source = """
            namespace N;

            public class C(int seed) : Base(seed)
            {
                private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
                {
                    "}",
                };

                private readonly Box _box = new Box(1) { Size = 2 };

                public int Count { get; set; } = 1;

                #region Generic
                public T Make<T>() where T : new()
                {
                    return new T();
                }
                #endregion

                [Values(new[] { 1, 2 })]
                public (int A, int B) Pair(int x)
                {
                    return (x, x);
                }

                public void Run([Range(1, 2)] int a, int b = default(int), Func<(int, int)>? f = null)
                {
                    int Local() => a;
                }

                private Action Later() => () =>
                {
                    Run(1);
                };

                void Quiet()
                {
                    // }
                }

                protected C(int a, int b) : this(a)
                {
                }
            }
            """;

        SourceText.MemberSpans(source).Should().Equal(
            new SourceText.Span("Make", 15, 4),
            new SourceText.Span("Pair", 22, 4),
            new SourceText.Span("Run", 27, 4),
            new SourceText.Span("Later", 32, 4),
            new SourceText.Span("Quiet", 37, 4),
            new SourceText.Span("C", 42, 3));
    }
}
