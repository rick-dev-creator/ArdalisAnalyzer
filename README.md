# ArdalisAnalyzer

[![NuGet](https://img.shields.io/nuget/v/ArdalisAnalyzer.svg)](https://www.nuget.org/packages/ArdalisAnalyzer/)

A Roslyn analyzer that detects unsafe access to `Result<T>.Value` from [Ardalis.Result](https://github.com/ardalis/Result) without first verifying the result status.

## Installation

```bash
dotnet add package ArdalisAnalyzer --version 1.1.0
```

## The Problem

`Ardalis.Result<T>` wraps a value alongside a status indicating success or failure. The `Value` property is always accessible — even when the result represents an error, a validation failure, or a not-found response. This means code that accesses `.Value` without checking `.IsSuccess` or `.Status` first can:

- **Throw `NullReferenceException`** when calling methods on a null `Value`
- **Silently produce corrupt data** when `Value` is null or default but the code continues as if it were valid

The second case is especially dangerous because nothing crashes — the application just processes garbage.

### Example: The Silent Bug

```csharp
var result = GetUser(id);
var username = result.Value;          // null if result is an error
var greeting = $"Hello, {username}";  // "Hello, " — no crash, just wrong
SendEmail(greeting);                  // sends a broken email
```

No exception. No log. Just a user receiving an email that says "Hello, ".

### Example: The Crash

```csharp
var result = GetUser(id);
var length = result.Value.Length;  // NullReferenceException
```

### Example: LINQ — Errors Mixed In

```csharp
var results = users.Select(id => GetUser(id));
var names = results.Select(r => r.Value).ToList();
// ["Alice", null, "Bob", null] — nulls from failed lookups
```

### Example: Implicit Conversion — The Invisible Bug

`Ardalis.Result<T>` defines an `implicit operator T` that silently extracts `.Value`. This means you can assign a `Result<T>` to a `T` variable without any compiler warning — no `.Value` access is visible in the code:

```csharp
Result<string> result = GetUser(-1);
string name = result;  // compiles fine — implicit conversion extracts Value
// name is null, no exception, no warning
```

This is arguably more dangerous than `.Value` because the developer may not even realize a conversion is happening.

### Example: Chain Extensions That Hide the Problem

A common pattern is to create functional-style extensions like `Then`, `Bind`, or `Map` that chain operations on `Result<T>`. If these extensions access `.Value` internally without checking status, errors propagate silently:

```csharp
public static Result<TOut> Then<TIn, TOut>(
    this Result<TIn> result, Func<TIn, TOut> transform)
{
    // BUG: accesses Value without checking status
    var transformed = transform(result.Value);
    return Result<TOut>.Success(transformed);
}

// Usage — NullReferenceException when GetUser fails:
var upper = GetUser(-1).Then(name => name.ToUpper());
```

## The Solution

This package includes two Roslyn analyzer rules:

- **ARDRES001** — Detects `.Value` access on `Result<T>` without a prior status check
- **ARDRES002** — Detects implicit conversion from `Result<T>` to `T` without a prior status check

Both perform **control flow analysis**, catching violations in direct code, extension methods, LINQ lambdas, delegates, and chain patterns.

### How It Works

#### ARDRES001 — Value Access

The analyzer registers on every `SimpleMemberAccessExpression` in the syntax tree. When it finds a `.Value` access:

1. **Type check** — Verifies the expression type is `Ardalis.Result.Result<T>` by walking the namespace chain (no string allocation).

2. **Single-pass ancestor walk** — Traverses ancestors once, checking for any of these guard patterns:
   - `if (result.IsSuccess) { ... result.Value ... }` — positive if block
   - `switch (result.Status) { case ResultStatus.Ok: ... }` — switch statement
   - `result.Status switch { ResultStatus.Ok => result.Value }` — switch expression
   - `result.IsSuccess ? result.Value : fallback` — ternary
   - `result.IsSuccess && result.Value.Length > 0` — short-circuit `&&`

3. **Guard clause scan** — Walks enclosing blocks looking for early exits before the `.Value` access:
   - `if (!result.IsSuccess) return;`
   - `if (!result.IsSuccess) throw new InvalidOperationException();`
   - `if (result.Status != ResultStatus.Ok) return;`

If none of these guards are found, the analyzer reports **ARDRES001**.

#### ARDRES002 — Implicit Conversion

The analyzer registers on variable declarations, assignments, return statements, and method arguments. When it detects an implicit user-defined conversion:

1. **Source type check** — Verifies the source is `Ardalis.Result.Result<T>`
2. **Conversion direction** — Confirms it's `Result<T>` → `T` (not the safe `T` → `Result<T>` direction)
3. **Guard check** — Uses the same flow analysis as ARDRES001

If unguarded, the analyzer reports **ARDRES002**.

### What It Catches

| Scenario | Detected? |
|----------|-----------|
| `result.Value` — no check | Yes |
| `result.Value.Length` — method on null | Yes |
| `Console.WriteLine(result.Value)` — passed as argument | Yes |
| `GetUser(1).Value` — inline method call | Yes |
| `.Select(r => r.Value)` — LINQ lambda | Yes |
| `from r in results select r.Value` — query syntax | Yes |
| `.Then(name => name.ToUpper())` — chain extension | Yes |
| `Func<Result<T>, T> f = r => r.Value` — delegate | Yes |
| `(r1.Value, r2.Value)` — tuple wrapping | Yes |
| Check on wrong variable: `if (r1.IsSuccess) { r2.Value }` | Yes |
| `string name = result;` — implicit conversion | Yes |
| `return result;` (method returns `T`) — implicit conversion | Yes |
| `Process(result)` (param is `T`) — implicit conversion | Yes |
| `string name = GetUser();` — inline implicit conversion | Yes |

### What It Allows (No False Positives)

| Pattern | Flagged? |
|---------|----------|
| `if (result.IsSuccess) { result.Value }` | No |
| `if (!result.IsSuccess) return;` then `result.Value` | No |
| `if (result.Status == ResultStatus.Ok) { result.Value }` | No |
| `result.IsSuccess ? result.Value : fallback` | No |
| `switch (result.Status) { case Ok: result.Value }` | No |
| `result.Status switch { Ok => result.Value }` | No |
| `result.IsSuccess && result.Value.Length > 0` | No |
| `nullable.Value` / `myObj.Value` — non-Ardalis types | No |
| `return "hello";` (`T` → `Result<T>` direction) | No |
| Guarded implicit: `if (IsSuccess) { string s = result; }` | No |

## Configuration

The default severity is **warning**. To change it, add an `.editorconfig` to your project:

```ini
[*.cs]
# Options: error | warning | suggestion | none
dotnet_diagnostic.ARDRES001.severity = error
dotnet_diagnostic.ARDRES002.severity = error
```

Setting them to `error` breaks the build on any unguarded `.Value` access or implicit conversion — enforcing safe usage at compile time.

## Project Structure

```
ArdalisAnalyzer/
├── ArdalisAnalyzer/                          # Console app with examples
├── ArdalisAnalyzer.Analyzer/                 # Roslyn analyzer (netstandard2.0)
├── ArdalisAnalyzer.Analyzer.Tests/           # Unit tests (57 cases)
└── ArdalisAnalyzer.Analyzer.Tests.LanguageExt/  # Tests with LanguageExt NuGet
```

## Usage

Install via NuGet:

```bash
dotnet add package ArdalisAnalyzer --version 1.1.0
```

Or add directly to your `.csproj`:

```xml
<PackageReference Include="ArdalisAnalyzer" Version="1.1.0" />
```

For local development, you can reference the analyzer project instead:

```xml
<ProjectReference Include="path/to/ArdalisAnalyzer.Analyzer.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Notes

This project was originally developed independently and later simplified and restructured with the assistance of [Claude Code](https://claude.ai/code). Parts of the analyzer implementation, test suite, and documentation were generated or refined during that process.
