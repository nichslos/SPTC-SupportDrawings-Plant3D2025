# SPTC SupportDrawings — AutoCAD Plant 3D 2025

This repository contains the isolated **SPTC SupportDrawings** C# plugin, currently at **REV 22.29**, preserving the REV 22.27 and REV 22.28 commands as fallbacks.

## Source baseline

Transferred without modifying existing plugin source from [nichslos/AutoPipeRouter-Plant3D2025](https://github.com/nichslos/AutoPipeRouter-Plant3D2025) at pinned commit `32a36b8f2709bed155c0bb01b4cf1d6ac4223abe`. The original repository is preserved.

- `src/SPTC.SupportDrawings/` — plugin source code and .NET 8 Windows x64 project
- `tests/SupportSheetSmokeTests/` — regression/smoke tests
- `.github/workflows/build-support-drawings.yml` — automatic and manual DLL build

## Build

GitHub Actions automatically runs on **every push to main** and validates pull requests against main. It can also be started manually from **Actions → Build SPTC Support Drawings DLL → Run workflow**.

The workflow uses the standard `windows-latest` GitHub-hosted runner, installs the .NET 8 SDK, runs regression checks, builds the library, and uploads a downloadable DLL artifact named `SPTC-SupportDrawings-Plant3D2025-REV22_29`.

Expected output: `SPTC.SupportDrawings.dll` from `src/SPTC.SupportDrawings/bin/Release/net8.0-windows/`.

## Notes

- **REV 22.29 XREF compact command:** `SPTC_ORTHO22_29_XREF_COMPACT_TEST` automatically discovers nearby loaded XREF/native pipes, with exactly two readable pipe-label lines (`LINE NO: <tag>` and `BOP EL. <elevation>`); full internal XREF source keys remain in the TXT log.
- **REV 22.29 read-only Update View audit:** run `SPTC_ORTHO22_29_AUDIT_UPDATE` in the generated Ortho DWG **after** manually running Plant 3D Update View. Compares original expected pipe annotation text/position to the actual paper-space DBText. Does not change the DWG or guarantee that properties were refreshed. See [REV 22.29 guide](docs/REV22_29_COMPACT_PIPE_UPDATE_AUDIT.md).
- **REV 22.28 test command:** `SPTC_ORTHO22_28_XREF_AUTO_TEST` automatically scans nearby native and loaded nested-XREF pipes for explicit Line Number Tag/BOP. No separate pipe selection. See [REV 22.28 test guide](docs/REV22_28_XREF_TEST_GUIDE.md).
- **REV 22.27 unchanged fallback:** `SPTC_ORTHO22_27_PIPE_BOP_TEST`; includes Pipe Line Number Tag and BOP annotations, ten-view placement, Support/TOS and structural member support.
- Run and validate the native AutoCAD Plant 3D commands in **Plant 3D 2025** on a test or backed-up project first.
- Builds require the NuGet dependencies in `SPTC.SupportDrawings.csproj`; a successful CI build is not equivalent to successful in-application runtime validation.
- The AutoPipeRouter plugin is intentionally not included in this dedicated SupportDrawings repository.
