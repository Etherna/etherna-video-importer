# Etherna Video Importer

Etherna Video Importer is a **.NET console application** (not a library) to import videos on Etherna from different sources. The solution produces two executables sharing the same core: **`evi`** (`EthernaVideoImporter`, generic use — imports from YouTube links or from a JSON video list) and **`evid`** (`EthernaVideoImporter.Devcon`, imports Devcon Archive's videos). The import pipeline: fetch metadata and video from the source, encode to HLS with FFmpeg, upload chunks to the Etherna gateway (Swarm) buying postage batches, publish/update video manifests on the Etherna Index, and optionally clean up videos removed from the source.

## Build, run, test

Target framework is **.NET 10** with `TreatWarningsAsErrors=true` and `AnalysisMode=AllEnabledByDefault` (and `Nullable=enable`, `EnableNETAnalyzers=true`) on every source project — warnings break the build. `NoWarn` suppresses only the NuGet vulnerability audit warnings (`NU1902;NU1903`).

```bash
dotnet restore EthernaVideoImporter.sln
dotnet build EthernaVideoImporter.sln -c Release
dotnet test  EthernaVideoImporter.sln -c Release    # runs the xUnit test project (currently an empty scaffold)
dotnet run --project src/EthernaVideoImporter -- --help                    # evi help text
dotnet run --project src/EthernaVideoImporter -- youtube <url> [OPTIONS]   # import from YouTube
dotnet run --project src/EthernaVideoImporter -- json <metadata.json> [OPTIONS]
dotnet run --project src/EthernaVideoImporter.Devcon -- <args>             # evid
```

Running an import requires **FFmpeg and FFprobe** binaries: by default they are searched in `<app_dir>/FFmpeg` (see `src/EthernaVideoImporter.Core/FFmpeg/Place_Here_FFmpeg.md`) or in the global install, or passed with `-f/--ffmpeg-path`. OS-specific binary names are resolved in `CommonConsts`. The two executable projects take a `FrameworkReference` to `Microsoft.AspNetCore.App` (needed by the SDK's native "code" auth flow), so the ASP.NET Core Runtime is a runtime prerequisite alongside the .NET Runtime — release packages also ship in a `selfcontained` variant. `InvariantGlobalization=true` is set on both executables.

The **`Debug-DevEnv`** build configuration defines the `DEVENV` constant, which switches the `#if DEVENV` blocks in each `Program.cs` to local dev services: SSO on `https://localhost:44379/`, gateway on `http://localhost:1633/`, index on `https://localhost:44357/`. Plain `Debug`/`Release` builds target the production Etherna endpoints from `EthernaUserClientsBuilder` defaults.

`nuget.config` adds the `ethernaMyget` feed (https://www.myget.org/F/etherna/api/v3/index.json) next to nuget.org — prerelease dependencies like `EthernaSdk.Users.Gateway`/`EthernaSdk.Users.Index` come from there. Versioning is automatic via **GitVersion** (`GitVersion.MsBuild` in every source project, plus SourceLink). CI (`.github/workflows/publish-stable.yml`): tags `v*.*.*` run `dotnet publish` for both executables over a matrix of runtime identifiers (linux/macos/windows × x86/x64/arm/arm64, framework-dependent and self-contained) and attach the archives to the GitHub release.

## Architecture

Solution `EthernaVideoImporter.sln`: three source projects plus one test project. Project folders are named `EthernaVideoImporter.*` but root namespaces use `Etherna.VideoImporter[.Core|.Devcon]`; under the root, the namespace mirrors the folder path.

- **`src/EthernaVideoImporter.Core`** (`Etherna.VideoImporter.Core`) — All the import logic, shared by both executables.
  - `EthernaVideoImporter.cs` (`IEthernaVideoImporter`) — the orchestrator: `RunAsync` authenticates the user, creates the working directory, gets videos from the `IVideoProvider`, reads the user's already-indexed videos, and per video decides the minimal required operation (skip / manifest-only update via `IMigrationService` / full encode+upload), then reports results and cleans up the index.
  - `Models/Domain/` — domain types (`Video`, `VideoMetadataBase`, `YouTubeVideoMetadataBase`, `ThumbnailFile`, the `VideoImportResultBase` hierarchy) and the strongly-typed working-directory layout under `Directories/` (`DirectoryBase`, `WorkingDirectory`, `EncodedVideoDirectory`, …). `Models/FFmpeg/` holds encoding enums and the `FFProbeResultDto`; `Models/GitHubDto/` the release-check DTO; `Models/ModelView/` the final `ImportSummaryModelView`.
  - `Services/` — `EncodingService` (HLS encoding), `FFmpegService` (FFmpeg/FFprobe process runner via MedallionShell), `VideoUploaderService` (chunking, postage batch purchase, manifest upload), `CleanerVideoService` (index cleanup), `MigrationService` (rebuilds manifests from old schema versions without re-encoding), `AppVersionService` (GitHub release update check), `ConsoleIoService` (`IIoService`). Interfaces `IVideoProvider` and `IResultReporterService` are defined here but implemented by the executables.
  - `Options/` — one options class per service (`FFmpegServiceOptions`, `VideoUploaderServiceOptions`, …), configured with lambdas through `ServiceCollectionExtensions.AddCoreServices`.
  - `Utilities/` — `YoutubeDownloader` (`IYoutubeDownloader`, wraps Etherna's YoutubeExplode fork `Etherna.YoutubeDownloader.Converter`), `UrlBuilder`.
- **`src/EthernaVideoImporter`** (`Etherna.VideoImporter`, assembly **`evi`**) — Generic CLI host. `Program.cs` parses arguments manually, wires DI, and runs the importer. Implements the two sources: `Services/YouTubeVideoProvider` (command `youtube`) and `Services/JsonListVideoProvider` (command `json`), plus `JsonResultReporterService`; source-specific models under `Models/`, options + validations under `Options/`.
- **`src/EthernaVideoImporter.Devcon`** (`Etherna.VideoImporter.Devcon`, assembly **`evid`**) — Devcon Archive host, same structure: `DevconVideoProvider`, `DevconResultReporterService`, and their options.
- **`test/EthernaVideoImporter.Core.Tests`** — xUnit + Moq test project referencing Core. Currently an empty scaffold with no tests: when adding logic worth covering, put tests here mirroring the Core folder layout.

### Key cross-cutting points

- **Provider/reporter plugin pattern.** Core orchestrates; each executable plugs in its own `IVideoProvider` (where videos come from) and `IResultReporterService` (where results are written), registered in its `Program.cs` next to `services.AddCoreServices(...)`. To support a new source, implement these two interfaces in a host project — don't branch inside Core.
- **CLI parsing is manual.** Each `Program.Main` parses `args` by hand (no command-line library) and keeps the full help text as a raw string literal const. A new option must be added to the parser, threaded into the right options lambda, and documented in the help text (and in `README.md`).
- **Etherna clients come from the EthernaSdk builders.** Without `-k/--api-key` the "code" grant flow opens a browser login (local redirect on port 11420); with it, the api-key flow is used. Then `AddEthernaGatewayClient(apiCompatibility, dryMode, gatewayBaseUrl)` and `AddEthernaIndexClient(indexBaseUrl)` register the service clients. The `--dry` flag propagates as the SDK client's `dryMode` plus `CleanerVideoServiceOptions.IsDryRun` — any new side-effecting operation must respect it.
- **Manifest building is SDK work.** Video manifests, HLS parsing, and chunking come from `Etherna.Sdk.Tools.Video` (`IVideoManifestService`, `IHlsService`), `Etherna.Sdk.Tools.UniversalFiles` (`UUri`/`UFile` over local/online/Swarm resources), and BeeNet (`IChunkService`, `Hasher`) — registered in `AddCoreServices`. This repo only orchestrates them.
- **Videos are recognized via manifest personal data.** `CommonConsts.ImporterIdentifier` plus the serialized source id let re-runs match already-uploaded videos (to minimize work) and let `CleanerVideoService` distinguish videos created by this tool (`-m/--remove-missing`) from foreign ones (`--remove-unrecognized`).
- **FFmpeg runs as an external process.** `FFmpegService` (singleton — it owns the located binary paths) shells out via MedallionShell; encoding parameters come from `FFmpegServiceOptions` (`--bitrate-reduction`, `--ffmpeg-preset`). Thumbnails are blurhashed with `Blurhash.SkiaSharp`.
- **All console I/O goes through `IIoService`** (`ConsoleIoService`) — never `Console` directly; this keeps services testable and output consistent.
- **No `ConfigureAwait(false)`** — this is an application, `CA2007` is disabled in `.editorconfig`. (This is the opposite of the Etherna SDK/library repos.)

## Issue tracker

Bugs and features are tracked in Jira project **EVI** (https://etherna.atlassian.net/projects/EVI). Branch names follow `feature/EVI-<id>-<slug>` / `improve/EVI-<id>-<slug>` / `fix/EVI-<id>-<slug>` — match this when creating branches. Release hotfixes use `hotfix/<version>` (e.g. `hotfix/0.3.12`); `dev` is the integration branch, `main` is stable; stable releases are tagged `v<version>` (which triggers the publish workflow).

# Coding Style

## General Principles

- Keep commits clean: only include changes strictly necessary for the task at hand.
- Never reference AI agents or assistants in commits or code — no agent names, no `Co-Authored-By` agent trailers, no "generated/assisted by" notes. Commit messages and code must read as the team's own work.
- Exceptions to these conventions are accepted when strictly necessary or when they significantly improve code quality. Justify with a comment where needed.
- All elements (usings, properties, methods, fields, enum members, etc.) are always alphabetically ordered within their respective sections.
- Primary constructors are preferred everywhere the constructor is a simple parameter assignment.
- Keep code clean: remove unused variables, dead code, and redundant imports.
- Every source file starts with the standard AGPL-3.0 copyright header (`// Copyright 2022-present Etherna SA` … see any existing file).

## Naming

- **Classes/Structs**: PascalCase (`EncodingService`, `VideoUploaderService`, `ThumbnailFile`)
- **Interfaces**: `I` prefix (`IVideoProvider`, `IIoService`, `IFFmpegService`)
- **Async methods**: always `Async` suffix (`RunAsync`, `EncodeVideoAsync`, `UploadVideoAsync`)
- **Properties**: PascalCase, boolean `Is` prefix (`IsDryRun`)
- **Private fields**: `_camelCase` only when backing a same-named property; otherwise plain `camelCase`
- **Primary constructor parameters**: `camelCase` without underscore
- **Constants**: PascalCase (`ImporterIdentifier`, `DefaultTtlPostageStamp`)
- **Enums**: PascalCase type and members (`FFmpegBitrateReduction.Normal`, `SourceType.YouTube`)
- **Namespaces**: `Etherna.VideoImporter[.Core|.Devcon].<Feature>` mirroring the folder under the root namespace (e.g. `Etherna.VideoImporter.Core.Models.Domain`, `Etherna.VideoImporter.Services`)
- **Base classes**: `Base` suffix for abstract (`VideoMetadataBase`, `DirectoryBase`, `VideoImportResultBase`)
- **DTOs**: `Dto` suffix (`FFProbeResultDto`, `VideoImportResultDto`, `DevconFileDto`)
- **Options**: `<Service>Options` with companion `<Service>OptionsValidation` where input needs validating (`DevconVideoProviderOptions`/`DevconVideoProviderOptionsValidation`)

## Code Organization

- One class per file, filename matches class name
- Namespace mirrors folder structure exactly (under the `Etherna.VideoImporter` root namespace)
- Block-scoped namespaces: `namespace X { ... }` — NOT file-scoped
- Using directives at the top of the file, before the namespace block, always alphabetically ordered and kept to the minimum necessary
- No global usings — each file declares its own imports
- Shared logic goes in `EthernaVideoImporter.Core`; the executable projects hold only source-specific providers, reporters, options, and CLI wiring

## Comments

Principal comments (generally multiline, important):
```csharp
// Capital start, ending period.
// Continued on next line if needed.
```

Secondary/separator comments:
```csharp
//no space, no capital, no ending period
```

## Member Ordering Within a Class

Use principal-style section comments to delimit groups, in this order:

```csharp
// Consts.
// Fields.
// Constructors.
// Properties.
// Methods.
// Helpers.
```

## Class Design

- `abstract` for base classes with shared behavior (`VideoMetadataBase`, `DirectoryBase`, `VideoImportResultBase`)
- `sealed` where appropriate
- Primary constructors everywhere the constructor is a simple assignment:
  ```csharp
  public class MigrationService(
      ISwarmClient beeClient,
      Hasher hasher,
      IHlsService hlsService,
      IUFileProvider uFileProvider)
      : IMigrationService
  {
  }
  ```
- Constructor chaining for inheritance
- Extension methods for utility operations (`Extensions/`)

## Async Patterns

- Always suffix with `Async`
- `CancellationToken` propagation through processing pipelines
- Return `Task` or `Task<T>`, never `async void`
- **No `ConfigureAwait(false)`** — this is an application, and `CA2007` is disabled in `.editorconfig`

## Null Handling

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- `ArgumentNullException.ThrowIfNull()` for parameter validation
- `is null` / `is not null` (not `== null`)
- Prefer `null` over `default` as default value for optional parameters
- Throw expressions: `?? throw new InvalidOperationException()`

## Formatting

- Allman braces (opening brace on new line)
- 4-space indentation
- Expression-bodied members for single-expression methods/properties
- String interpolation; raw string literals for embedded multi-line text (see the help texts in `Program.cs`)
- LINQ method chains: one operation per line, aligned
- Blank line between member sections

## C# Language Features

- Pattern matching: `is`, `is not`, type patterns, property patterns
- Switch expressions for multi-branch returns
- Primary constructors everywhere applicable
- Collection expressions: `[]`, `[..spread]` — prefer them over constructors to initialize any collection (`[]` not `new()`)
- Target-typed `new()` when type is clear from context (for non-collection types)

## LINQ

- Method syntax preferred over query syntax
- Fluent chaining, one operation per line for readability

## Dependency Injection

- Constructor injection exclusively
- `IIoService` for I/O abstraction (testability)
- Core registration via `ServiceCollectionExtensions.AddCoreServices`, taking one configuration lambda per options class
- `AddTransient` by default; `AddSingleton` for `FFmpegService` and `IUFileProvider`
- Each executable registers its own `IVideoProvider` and `IResultReporterService` in `Program.cs`

## Testing (xUnit + Moq)

The test project is currently an empty scaffold — when adding tests, follow the conventions of the other Etherna repos:

- `[Fact]` for basic tests, `[Theory]` with `[MemberData]` for parameterized cases
- AAA pattern with section comments: `// Setup.`, `// Action.`, `// Assert.`
- Moq for mocking (`new Mock<IIoService>()`)
- Mirror the folder layout of the project under test (`EthernaVideoImporter.Core.Tests` mirrors `EthernaVideoImporter.Core`)
