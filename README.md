# Chess

A local two-player chess application for Windows, built with C#, .NET 10 LTS, and WPF.
The rules live in `ChessLogic`; the desktop interface lives in `ChessUI`.

The original foundation follows [OttoBotCode's chess tutorial](https://www.youtube.com/@OttoBotCode).

## Build and run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.
From the repository root:

```powershell
dotnet restore src/Chess.sln
dotnet build src/Chess.sln --configuration Release
dotnet run --project src/ChessUI/ChessUI.csproj
```

Alternatively, open `src/Chess.sln` in a Visual Studio version supporting .NET 10
with the **.NET desktop development** workload, and select `ChessUI` as the startup project.

Select a piece, then a highlighted destination. Choose a piece when promoting a pawn.
Press **Escape** to pause, continue, or restart.

## Features

- Board rendering and legal-move highlights
- Turn tracking and king-safety validation
- Castling, en passant, and all four promotion choices
- Checkmate, stalemate, and basic insufficient-material detection
- Pause, restart, and rematch menus

See [gameplay controls and validation](docs/gameplay.md) for draw claims, move history, resignation, and tests.

## Validation and delivery

Pull requests to `main` and pushes to `main` run the GitHub Actions workflow:

1. Restore and build the solution on Windows.
2. Run all test projects in the solution and upload TRX results when available.
3. Publish and upload `Chess-win-x64.zip` as a workflow artifact.

The ZIP contains a self-contained Windows x64 build, so players do not need to install .NET.
Extract the full archive and run `ChessUI.exe`.

To publish a GitHub Release after merging and checking CI, tag a commit on `main`:

```powershell
git switch main
git pull --ff-only
git tag v0.1.0
git push origin v0.1.0
```

The tag workflow builds and validates the application before publishing the ZIP and
generated release notes. Tags outside `main` are rejected. No signing credentials are configured.
Use a new version tag for each release.

The `Windows build and tests` check can be selected as a required status check in the
repository's branch protection settings after its first run.
