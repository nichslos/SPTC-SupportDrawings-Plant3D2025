# SPTC SupportDrawings — AutoCAD Plant 3D 2025

This repository contains the isolated **SPTC SupportDrawings** C# plugin, starting at **REV 22.27**.

## Source baseline

Transferred without modifying existing plugin source from [nichslos/AutoPipeRouter-Plant3D2025](https://github.com/nichslos/AutoPipeRouter-Plant3D2025) at pinned commit `32a36b8f2709bed155c0bb01b4cf1d6ac4223abe`. The original repository is preserved.

- `src/SPTC.SupportDrawings/` — plugin source code and .NET 8 Windows x64 project
- `tests/SupportSheetSmokeTests/` — regression/smoke tests
- `.github/workflows/build-support-drawings.yml` — automatic and manual DLL build

## Build

GitHub Actions automatically runs on a **push to main** that changes SupportDrawings source files, tests, or the workflow. It can also be started manually from **Actions → Build SPTC Support Drawings DLL → Run workflow**.

The workflow uses the standard `windows-latest` GitHub-hosted runner, installs the .NET 8 SDK, runs regression checks, builds the library, and uploads a downloadable DLL artifact named `SPTC-SupportDrawings-Plant3D2025-REV22_27`.

Expected output: `SPTC.SupportDrawings.dll` from `src/SPTC.SupportDrawings/bin/Release/net8.0-windows/`.

## Notes

- REV 22.27 introduces Pipe Line Number Tag and BOP annotations, with prior ten-view placement, Support/TOS, and structural-member functions retained.
- Run and validate the native AutoCAD Plant 3D commands in **Plant 3D 2025** on a test or backed-up project first.
- Builds require the NuGet dependencies in `SPTC.SupportDrawings.csproj`; a successful CI build is not equivalent to successful in-application runtime validation.
- The AutoPipeRouter plugin is intentionally not included in this dedicated SupportDrawings repository.
