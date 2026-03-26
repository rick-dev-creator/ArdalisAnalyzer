using Microsoft.CodeAnalysis.Testing;
using Verify = ArdalisAnalyzer.Analyzer.Tests.CSharpAnalyzerVerifier<
    ArdalisAnalyzer.Analyzer.ResultImplicitConversionAnalyzer>;

namespace ArdalisAnalyzer.Analyzer.Tests;

public class ResultImplicitConversionAnalyzerTests
{
    // =================================================================
    //  SHOULD WARN — Variable assignment
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_VariableDeclaration_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    string name = {|#0:result|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("result", "string"));
    }

    [Fact]
    public async Task ImplicitConversion_IntType_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<int>.Success(42);
                    int val = {|#0:result|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("result", "int"));
    }

    // =================================================================
    //  SHOULD WARN — Assignment expression
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_Assignment_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    string name;
                    name = {|#0:result|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("result", "string"));
    }

    // =================================================================
    //  SHOULD WARN — Return statement
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_Return_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                string M()
                {
                    var result = Result<string>.Success("ok");
                    return {|#0:result|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("result", "string"));
    }

    // =================================================================
    //  SHOULD WARN — Method argument
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_MethodArgument_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    Process({|#0:result|});
                }

                void Process(string value) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("result", "string"));
    }

    // =================================================================
    //  SHOULD WARN — Inline method call
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_InlineMethodCall_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    string name = {|#0:GetUser()|};
                }

                Result<string> GetUser() => Result<string>.Success("ok");
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("GetUser()", "string"));
    }

    // =================================================================
    //  SHOULD WARN — Multiple conversions
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_Multiple_AllWarn()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var r1 = Result<string>.Success("a");
                    var r2 = Result<int>.Success(1);
                    string a = {|#0:r1|};
                    int b = {|#1:r2|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("r1", "string"),
            Verify.Diagnostic("ARDRES002").WithLocation(1).WithArguments("r2", "int"));
    }

    // =================================================================
    //  SHOULD WARN — Wrong guard variable
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_GuardOnWrongVariable_Warns()
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
                        string val = {|#0:r2|};
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002").WithLocation(0).WithArguments("r2", "string"));
    }

    // =================================================================
    //  SHOULD NOT WARN — Guarded with if (IsSuccess)
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_InsideIsSuccessCheck_NoWarning()
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
                        string name = result;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Guard clause
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_GuardClause_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    if (!result.IsSuccess) return;
                    string name = result;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Guard clause with return
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_GuardClauseReturn_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                string M()
                {
                    var result = Result<string>.Success("ok");
                    if (!result.IsSuccess) return "error";
                    return result;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Status check
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_StatusEqualsOk_NoWarning()
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
                        string name = result;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Ternary
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_Ternary_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                void M()
                {
                    var result = Result<string>.Success("ok");
                    string name = result.IsSuccess ? result : "fallback";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — T to Result<T> (safe direction)
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_ValueToResult_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Test
            {
                Result<string> M()
                {
                    return "hello";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    // =================================================================
    //  SHOULD NOT WARN — Non-Ardalis types
    // =================================================================

    [Fact]
    public async Task ImplicitConversion_NonArdalisType_NoWarning()
    {
        var code = """
            class Wrapper<T>
            {
                public T Value { get; }
                public Wrapper(T value) { Value = value; }
                public static implicit operator T(Wrapper<T> w) => w.Value;
            }

            class Test
            {
                void M()
                {
                    var w = new Wrapper<string>("ok");
                    string name = w;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }
}
