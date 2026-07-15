# Postie

Postie is a set of .NET libraries that map CQRS commands and queries directly to ASP.NET Core minimal
API endpoints, with a bring-your-own-mediator design. Packages are published to nuget.org and must be
free forever — no commercial editions.

## Core Principles

- The ASP.NET Core endpoint mapping is the headline feature; the mediator is a good-enough in-box
  default, not a MediatR competitor.
- The endpoint engine (`Postie.AspNetCore`) must never reference a mediator package. Mediator support
  plugs in via adapter packages.
- Keep the public surface area small and stable. Breaking changes require explicit approval.
- Public APIs require XML documentation; behaviour that isn't obvious from the signature requires a
  README example.

## Coding Standards

- Follow `.editorconfig`: PascalCase types, `_camelCase` private fields, `var` for obvious types,
  expression-bodied members when they aid clarity.
- Use framework types (e.g. `String.IsNullOrEmpty`) when calling static methods; built-in types
  (e.g. `string`) for declarations.
- Validate null arguments on public entry points with `ArgumentNullException.ThrowIfNull`.
- Use the latest C# language features where possible (collection expressions, primary constructors).
- Fix compiler warnings unless explicitly instructed otherwise.

## Testing

- Multi-target `net8.0;net9.0;net10.0` — run tests across all frameworks before pushing.
- xUnit v3; test methods PascalCase with a Given/When/Then XML summary; `[Trait("Category", "Unit")]`.
- CI runs `dotnet test` without a category filter — untagged tests still run.

## Repository Tips

- Shared build metadata lives in `Directory.Build.props` (root and `src/`); NuGet versions are centrally
  managed in `Directory.Packages.props`.
- Versioning: `VersionPrefix` in `Directory.Build.props` is the deliberate Major.Minor; the patch is
  derived by CI from commits since that line last changed (`get-dotnet-version` action). Set
  `VersionSuffix` for prerelease lines.
- CI publishes prerelease packages to GitHub Packages from `main`; releases to nuget.org are manual via
  the Release workflow.
