using Ardalis.Result;

new ExampleRunner()
    // ── 1. Simple direct access (no guard) ──────────────────────
    .Category("1. Direct Value Access (UNSAFE)")
    .Add("Direct access on success", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(1);
        return $"Value: {r.Value}";
#pragma warning restore ARDRES001
    })
    .Add("Direct access on error", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1);
        return $"Value: '{r.Value}'";
#pragma warning restore ARDRES001
    })
    .Add("Value.Length on error (NullRef)", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1);
        return $"Length: {r.Value.Length}";
#pragma warning restore ARDRES001
    })
    .Add("Value as method argument", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetOrder(-1);
        return $"Processing: {r.Value}";
#pragma warning restore ARDRES001
    })

    // ── 2. Guard patterns (safe — no pragma needed) ─────────────
    .Category("2. Guard Patterns (SAFE)")
    .Add("if (IsSuccess)", () =>
    {
        var r = Fake.GetUser(1);
        if (r.IsSuccess)
            return $"Value: {r.Value}";
        return "Not success";
    })
    .Add("Guard clause: if (!IsSuccess) return", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Guarded exit";
        return $"Value: {r.Value}";
    })
    .Add("Status == ResultStatus.Ok", () =>
    {
        var r = Fake.GetUser(1);
        if (r.Status == ResultStatus.Ok)
            return $"Value: {r.Value}";
        return "Not Ok";
    })
    .Add("Ternary guard", () =>
    {
        var r = Fake.GetUser(1);
        return r.IsSuccess ? $"Value: {r.Value}" : "Fallback";
    })
    .Add("Switch expression on Status", () =>
    {
        var r = Fake.GetUser(1);
        return r.Status switch
        {
            ResultStatus.Ok => $"Value: {r.Value}",
            _ => "Not Ok"
        };
    })
    .Add("Switch statement on Status", () =>
    {
        var r = Fake.GetUser(1);
        switch (r.Status)
        {
            case ResultStatus.Ok:
                return $"Value: {r.Value}";
            default:
                return "Not Ok";
        }
    })
    .Add("Short-circuit && guard", () =>
    {
        var r = Fake.GetUser(1);
        if (r.IsSuccess && r.Value.Length > 0)
            return $"Value: {r.Value}";
        return "Empty or failed";
    })

    // ── 3. Direct access — correct patterns (no pragma needed) ──
    .Category("3. Direct Access (SAFE — correct patterns)")
    .Add("if (IsSuccess) then access", () =>
    {
        var r = Fake.GetUser(1);
        if (r.IsSuccess)
            return $"Value: {r.Value}";
        return $"Error: {string.Join(", ", r.Errors)}";
    })
    .Add("if (IsSuccess) with error result", () =>
    {
        var r = Fake.GetUser(-1);
        if (r.IsSuccess)
            return $"Value: {r.Value}";
        return $"Error: {string.Join(", ", r.Errors)}";
    })
    .Add("Guard clause + Value.Length", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Guarded";
        return $"Length: {r.Value.Length}";
    })
    .Add("Guard clause + pass as argument", () =>
    {
        var r = Fake.GetOrder(5);
        if (!r.IsSuccess) return "Guarded";
        return $"Processing: {r.Value}";
    })

    // ── 4. LINQ (unsafe — cross-lambda flow) ────────────────────
    .Category("4. LINQ Access (UNSAFE)")
    .Add("Select without check", () =>
    {
#pragma warning disable ARDRES001
        var results = new List<Result<string>>
        {
            Result<string>.Success("Alice"),
            Result<string>.Error("fail"),
            Result<string>.Success("Bob")
        };
        var values = results.Select(r => r.Value).ToList();
        return string.Join(", ", values);
#pragma warning restore ARDRES001
    })
    .Add("Where(IsSuccess).Select(Value) — cross-lambda", () =>
    {
#pragma warning disable ARDRES001
        var results = new List<Result<string>>
        {
            Result<string>.Success("Alice"),
            Result<string>.Error("fail"),
            Result<string>.Success("Bob")
        };
        var values = results
            .Where(r => r.IsSuccess)
            .Select(r => r.Value)
            .ToList();
        return string.Join(", ", values);
#pragma warning restore ARDRES001
    })
    .Add("Query syntax without check", () =>
    {
#pragma warning disable ARDRES001
        var results = new List<Result<string>>
        {
            Result<string>.Success("Alice"),
            Result<string>.Error("fail")
        };
        var values = from r in results select r.Value;
        return string.Join(", ", values);
#pragma warning restore ARDRES001
    })

    // ── 5. LINQ (safe — check inside lambda) ────────────────────
    .Category("5. LINQ Access (SAFE)")
    .Add("Select with inline ternary guard", () =>
    {
        var results = new List<Result<string>>
        {
            Result<string>.Success("Alice"),
            Result<string>.Error("fail"),
            Result<string>.Success("Bob")
        };
        var values = results
            .Select(r => r.IsSuccess ? r.Value : "[error]")
            .ToList();
        return string.Join(", ", values);
    })
    .Add("Query syntax with ternary guard", () =>
    {
        var results = new List<Result<string>>
        {
            Result<string>.Success("Alice"),
            Result<string>.Error("fail")
        };
        var values = from r in results
                     select r.IsSuccess ? r.Value : "[error]";
        return string.Join(", ", values);
    })
    .Add("Select with switch expression guard", () =>
    {
        var results = new List<Result<string>>
        {
            Result<string>.Success("Alice"),
            Result<string>.Error("fail"),
            Result<string>.Success("Bob")
        };
        var values = results.Select(r => r.Status switch
        {
            ResultStatus.Ok => r.Value,
            _ => "[error]"
        }).ToList();
        return string.Join(", ", values);
    })

    // ── 6. Custom chain extensions (unsafe) ─────────────────────
    .Category("6. Custom Chain Extensions (UNSAFE)")
    .Add("Then — transform on error result", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1).Then(name => name.ToUpper());
        return $"Value: '{r.Value}'";
