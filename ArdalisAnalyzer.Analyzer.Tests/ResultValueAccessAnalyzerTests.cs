using Microsoft.CodeAnalysis.Testing;
using Verify = ArdalisAnalyzer.Analyzer.Tests.CSharpAnalyzerVerifier<
    ArdalisAnalyzer.Analyzer.ResultValueAccessAnalyzer>;

namespace ArdalisAnalyzer.Analyzer.Tests;

public class ResultValueAccessAnalyzerTests
{
    private const string ResultHelpers = """
        using Ardalis.Result;

        public static class Helpers
        {
            public static Result<string> GetUser(int id)
            {
                if (id <= 0) return Result<string>.Error("Invalid");
                return Result<string>.Success("Ricardo");
            }

            public static Result<int> GetOrder(int id)
            {
                if (id <= 0) return Result<int>.Invalid(new ValidationError("Bad ID"));
                return Result<int>.Success(42);
            }
        }
        """;

    // =================================================================
    //  SHOULD WARN — Simple / Direct access
    // =================================================================

    [Fact]
    public async Task DirectValueAccess_NoCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    var x = {|#0:result.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ValueInStringInterpolation_NoCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    var msg = $"Value: {{|#0:result.Value|}}";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ValuePassedAsArgument_NoCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    System.Console.WriteLine({|#0:result.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ValueMemberAccess_NoCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    var len = {|#0:result.Value|}.Length;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ValueInComparison_NoCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if ({|#0:result.Value|} == "ok") { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ValueAssignedToVariable_NoCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<int>.Success(10);
                    int val = {|#0:result.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — Method return / inline
    // =================================================================

    [Fact]
    public async Task MethodReturnValueAccess_Warns()
    {
        var code = ResultHelpers + """

            class Test
            {
                void M()
                {
                    var x = {|#0:Helpers.GetUser(1).Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("Helpers.GetUser(1)"));
    }

    // =================================================================
    //  SHOULD WARN — Extension methods / chain
    // =================================================================

    [Fact]
    public async Task ExtensionMethod_AccessesValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            static class Ext
            {
                public static Result<TOut> Then<TIn, TOut>(this Result<TIn> result, System.Func<TIn, TOut> f)
                {
                    var v = f({|#0:result.Value|});
                    return Result<TOut>.Success(v);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ExtensionMethod_Tap_AccessesValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            static class Ext
            {
                public static Result<T> Tap<T>(this Result<T> result, System.Action<T> action)
                {
                    action({|#0:result.Value|});
                    return result;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task ExtensionMethod_Combine_BothValuesWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            static class Ext
            {
                public static Result<TOut> Combine<T1, T2, TOut>(
                    this Result<T1> first, Result<T2> second, System.Func<T1, T2, TOut> f)
                {
                    var v = f({|#0:first.Value|}, {|#1:second.Value|});
                    return Result<TOut>.Success(v);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("first"),
            Verify.Diagnostic("ARDRES001").WithLocation(1).WithArguments("second"));
    }

    [Fact]
    public async Task ExtensionMethod_UnwrapOr_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;

            static class Ext
            {
                public static T UnwrapOr<T>(this Result<T> result, T fallback)
                {
                    return {|#0:result.Value|} ?? fallback;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD WARN — LINQ scenarios
    // =================================================================

    [Fact]
    public async Task LinqSelect_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using System.Collections.Generic;
            using System.Linq;

            class Test
            {
                void M()
                {
                    var results = new List<Result<string>>
                    {
                        Result<string>.Success("a"),
                        Result<string>.Error("fail")
                    };

                    var values = results.Select(r => {|#0:r.Value|}).ToList();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    [Fact]
    public async Task LinqWhere_ThenSelect_ValueWithoutCheckInSelect_Warns()
    {
        var code = """
            using Ardalis.Result;
            using System.Collections.Generic;
            using System.Linq;

            class Test
            {
                void M()
                {
                    var results = new List<Result<string>>
                    {
                        Result<string>.Success("a"),
                        Result<string>.Error("fail")
                    };

                    // Where filters, but the lambda in Select still accesses Value without check
                    var values = results
                        .Where(r => r.IsSuccess)
                        .Select(r => {|#0:r.Value|})
                        .ToList();
                }
            }
            """;

        // The analyzer can't track cross-lambda flow (Where → Select are separate lambdas)
        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    [Fact]
    public async Task LinqQuerySyntax_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using System.Collections.Generic;
            using System.Linq;

            class Test
            {
                void M()
                {
                    var results = new List<Result<string>>
                    {
                        Result<string>.Success("a"),
                        Result<string>.Error("fail")
                    };

                    var values = from r in results
                                 select {|#0:r.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    [Fact]
    public async Task LinqFirstOrDefault_ValueAccess_Warns()
    {
        var code = """
            using Ardalis.Result;
            using System.Collections.Generic;
            using System.Linq;

            class Test
            {
                void M()
                {
                    var results = new List<Result<string>> { Result<string>.Success("a") };
                    var first = results.First();
                    var x = {|#0:first.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("first"));
    }

    // =================================================================
    //  SHOULD WARN — Lambda and delegate scenarios
    // =================================================================

    [Fact]
    public async Task LambdaBody_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using System;

            class Test
            {
                void M()
                {
                    Func<Result<string>, string> extract = r => {|#0:r.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    [Fact]
    public async Task ActionDelegate_ValueWithoutCheck_Warns()
    {
        var code = """
            using Ardalis.Result;
            using System;

            class Test
            {
                void M()
                {
                    Action<Result<string>> log = r => Console.WriteLine({|#0:r.Value|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r"));
    }

    // =================================================================
    //  SHOULD WARN — Multiple accesses
    // =================================================================

    [Fact]
    public async Task MultipleValueAccesses_NoCheck_AllWarn()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var r1 = Result<string>.Success("a");
                    var r2 = Result<int>.Success(1);
                    var a = {|#0:r1.Value|};
                    var b = {|#1:r2.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r1"),
            Verify.Diagnostic("ARDRES001").WithLocation(1).WithArguments("r2"));
    }

    // =================================================================
    //  SHOULD WARN — Wrong guard (different variable)
    // =================================================================

    [Fact]
    public async Task GuardOnDifferentVariable_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var r1 = Result<string>.Success("a");
                    var r2 = Result<string>.Error("fail");

                    if (r1.IsSuccess)
                    {
                        var x = {|#0:r2.Value|};
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("r2"));
    }

    // =================================================================
    //  SHOULD NOT WARN — Properly guarded (if IsSuccess)
    // =================================================================

    [Fact]
    public async Task InsideIsSuccessCheck_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (result.IsSuccess)
                    {
                        var x = result.Value;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task IsSuccessEqualsTrue_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (result.IsSuccess == true)
                    {
                        var x = result.Value;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task IsSuccessWithLogicalAnd_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (result.IsSuccess && result.Value.Length > 0)
                    {
                        var x = result.Value;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Guard clause (early return)
    // =================================================================

    [Fact]
    public async Task GuardClause_NegatedIsSuccess_Return_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (!result.IsSuccess) return;
                    var x = result.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task GuardClause_NegatedIsSuccess_Throw_NoWarning()
    {
        var code = """
            using Ardalis.Result;
            using System;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (!result.IsSuccess)
                        throw new InvalidOperationException();
                    var x = result.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task GuardClause_IsSuccessEqualsFalse_Return_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (result.IsSuccess == false) return;
                    var x = result.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task GuardClause_StatusNotEqualsOk_Return_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (result.Status != ResultStatus.Ok) return;
                    var x = result.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task GuardClause_BlockWithReturn_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (!result.IsSuccess)
                    {
                        System.Console.WriteLine("error");
                        return;
                    }
                    var x = result.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Status == ResultStatus.Ok in if
    // =================================================================

    [Fact]
    public async Task StatusEqualsOk_InIf_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (result.Status == ResultStatus.Ok)
                    {
                        var x = result.Value;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — switch statement
    // =================================================================

    [Fact]
    public async Task SwitchStatement_CaseOk_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    switch (result.Status)
                    {
                        case ResultStatus.Ok:
                            var x = result.Value;
                            break;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — switch expression
    // =================================================================

    [Fact]
    public async Task SwitchExpression_OkArm_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    var x = result.Status switch
                    {
                        ResultStatus.Ok => result.Value,
                        _ => "fallback"
                    };
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Ternary / conditional
    // =================================================================

    [Fact]
    public async Task Ternary_IsSuccess_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    var x = result.IsSuccess ? result.Value : "fallback";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Extension method WITH proper guard
    // =================================================================

    [Fact]
    public async Task ExtensionMethod_WithIsSuccessCheck_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            static class Ext
            {
                public static Result<TOut> SafeThen<TIn, TOut>(
                    this Result<TIn> result, System.Func<TIn, TOut> f)
                {
                    if (!result.IsSuccess)
                        return Result<TOut>.Error("fail");
                    return Result<TOut>.Success(f(result.Value));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task ExtensionMethod_WithStatusCheck_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            static class Ext
            {
                public static Result<TOut> SafeThen<TIn, TOut>(
                    this Result<TIn> result, System.Func<TIn, TOut> f)
                {
                    if (result.Status != ResultStatus.Ok)
                        return Result<TOut>.Error("fail");
                    return Result<TOut>.Success(f(result.Value));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Non-Ardalis Result types
    // =================================================================

    [Fact]
    public async Task NonArdalisType_WithValueProperty_NoWarning()
    {
        var code = """
            class MyResult<T>
            {
                public T Value { get; set; }
            }

            class Test
            {
                void M()
                {
                    var result = new MyResult<string> { Value = "ok" };
                    var x = result.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task NullableValueType_NoWarning()
    {
        var code = """
            class Test
            {
                void M()
                {
                    int? x = 5;
                    var y = x.Value;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD WARN — Null-conditional (?.) and null-coalescing (??)
    //  These do NOT check IsSuccess — they only handle null references
    // =================================================================

    [Fact]
    public async Task NullConditional_ValueAccess_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    Result<string> result = Result<string>.Error("fail");
                    var x = {|#0:result?.Value|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task NullConditional_ValueWithMemberAccess_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    Result<string> result = Result<string>.Error("fail");
                    var x = {|#0:result?.Value?.Length|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task NullCoalescing_ValueAccess_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Error("fail");
                    var x = {|#0:result.Value|} ?? "default";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    [Fact]
    public async Task NullConditional_WithNullCoalescing_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    Result<string> result = Result<string>.Error("fail");
                    var x = {|#0:result?.Value|} ?? "default";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES001").WithLocation(0).WithArguments("result"));
    }

    // =================================================================
    //  SHOULD NOT WARN — Null-conditional with proper guard
    // =================================================================

    [Fact]
    public async Task NullConditional_WithIsSuccessGuard_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    Result<string> result = Result<string>.Success("ok");
                    if (result.IsSuccess)
                    {
                        var x = result?.Value;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task NullConditional_WithGuardClause_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    Result<string> result = Result<string>.Success("ok");
                    if (!result.IsSuccess) return;
                    var x = result?.Value ?? "fallback";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Nested guard in outer scope
    // =================================================================

    [Fact]
    public async Task NestedGuardClause_OuterScope_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                string M()
                {
                    var result = Result<string>.Success("ok");
                    if (!result.IsSuccess) return "err";

                    if (result.Value.Length > 3)
                    {
                        return result.Value;
                    }
                    return result.Value.ToUpper();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }
}
