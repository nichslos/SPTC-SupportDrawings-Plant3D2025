# SPTC SupportDrawings — AutoCAD Plant 3D 2025

## REV 22.35 — Support Drawing Manager (UI + automation)

**Production command registration stays at exactly four:** `SPTC_SUPPORTORTHO10`, `SPTC_SUPPORTEXPORT`, `SPTC_SUPPORTBATCH`, `SPTC_SUPPORT2DBATCH`. This version adds a WinForms **Support Drawing Manager** that opens when `SPTC_SUPPORTORTHO10` is called, with four operation tabs. Choosing another tab dispatches the corresponding existing export/batch command rather than creating extra AutoCAD commands.

### Features

- **OrthoCube mode**: SupportFocus (default) or Original.
- **Seed**: automatically identify a project-registered **empty native Ortho DWG** in the active project Orthos directory. Manual seed picker remains available. Reject unregistered/nonempty seeds.
- **Preflight**: source/project summary, selected options, native seed verification, unique output name, safe scale fit and write/registration guards. Fail before any new DWG write on incompatible scale/seed.
- **DWG prefix**: optional validated filename prefix, with the previously verified unique source/handle/time suffix preserved. Existing destination filenames are never overwritten.
- **Native view selection**: choose any subset of TOP, BOTTOM, LEFT, RIGHT, FRONT, BACK, NW, NE, SW, SE (all 10 selected by default). The original all-ten layout and auto-fit path are preserved; subsets retain their original paper slots.
- **Scale**: automatic (default) or 1:10 through 1:500 menu. Unsafe manual magnification fails preflight before cloning/registering. Defaults preserve the original ten-view fit.
- **Annotation switches**: independently show/hide Pipe Line Number Tag, Pipe BOP, Support/Structure TOS, Support/Structure description (all ON by default).
- **Batch Ortho**: select multiple source BLOCK references and generate one native Ortho per selected block, sequentially. Each output uses an independent unique name and source selection; writes a timestamped `_SPTC_BATCH_*.txt` PASS/FAIL report. Use only in copied projects until runtime verified.
- **Progress**: nonmodal generation status for per-view work and final result. Generation TXT is now append-only (avoids repeated full-file rewrites).
- **Four operation tabs**: Ten-view Ortho, Single Support Export, Support Batch, and 2D Batch. Existing support/batch dialogs remain unchanged when dispatched.

### Safety and current limitations

- Native Ortho generation still uses Autodesk's **unsupported internal** `GenerateOrthos` API. **Use a copied or backed-up Plant 3D 2025 project**.
- The form does not eliminate support selection. Batch Ortho requires separate BLOCK reference(s) representing individual supports; it does **not** infer separate supports from arbitrary loose member selections.
- Batch drawing registration has not been verified in a live Plant 3D session yet. The default single-support, 10-view configuration remains the recommended operational path until tested.
- The seeded native sheet must be project-registered and empty, and source/model registration is checked. No existing DWG is overwritten. Native Update View and generated CAD linework still require visual validation.
- The generator retains internal REV22.29 output log identifiers for compatibility/history; the public command is `SPTC_SUPPORTORTHO10`.

### Install

1. Close Plant 3D completely; replace the old `SPTC.SupportDrawings.dll`.
2. In a copied project open a saved registered source 3D DWG with resolved XREFs.
3. `NETLOAD` the REV22.35 DLL and run `SPTC_SUPPORTORTHO10`.
4. Choose the form tab and settings, click **Preflight** to inspect options, then **Run selected**.
5. When generating Ortho drawings, select one support or select multiple block supports if Batch Ortho is enabled. Review all generated DWGs, paper annotations and TXT logs.

### Build

Standard Windows GitHub Actions runner, .NET 8 x64, source/geometry regression smoke tests, UI-subset title/callout smoke tests, exact four-command CI allowlist, DLL artifact `SPTC-SupportDrawings-Plant3D2025-REV22_35-UI`.

Rollback source is preserved in repository Git history and the [full pre-cleanup archive](https://github.com/nichslos/SPTC-SupportDrawings-Plant3D2025/tree/archive-rev22-30-full-test-history).
