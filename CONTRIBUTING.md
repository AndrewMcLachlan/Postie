# Contributing to Postie

Thanks for your interest in Postie. Issues and pull requests are welcome — this page tells you how
to get a change accepted with the least friction.

## Before you build a feature

Open an issue first. Postie deliberately keeps its public surface small: the endpoint engine
(`Postie.AspNetCore`) must never reference a mediator or validation package, mappings only advertise
responses they themselves produce, and conventions you can already compose on the returned
`RouteHandlerBuilder` are usually declined as built-ins. An issue conversation up front avoids you
building something that can't be merged.

Bug fixes don't need prior discussion — a failing test that demonstrates the bug is the best
possible opening move.

## Building and testing

You need the .NET 8, 9 and 10 SDKs installed.

```
dotnet build Postie.slnx
dotnet test Postie.slnx
```

Tests run across `net8.0`, `net9.0` and `net10.0`; all frameworks must pass. The two runnable
sample apps under [`samples/`](samples) are handy for exercising endpoint behavior by hand.

## Conventions

- `.editorconfig` is the law: PascalCase types, `_camelCase` private fields, `var` for obvious
  types, expression-bodied members when they aid clarity. Use the latest C# features where they fit
  (collection expressions, primary constructors).
- Public APIs require XML documentation. Behavior that isn't obvious from the signature requires a
  README example in the affected package's README.
- Validate null arguments on public entry points with `ArgumentNullException.ThrowIfNull`.
- Fix all compiler warnings — the build should be warning-clean.
- Tests are xUnit v3: PascalCase method names, a `/// Given/When/Then` XML summary, and a
  `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]` trait. Integration tests
  use `Microsoft.AspNetCore.TestHost` against real endpoints, not mocks.

## Breaking changes

Breaking changes to the public surface require explicit maintainer approval before you start —
raise them in an issue. Call any breaking change out in your PR description.

## Dependencies

NuGet versions are centrally managed in `Directory.Packages.props`. Note that the MediatR
reference is deliberately pinned to the 12.x line (the last Apache-2.0 releases); do not bump it
to 13+.

## Releases

CI publishes `-ci.N` prerelease packages to GitHub Packages from `main`. Releases to nuget.org are
manual, via the Release workflow. You don't need to touch versioning in a PR — `Version` in
`Directory.Build.props` is maintained deliberately.

## License

Postie is MIT licensed and free forever. By contributing, you agree that your contributions are
licensed under the [MIT License](LICENSE).
