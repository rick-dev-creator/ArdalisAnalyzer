# Contributing to ArdalisAnalyzer

Thanks for your interest in contributing!

## Getting Started

1. Fork the repository
2. Clone your fork
3. Create a branch for your change

## Building

```bash
dotnet build ArdalisAnalyzer.slnx
```

## Running Tests

```bash
dotnet test ArdalisAnalyzer.slnx
```

All 57 tests must pass before submitting a pull request.

## Adding a New Guard Pattern

If you find a valid guard pattern that causes a false positive:

1. Add a test case in `ResultValueAccessAnalyzerTests.cs` that demonstrates the pattern
2. Verify the test fails (the analyzer incorrectly flags it)
3. Update `ResultValueAccessAnalyzer.cs` to recognize the pattern
4. Verify all tests pass

## Adding a New Detection Case

If you find an unsafe `.Value` access the analyzer misses:

1. Add a test case that expects the diagnostic
2. Verify the test fails (the analyzer doesn't flag it)
3. Update the analyzer logic
4. Verify all tests pass

## Pull Requests

- Keep changes focused — one fix or feature per PR
- Include tests for any new behavior
- Ensure all existing tests still pass
- Update the README if the change affects usage or detection tables
