# SPTC SupportDrawings — Plant 3D 2025

**Production cleanup REV 22.31** — retains the verified REV 22.29 native 10-view generator, nested-XREF pipe detection, Line Number Tag/BOP text, support TOS, automatic ten-view placement, and REV 22.30 read-only XREF verification.

## Production commands

| Purpose | AutoCAD command |
| --- | --- |
| Generate 10 native Ortho views and pipe/support annotations | `SPTC_SUPPORTORTHO10` |
| Verify loaded XREF pipe metadata and leader alignment after Update View | `SPTC_ORTHO22_30_VALIDATE_XREF_UPDATE` |
| Audit whether original pipe DBText persisted after Update View | `SPTC_ORTHO22_29_AUDIT_UPDATE` |
| Existing support export, Support2D, inspection and batch operations | `SPTC_SUPPORTEXPORT`, `SPTC_SUPPORT2D`, `SPTC_SUPPORTINSPECT`, `SPTC_SUPPORTBATCH` |

The generator is the **same verified code** as REV 22.29, with its class and public AutoCAD command renamed. The source generation log and saved Ortho DWG still use the **REV22_29** naming conventions so both audit commands remain compatible. Do **not** rename that log or edit its source entries.

## Using the production generator

1. In a backed-up Plant 3D 2025 project, open a saved project-registered model with resolved pipe XREFs.
2. `NETLOAD` the `SPTC.SupportDrawings.dll` built by this repository.
3. Run `SPTC_SUPPORTORTHO10`; select the support and complete the same copied-project/empty Ortho seed safety confirmation as REV 22.29.
4. The plugin creates ten standard Ortho viewports automatically and writes a `_REV22_29_RESULT.txt` beside the Ortho DWG.
5. Open the generated Ortho and its source model together. After **manual** Update View, run `SPTC_ORTHO22_29_AUDIT_UPDATE` and `SPTC_ORTHO22_30_VALIDATE_XREF_UPDATE` if required.

The verification commands do not update the DWG or source PnP database. `PASS` still does **not** establish freshness of Autodesk's generated model linework or that the disk XREF was reloaded.

## Build and tests

Automatic GitHub Actions builds run on `main` pushes and pull requests, using the **standard `windows-latest` runner** and .NET 8. The workflow runs:
- Support geometry, viewport and metadata smoke tests;
- Compact two-line XREF pipe label smoke tests;
- Strict original-manifest parsing smoke tests;
- Production command/safety invariants and full x64 DLL compilation.

Download the artifact `SPTC-SupportDrawings-Plant3D2025-REV22_31-CLEAN` from a successful workflow run; extract the DLL for `NETLOAD`.

## Cleanup and rollback

- Replaced 35+ old experimental full-generator classes with a single production 10-view generator. Earlier experimental command names (ending in `_TEST`) are **not included** in this cleaned DLL.
- Removed the obsolete support source README and early test probe/staging files from the active branch.
- Condensed the 88 KB CI script while retaining executable smoke tests and key safety assertions.
- **Full prior source and CI workflow are preserved** in Git branch [`archive-rev22-30-full-test-history`](https://github.com/nichslos/SPTC-SupportDrawings-Plant3D2025/tree/archive-rev22-30-full-test-history). The preceding compiled REV 22.30 artifact is also available in the previous run history.

Autodesk Plant 3D's native Ortho generation still uses an unsupported internal API and must be run on a copied/backed-up project first. Compilation and smoke tests do not replace live CAD validation.

See [REV 22.29 label/audit guide](docs/REV22_29_COMPACT_PIPE_UPDATE_AUDIT.md) and [REV 22.30 XREF verification guide](docs/REV22_30_XREF_UPDATE_VALIDATION.md) for diagnostics.
