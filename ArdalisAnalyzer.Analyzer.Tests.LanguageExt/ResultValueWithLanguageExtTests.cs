using Microsoft.CodeAnalysis.Testing;
using Verify = ArdalisAnalyzer.Analyzer.Tests.LanguageExt.CSharpAnalyzerVerifier<
    ArdalisAnalyzer.Analyzer.ResultValueAccessAnalyzer>;

namespace ArdalisAnalyzer.Analyzer.Tests.LanguageExt;

public class ResultValueWithLanguageExtTests
{
    // =================================================================
    //  SHOULD WARN — Using LanguageExt Pipe to chain Value access
    // =================================================================

    [Fact]
    public async Task Pipe_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    var upper = {|#0:result.Value|}.Apply(s => s.ToUpper());
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Using LanguageExt Map on Value
    // =================================================================

    [Fact]
    public async Task LanguageExtMap_OnValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    var opt = Some({|#0:result.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Wrapping Value in Option without check
    // =================================================================

    [Fact]
    public async Task WrapValueInOption_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    Option<string> opt = Optional({|#0:result.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Value in LanguageExt Lst constructor
    // =================================================================

    [Fact]
    public async Task ValueInLstConstructor_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    var list = List({|#0:result.Value|}, "world");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Using Value with LanguageExt Either
    // =================================================================

    [Fact]
    public async Task ValueInEitherRight_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    Either<string, string> either = Right({|#0:result.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Custom extension using LanguageExt + Value
    // =================================================================

    [Fact]
    public async Task CustomExtension_ToOption_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            static class ResultToLanguageExt
            {
                public static Option<T> ToOption<T>(this Result<T> result)
                {
                    return Optional({|#0:result.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task CustomExtension_ToEither_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            static class ResultToLanguageExt
            {
                public static Either<string, T> ToEither<T>(this Result<T> result)
                {
                    return Right({|#0:result.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — LanguageExt Seq/Map with Value
    // =================================================================

    [Fact]
    public async Task ValueInSeqMap_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var results = Seq(
                        Result<string>.Success("a"),
                        Result<string>.Error("fail")
                    );

                    var values = results.Map(r => {|#0:r.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    [Fact]
    public async Task ValueInSeqFilter_ThenMap_StillWarns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var results = Seq(
                        Result<string>.Success("a"),
                        Result<string>.Error("fail")
                    );

                    // Filter + Map are separate lambdas, analyzer can't track cross-lambda flow
                    var values = results
                        .Filter(r => r.IsSuccess)
                        .Map(r => {|#0:r.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    // =================================================================
    //  SHOULD WARN — LanguageExt HashMap with Value as key/value
    // =================================================================

    [Fact]
    public async Task ValueAsHashMapEntry_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var key = Result<string>.Success("key");
                    var val = Result<int>.Success(42);
                    var map = HashMap(({|#0:key.Value|}, {|#1:val.Value|}));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("key"),
            Verify.Diagnostic("ARDRES001").WithLocation(1).WithArguments("val"));
    }

    // =================================================================
    //  SHOULD WARN — LanguageExt Match/fold receiving Value
    // =================================================================

    [Fact]
    public async Task OptionMatchReceivingValue_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    Option<string> opt = Some("test");

                    var output = opt.Match(
                        Some: s => s + {|#0:result.Value|},
                        None: () => "none"
                    );
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — LanguageExt Try wrapping Value
    // =================================================================

    [Fact]
    public async Task TryWrappingValue_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    var t = Try(() => {|#0:result.Value|}.ToUpper());
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Composing multiple results with LanguageExt
    // =================================================================

    [Fact]
    public async Task MultipleResultsTuple_WithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var r1 = Result<string>.Success("a");
                    var r2 = Result<int>.Success(1);
                    var tuple = ({|#0:r1.Value|}, {|#1:r2.Value|});
                    var mapped = tuple.Map((s, i) => $"{s}:{i}");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r1"),
            Verify.Diagnostic("ARDRES001").WithLocation(1).WithArguments("r2"));
    }

    // =================================================================
    //  SHOULD NOT WARN — Proper guard + LanguageExt usage
    // =================================================================

    [Fact]
    public async Task GuardedThenWrappedInOption_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    if (result.IsSuccess)
                    {
                        Option<string> opt = Some(result.Value);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task GuardClauseThenLanguageExtMap_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                Option<string> M()
                {
                    var result = Result<string>.Success("hello");
                    if (!result.IsSuccess) return None;
                    return Some(result.Value);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task SafeExtension_ToOption_WithGuard_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            static class ResultToLanguageExt
            {
                public static Option<T> ToOption<T>(this Result<T> result)
                {
                    if (!result.IsSuccess) return None;
                    return Some(result.Value);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task SafeExtension_ToEither_WithGuard_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            static class ResultToLanguageExt
            {
                public static Either<string, T> ToEither<T>(this Result<T> result)
                {
                    if (result.IsSuccess)
                        return Right(result.Value);
                    return Left(string.Join(", ", result.Errors));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task TernaryGuard_WithLanguageExt_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("hello");
                    Option<string> opt = result.IsSuccess
                        ? Some(result.Value)
                        : None;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task SwitchExpression_WithLanguageExt_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                Option<string> M()
                {
                    var result = Result<string>.Success("hello");
                    return result.Status switch
                    {
                        ResultStatus.Ok => Some(result.Value),
                        _ => None
                    };
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — LanguageExt types (not Ardalis.Result)
    // =================================================================

    [Fact]
    public async Task LanguageExtOptionMatch_NotArdalisResult_NoWarning()
    {
        var code = """
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    Option<string> opt = Some("hello");
                    var val = opt.Match(Some: s => s, None: () => "none");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task LanguageExtEitherMatch_NotArdalisResult_NoWarning()
    {
        var code = """
            using LanguageExt;
            using static LanguageExt.Prelude;

            class Test
            {
                void M()
                {
                    Either<string, int> either = Right(42);
                    var val = either.Match(Right: x => x, Left: _ => 0);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }
}