#pragma warning restore ARDRES001
    })
    .Add("ThenBind — chain on error result", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1).ThenBind(name => Fake.GetOrder(name.Length));
        return $"Value: '{r.Value}'";
#pragma warning restore ARDRES001
    })
    .Add("Tap — side effect on error result", () =>
    {
#pragma warning disable ARDRES001
        var captured = "";
        Fake.FindProduct(999).Tap(p => captured = p);
        return $"Captured: '{captured}'";
#pragma warning restore ARDRES001
    })
    .Add("Combine — merge two error results", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1).Combine(Fake.GetOrder(-1), (u, o) => $"{u}->{o}");
        return $"Combined: '{r.Value}'";
#pragma warning restore ARDRES001
    })
    .Add("Multi-chain — Then.Then.ThenBind", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1)
            .Then(n => n.Trim())
            .Then(n => n.ToUpper())
            .ThenBind(n => Fake.FindProduct(n.Length));
        return $"Final: '{r.Value}'";
#pragma warning restore ARDRES001
    })
    .Add("UnwrapOr — fallback ignored", () =>
    {
#pragma warning disable ARDRES001
        var val = Fake.GetUser(-1).UnwrapOr("Default");
        return $"Value: '{val}'";
#pragma warning restore ARDRES001
    })
    .Add("Dump — log on error", () =>
    {
#pragma warning disable ARDRES001
        var log = Fake.FindProduct(999).Dump("Lookup");
        return log;
#pragma warning restore ARDRES001
    })

    // ── 7. Custom chain extensions (safe) ────────────────────────
    .Category("7. Custom Chain Extensions (SAFE)")
    .Add("Then with IsSuccess guard", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Guarded";
        var mapped = r.Value.ToUpper();
        return $"Mapped: {mapped}";
    })
    .Add("ThenBind with guard clause", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Guarded";
        var order = Fake.GetOrder(r.Value.Length);
        return order.IsSuccess ? $"Order: {order.Value}" : "Order failed";
    })
    .Add("Combine with both guards", () =>
    {
        var r1 = Fake.GetUser(1);
        var r2 = Fake.GetOrder(5);
        if (r1.IsSuccess && r2.IsSuccess)
            return $"Combined: {r1.Value}->{r2.Value}";
        return "One or both failed";
    })
    .Add("Multi-step with guard at each step", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Step 1 failed";
        var trimmed = r.Value.Trim();
        var upper = trimmed.ToUpper();
        var product = Fake.FindProduct(upper.Length);
        if (!product.IsSuccess) return "Step 2 failed";
        return $"Final: {product.Value}";
    })
    .Add("Ternary chain", () =>
    {
        var r = Fake.GetUser(1);
        var val = r.IsSuccess ? r.Value.ToUpper() : "DEFAULT";
        return $"Value: {val}";
    })

    // ── 8. Wrapping Value into other types (unsafe) ─────────────
    .Category("8. Wrapping Value (UNSAFE)")
    .Add("Wrap in Nullable", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetOrder(-1);
        int? nullable = r.Value;
        return $"Nullable: {nullable}";
#pragma warning restore ARDRES001
    })
    .Add("Wrap in Tuple", () =>
    {
#pragma warning disable ARDRES001
        var r1 = Fake.GetUser(-1);
        var r2 = Fake.GetOrder(-1);
        var tuple = (r1.Value, r2.Value);
        return $"Tuple: ({tuple.Item1}, {tuple.Item2})";
#pragma warning restore ARDRES001
    })
    .Add("Wrap in List", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1);
        var list = new List<string> { r.Value };
        return $"List[0]: '{list[0]}'";
