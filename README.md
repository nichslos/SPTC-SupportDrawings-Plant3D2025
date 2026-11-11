# SPTC SupportDrawings — AutoCAD Plant 3D 2025

**REV 22.33 — Exactly four production commands.**

The tested ten-view native Ortho generation code and its XREF Pipe Line Number Tag / BOP, support TOS, structure annotations, and automatic ten-view placement are unchanged. This revision trims registered AutoCAD commands and obsolete diagnostic code.

## Commands included in REV 22.33

| AutoCAD command | Function |
| --- | --- |
| `SPTC_SUPPORTORTHO10` | Generate 10 native Ortho views with pipe/support annotations |
| `SPTC_SUPPORTEXPORT` | Export one selected support drawing |
| `SPTC_SUPPORTBATCH` | Batch export support drawing DWGs |
| `SPTC_SUPPORT2DBATCH` | Batch export 2D support drawings (with existing 3D fallback) |

**No other SPTC commands are registered in this DLL.**

## Commands removed from production registration

- `SPTC_SUPPORT2D` (interactive Ortho launcher)
- `SPTC_SUPPORT2DLINEWORK` (single-selection 2D exporter)
- `SPTC_SUPPORTINSPECT` (entity-audit launcher)
- `SPTC_SUPPORTMANAGER` (duplicate alias for batch export)
- `SPTC_ORTHO17_TOP_PREFLIGHT` (legacy diagnostic)
- `SPTC_ORTHO22_6_CAPTURE_NATIVE_ISO` (legacy diagnostic)
- `SPTC_ORTHO22_6_COMPARE_NATIVE_ISO` (legacy diagnostic)

The previously removed REV22.29 and REV22.30 validation commands also remain absent. Shared support export, Plant Ortho and pipe annotation helpers required by the retained four commands are kept. Internal legacy identifiers and `_REV22_29_RESULT.txt` output naming remain compatible with the previously verified 10-view generator.

## Install and test

1. Close AutoCAD Plant 3D **2025** completely before replacing an old DLL (previous DLL registrations are kept in the existing AutoCAD process).
2. Open a copied or backed-up Plant 3D 2025 project.
3. Run `NETLOAD` and select the new `SPTC.SupportDrawings.dll`.
4. Use `SPTC_SUPPORTORTHO10` for the proven ten-view workflow, or one of the three export/batch commands above.
5. Validate generated Ortho geometry and support annotations in Plant 3D. Autodesk native Ortho generation still uses an unsupported internal API and should be run on copied projects.

## Build and rollback

GitHub Actions (`windows-latest`, .NET 8) enforces an exact four-command allowlist, runs source/geometry/callout regression checks, compiles the DLL, and uploads `SPTC-SupportDrawings-Plant3D2025-REV22_33-FOUR-COMMANDS`.

The complete earlier project and test history is recoverable from Git commits or the [REV22.30 archive branch](https://github.com/nichslos/SPTC-SupportDrawings-Plant3D2025/tree/archive-rev22-30-full-test-history).
