# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-03-26

### Added
- **ARDRES002**: New rule that detects implicit conversion from `Result<T>` to `T` without checking `IsSuccess` or `Status`. Covers variable declarations, assignments, return statements, and method arguments.
- **Null-conditional (`?.`) detection**: ARDRES001 now detects `result?.Value`, `result?.Value?.Length`, and `result?.Value ?? "default"` patterns.
- **Null-coalescing (`??`) detection**: `result.Value ?? "default"` was already detected; now `?.` combined with `??` is also caught.
- Shared flow analysis helpers (`ResultAnalyzerHelpers`) used by both ARDRES001 and ARDRES002.
- 21 new unit tests (15 for ARDRES002, 6 for `?.`/`??` scenarios).

### Changed
- Refactored ARDRES001 to use shared helper class for guard detection logic.

## [1.0.0] - 2026-03-26

### Added
- **ARDRES001**: Roslyn analyzer that detects `Result<T>.Value` access without prior `IsSuccess` or `Status` check.
- Control flow analysis with 6 guard patterns: `if (IsSuccess)`, guard clause (early return/throw), `switch` statement, `switch` expression, ternary (`? :`), short-circuit (`&&`).
- Detection in direct access, LINQ lambdas, query syntax, delegates, extension methods, chain patterns, and tuple/collection wrapping.
- 36 unit tests for ARDRES001.
- 21 unit tests with LanguageExt integration.
- NuGet package configuration.
- GitHub Actions CI workflow.

[1.1.0]: https://github.com/rick-dev-creator/ArdalisAnalyzer/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/rick-dev-creator/ArdalisAnalyzer/releases/tag/v1.0.0