#pragma warning restore ARDRES001
    })
    .Add("Wrap in Dictionary", () =>
    {
#pragma warning disable ARDRES001
        var r = Fake.GetUser(-1);
        var dict = new Dictionary<string, string> { ["user"] = r.Value };
        return $"Dict[user]: '{dict["user"]}'";
#pragma warning restore ARDRES001
    })

    // ── 9. Wrapping Value (safe) ────────────────────────────────
    .Category("9. Wrapping Value (SAFE)")
    .Add("Wrap in Nullable with guard", () =>
    {
        var r = Fake.GetOrder(5);
        if (!r.IsSuccess) return "Guarded";
        int? nullable = r.Value;
        return $"Nullable: {nullable}";
    })
    .Add("Wrap in Tuple with guard", () =>
    {
        var r1 = Fake.GetUser(1);
        var r2 = Fake.GetOrder(5);
        if (r1.IsSuccess && r2.IsSuccess)
        {
            var tuple = (r1.Value, r2.Value);
            return $"Tuple: ({tuple.Item1}, {tuple.Item2})";
        }
        return "One or both failed";
    })
    .Add("Wrap in List with guard", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Guarded";
        var list = new List<string> { r.Value };
        return $"List[0]: '{list[0]}'";
    })
    .Add("Wrap in Dictionary with guard", () =>
    {
        var r = Fake.GetUser(1);
        if (!r.IsSuccess) return "Guarded";
        var dict = new Dictionary<string, string> { ["user"] = r.Value };
        return $"Dict[user]: '{dict["user"]}'";
    })
    .Add("Wrap in Tuple with ternary", () =>
    {
        var r1 = Fake.GetUser(1);
        var r2 = Fake.GetOrder(5);
        var tuple = (
            r1.IsSuccess ? r1.Value : "N/A",
            r2.IsSuccess ? r2.Value : 0
        );
        return $"Tuple: ({tuple.Item1}, {tuple.Item2})";
    })

    // ── 10. Lambdas & delegates (unsafe) ────────────────────────
    .Category("10. Lambdas & Delegates (UNSAFE)")
    .Add("Func extracting Value", () =>
    {
#pragma warning disable ARDRES001
        Func<Result<string>, string> extract = r => r.Value;
        var result = Fake.GetUser(-1);
        return $"Extracted: '{extract(result)}'";
#pragma warning restore ARDRES001
    })
    .Add("Action using Value", () =>
    {
#pragma warning disable ARDRES001
        var output = "";
        Action<Result<string>> log = r => output = r.Value;
        log(Fake.GetUser(-1));
        return $"Logged: '{output}'";
#pragma warning restore ARDRES001
    })

    // ── 11. Lambdas & delegates (safe) ──────────────────────────
    .Category("11. Lambdas & Delegates (SAFE)")
    .Add("Func with ternary guard", () =>
    {
        Func<Result<string>, string> extract = r =>
            r.IsSuccess ? r.Value : "[error]";
        return $"Extracted: '{extract(Fake.GetUser(-1))}'";
    })
    .Add("Func with ternary guard (success)", () =>
    {
        Func<Result<string>, string> extract = r =>
            r.IsSuccess ? r.Value : "[error]";
        return $"Extracted: '{extract(Fake.GetUser(1))}'";
    })
    .Add("Action with IsSuccess guard", () =>
    {
        var output = "";
        Action<Result<string>> log = r =>
        {
            if (r.IsSuccess) output = r.Value;
            else output = "[error]";
        };
        log(Fake.GetUser(-1));
        return $"Logged: '{output}'";
    })
    .Add("Action with IsSuccess guard (success)", () =>
    {
        var output = "";
        Action<Result<string>> log = r =>
        {
            if (r.IsSuccess) output = r.Value;
            else output = "[error]";
        };
        log(Fake.GetUser(1));
        return $"Logged: '{output}'";
    })

    .Run();

// ── Helper methods ──────────────────────────────────────────────
static class Fake
{
    public static Result<string> GetUser(int id)
        => id <= 0 ? Result<string>.Error("Invalid ID") : Result<string>.Success("Ricardo");

    public static Result<int> GetOrder(int id)
        => id <= 0 ? Result<int>.Invalid(new ValidationError("ID must be > 0")) : Result<int>.Success(42);

    public static Result<string> FindProduct(int id)
        => id > 100 ? Result<string>.NotFound("Product not found") : Result<string>.Success("Laptop");
}

// ── Builder ─────────────────────────────────────────────────────
class ExampleRunner
{
    private readonly List<(string Category, string Name, Func<string> Action)> _examples = [];
    private string _currentCategory = "";

    public ExampleRunner Category(string name)
    {
        _currentCategory = name;
        return this;
    }

    public ExampleRunner Add(string name, Func<string> action)
    {
        _examples.Add((_currentCategory, name, action));
        return this;
    }

    public void Run()
    {
        var lastCategory = "";
        var passed = 0;
        var failed = 0;

        foreach (var (category, name, action) in _examples)
        {
            if (category != lastCategory)
            {
                Console.WriteLine();
                Console.WriteLine($"{"",2}=== {category} ===");
                lastCategory = category;
            }

            try
            {
                var result = action();
                Console.WriteLine($"  [OK]   {name,-45} => {result}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [FAIL] {name,-45} => {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  Total: {passed + failed} | Passed: {passed} | Failed: {failed}");
    }
}
