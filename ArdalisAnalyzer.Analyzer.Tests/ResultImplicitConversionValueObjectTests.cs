using Microsoft.CodeAnalysis.Testing;
using Verify = ArdalisAnalyzer.Analyzer.Tests.CSharpAnalyzerVerifier<
    ArdalisAnalyzer.Analyzer.ResultImplicitConversionAnalyzer>;

namespace ArdalisAnalyzer.Analyzer.Tests;

public class ResultImplicitConversionValueObjectTests
{
    // =================================================================
    //  SHOULD WARN — Value Object Create() with implicit conversion
    // =================================================================

    [Fact]
    public async Task ValueObject_CreateInTernary_ImplicitConversion_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Retailer
            {
                public string Name { get; }
                private Retailer(string name) => Name = name;

                public static Result<Retailer> Create(string value)
                {
                    if (string.IsNullOrEmpty(value))
                        return Result<Retailer>.Error("Retailer name is required");
                    return Result<Retailer>.Success(new Retailer(value));
                }
            }

            class Order
            {
                public Retailer Retailer { get; set; }

                public void FromEntity(string retailerName)
                {
                    Retailer = {|#0:Retailer.Create(retailerName)|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002")
                .WithLocation(0)
                .WithArguments("Retailer.Create(retailerName)", "Retailer"));
    }

    [Fact]
    public async Task ValueObject_CreateInTernaryWithNullCheck_ImplicitConversion_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Retailer
            {
                public string Name { get; }
                private Retailer(string name) => Name = name;

                public static Result<Retailer> Create(string value)
                {
                    if (string.IsNullOrEmpty(value))
                        return Result<Retailer>.Error("Retailer name is required");
                    return Result<Retailer>.Success(new Retailer(value));
                }
            }

            class Order
            {
                public Retailer Retailer { get; set; }

                public void FromEntity(string name)
                {
                    // Ternary checks the input string, NOT the Result status — still unsafe
                    Retailer = {|#0:name is not null ? Retailer.Create(name) : null|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002")
                .WithLocation(0)
                .WithArguments("name is not null ? Retailer.Create(name) : null", "Retailer"));
    }

    [Fact]
    public async Task ValueObject_CreateAssignment_ImplicitConversion_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Email
            {
                public string Address { get; }
                private Email(string address) => Address = address;

                public static Result<Email> Create(string value)
                {
                    if (!value.Contains("@"))
                        return Result<Email>.Error("Invalid email");
                    return Result<Email>.Success(new Email(value));
                }
            }

            class User
            {
                public Email Email { get; set; }

                public void SetEmail(string raw)
                {
                    Email = {|#0:Email.Create(raw)|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002")
                .WithLocation(0)
                .WithArguments("Email.Create(raw)", "Email"));
    }

    [Fact]
    public async Task ValueObject_CreateReturn_ImplicitConversion_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Money
            {
                public decimal Amount { get; }
                private Money(decimal amount) => Amount = amount;

                public static Result<Money> Create(decimal value)
                {
                    if (value < 0)
                        return Result<Money>.Error("Amount cannot be negative");
                    return Result<Money>.Success(new Money(value));
                }
            }

            class Service
            {
                public Money GetPrice(decimal raw)
                {
                    return {|#0:Money.Create(raw)|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002")
                .WithLocation(0)
                .WithArguments("Money.Create(raw)", "Money"));
    }

    // =================================================================
    //  SHOULD NOT WARN — Value Object Create() with proper check
    // =================================================================

    // =================================================================
    //  SHOULD WARN — Target-typed conditional (C# 9+)
    //  Compiler resolves implicit conversion per-branch, not on the
    //  whole ternary. The analyzer must inspect WhenTrue/WhenFalse.
    // =================================================================

    [Fact]
    public async Task ValueObject_TargetTypedTernary_ImplicitConversion_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Retailer
            {
                public string Name { get; }
                private Retailer(string name) => Name = name;

                public static Result<Retailer> Create(string value)
                {
                    if (string.IsNullOrEmpty(value))
                        return Result<Retailer>.Error("Retailer name is required");
                    return Result<Retailer>.Success(new Retailer(value));
                }
            }

            class OrderEntity { public string Retailer { get; set; } }

            class Order
            {
                public Retailer Retailer { get; set; }

                public static Order FromEntity(OrderEntity entity)
                {
                    var order = new Order();
                    order.Retailer = {|#0:entity.Retailer is not null ? Retailer.Create(entity.Retailer) : null|};
                    return order;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002")
                .WithLocation(0)
                .WithArguments("entity.Retailer is not null ? Retailer.Create(entity.Retailer) : null", "Retailer"));
    }

    [Fact]
    public async Task ValueObject_TargetTypedTernary_MultipleValueObjects_Warns()
    {
        var code = """
            using Ardalis.Result;

            class Email
            {
                public string Address { get; }
                private Email(string address) => Address = address;
                public static Result<Email> Create(string v) =>
                    v.Contains("@") ? Result<Email>.Success(new Email(v)) : Result<Email>.Error("bad");
            }

            class Phone
            {
                public string Number { get; }
                private Phone(string number) => Number = number;
                public static Result<Phone> Create(string v) =>
                    v.Length > 5 ? Result<Phone>.Success(new Phone(v)) : Result<Phone>.Error("bad");
            }

            class Contact
            {
                public Email Email { get; set; }
                public Phone Phone { get; set; }

                public void Map(string email, string phone)
                {
                    Email = {|#0:email is not null ? Email.Create(email) : null|};
                    Phone = {|#1:phone is not null ? Phone.Create(phone) : null|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code,
            Verify.Diagnostic("ARDRES002")
                .WithLocation(0).WithArguments("email is not null ? Email.Create(email) : null", "Email"),
            Verify.Diagnostic("ARDRES002")
                .WithLocation(1).WithArguments("phone is not null ? Phone.Create(phone) : null", "Phone"));
    }

    // =================================================================
    //  SHOULD NOT WARN — Value Object Create() with proper check
    // =================================================================

    [Fact]
    public async Task ValueObject_CreateWithGuardClause_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Retailer
            {
                public string Name { get; }
                private Retailer(string name) => Name = name;

                public static Result<Retailer> Create(string value)
                {
                    if (string.IsNullOrEmpty(value))
                        return Result<Retailer>.Error("Retailer name is required");
                    return Result<Retailer>.Success(new Retailer(value));
                }
            }

            class Order
            {
                public Retailer Retailer { get; set; }

                public Result<Order> FromEntity(string retailerName)
                {
                    var retailerResult = Retailer.Create(retailerName);
                    if (!retailerResult.IsSuccess)
                        return Result<Order>.Error("Invalid retailer");
                    Retailer = retailerResult.Value;
                    return Result<Order>.Success(this);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task ValueObject_CreateWithTernaryOnResult_NoWarning()
    {
        var code = """
            using Ardalis.Result;

            class Retailer
            {
                public string Name { get; }
                private Retailer(string name) => Name = name;

                public static Result<Retailer> Create(string value)
                {
                    if (string.IsNullOrEmpty(value))
                        return Result<Retailer>.Error("Retailer name is required");
                    return Result<Retailer>.Success(new Retailer(value));
                }
            }

            class Order
            {
                public Retailer Retailer { get; set; }

                public void FromEntity(string retailerName)
                {
                    var retailerResult = Retailer.Create(retailerName);
                    Retailer = retailerResult.IsSuccess ? retailerResult.Value : null;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(code);
    }
}
