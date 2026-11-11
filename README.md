# SPTC SupportDrawings — AutoCAD Plant 3D 2025

## REV 22.38 — Simplified drawing annotations

Only **displayed annotation text** changes; line tags, BOP elevations,
XREF provenance, source filenames in logs, 10-view generation, placement,
leader geometry, and all four production commands are unchanged.

| Before (REV 22.37) | After (REV 22.38) |
| --- | --- |
| `LINE NO: 100A-TEST-001` | `100A-TEST-001` |
| `BOP EL. +1117.5745 mm` | `BOP EL. +1117.5745 mm` (unchanged) |
| `XREF STRUCTURE: SUPP-1.dwg` | `SUPP-1` |
| `XREF SUPPORT: SUPP-2.dwg` | `SUPP-2` |

The actual XREF source `.dwg` filename and structure/support classification
remain in the TXT diagnostics; only paper-space labels omit the category
prefix and final extension. Repeated member labels stay repeated, because
the source links and leaders are kept independent.

Run `SPTC_SUPPORTORTHO10` after `NETLOAD` on a copied Plant 3D 2025
project and inspect labels in generated views. This formatting is
unit-tested; native Plant 3D visual inspection remains necessary.

## REV 22.37 — Nearby XREF Structure/Support source DWG annotations

**New default-on option:** `Nearby XREF members: label source DWG` in
`SPTC_SUPPORTORTHO10` > **10-View Ortho** > **Annotations**.

When SupportFocus is selected (and also when Original mode is used), the
generator searches a limited support-centered envelope for real Plant 3D
`StructureMember` and clearly identified native Pipe/Plant Support objects
inside **resolved, loaded XREFs**, including nested XREFs. It transforms their
actual source extents into host drawing WCS, then retains only objects that
intersect the final OrthoCube. Nearby XREF structure/support candidates can
extend the *vertical* SupportFocus extent within the existing safety cap;
the selected host support still determines horizontal footprint.

Each matched member is given an actual paper-space source callout such as:

```text
XREF STRUCTURE: STEEL_SUPPORTS_01.dwg
XREF SUPPORT: PIPE_SUPPORTS_A.dwg
```

The displayed DWG filename is read from the **actual source XREF database
filename**, not assumed from the block name. The full source path, nested
reference identity, detected object type, and annotation counts are written to
the generation TXT using the `REV22_37_XREF_...` markers.

XREF filenames use the existing native paper-space leader/callout planner. They
are **lower priority** than existing native Structure Member Size/TOS and Pipe
Line Number Tag/BOP labels. If there is not enough safe room, optional XREF
filenames are skipped and reported rather than crowding out the known-good
annotations. The new setting is optional; uncheck it to retain exactly the
previous XREF annotation behavior.

**Scope:** Only verifiably native structure or support objects are labelled;
ordinary XREF lines, basic 3D solids, and generic block references are not
misidentified as Plant supports. Source member size, support TOS, and other
unavailable PnP properties are not invented. A loaded XREF is required;
unresolved XREFs are skipped with diagnostics. The code does **not** edit source
DWGs, XREF models, PnP rows, or alter pipe/steel annotation calculations.

Four existing AutoCAD commands are unchanged. The SPTC Engineering branded UI
remains intact. The code requires a live Plant 3D 2025 copied-project test to
confirm XREF object type recognition and paper-space callout readability.

## REV 22.36 — SPTC Engineering branded WinForms UI

The supplied **SPTC Engineering** logo is now embedded directly in the DLL as a
transparent optimized PNG. The full-color wordmark is displayed in the header
of the Support Drawing Manager, as a smaller banner in the progress window and
existing Support Batch export dialog. A compact brand icon is used in the
Windows title bars. The logo remains visible offline with no external PNG,
file path, manual resource installation or internet connection.

Branding adds no new AutoCAD commands and does **not** alter Ortho creation,
support batch export, source selection, scale, camera, annotations or project
registration. For legibility, the Manager and progress windows have slightly
taller headers; the original tab controls remain available. This revision
includes a Windows logo/image/icon smoke test in GitHub Actions.

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
3. `NETLOAD` the REV22.36 DLL and run `SPTC_SUPPORTORTHO10`.
4. Choose the form tab and settings, click **Preflight** to inspect options, then **Run selected**.
5. When generating Ortho drawings, select one support or select multiple block supports if Batch Ortho is enabled. Review all generated DWGs, paper annotations and TXT logs.

### Build

Standard Windows GitHub Actions runner, .NET 8 x64, source/geometry regression smoke tests, UI-subset title/callout smoke tests, exact four-command CI allowlist, DLL artifact `SPTC-SupportDrawings-Plant3D2025-REV22_38-SIMPLE-LABELS`.

Rollback source is preserved in repository Git history and the [full pre-cleanup archive](https://github.com/nichslos/SPTC-SupportDrawings-Plant3D2025/tree/archive-rev22-30-full-test-history).
