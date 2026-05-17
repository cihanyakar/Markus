# Markus

[![CI](https://github.com/cihanyakar/Markus/actions/workflows/ci.yml/badge.svg)](https://github.com/cihanyakar/Markus/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com)

A fast, elegant Markdown viewer built with .NET 10 and Avalonia.

> Status: bootstrapping. The application architecture is still being planned.

## Tech stack

- .NET 10 / C# (latest language version)
- Avalonia 12 (cross-platform UI)
- CommunityToolkit.Mvvm

## Build and run

```bash
dotnet tool restore
dotnet build
dotnet run --project src/Markus
```

## Test

```bash
dotnet test
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All contributions must follow Conventional Commits and pass formatter, analyzers, and build (all warnings are errors).

## License

[MIT](LICENSE)
