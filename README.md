# SPTC SupportDrawings — AutoCAD Plant 3D 2025

## REV 22.44 — Every orthographic view, every structure/support member, H and V

This revision changes the **default-on Auto H/V dimensions: all Ortho
members/views** option to meet the new drawing requirement illustrated by
the 2026-11-11 six-view PDF. Previous REV22.42/43 deliberately kept only
one representative dimension across the whole drawing; they often left
BOTTOM, LEFT, RIGHT and BACK without a dimension.

- **Each selected TOP/BOTTOM/FRONT/BACK/LEFT/RIGHT native view** now
  independently attempts **two DIMLINEAR entities per recognized Plant
  StructureMember or Plant Support**, one horizontally and one vertically.
  An identical length or member shown in another view is **not suppressed**.
  This applies to PnP-verified native source members and qualifying Plant
  members in resolved nested XREFs (in their host WCS positions).
- Values are the **overall horizontal and vertical view-projected geometric
  envelope** of the source member, computed from eight corners of its
  WCS GeometricExtents and the **actual saved native viewport camera/scale**.
  **These are NOT member cutting/centerline lengths**; e.g. a vertical post
  can have separate plan footprint dimensions in TOP and its overall height
  in FRONT. An assembly's overall envelope may contain more than just the
  bare steel.
- Native AutoCAD RotatedDimension entities are placed in **paper space**
  on the existing `SPTC_AUTO_DIMLINEAR` layer. A paper-size length is NOT
  misreported as a model millimeter length: the displayed number is
  calculated from the actual native viewport projection and source units.
- Multiple dimension line lanes are tried to avoid text/line crowding and
  the Ortho viewport frames. A zero/too small projected axis, an
  unrecognized source, a partial/off-view member, or insufficient paper
  space is **skipped**, with a precise `REV22_44_DIMENSION_SKIPPED` reason.
  Thus no plugin can guarantee both dimensions for every object if the
  drawing is too small to place them legibly.
- **Isometric NW/NE/SW/SE views remain excluded from linear H/V
  dimensions.** Their apparent projected lengths are foreshortened and
  must not be represented as true DIMLINEAR measurements. They can still
  show existing pipe, support and member leader annotations.
- Result TXT contains `REV22_44_DIMENSION_MODE`,
  `REV22_44_MEMBER_SOURCES`, `REV22_44_HV_AXES_REQUESTED`,
  `REV22_44_HV_AXES_PLANNED`, `REV22_44_DIMENSIONS_CREATED`, and
  per-view `REV22_44_VIEW` lines with horizontal/vertical counts.
  Individual dimensions keep their real member handle/XREF reference ID.

**Safety and limitations:** Source models, loaded XREFs and PnP tables are
never written. SupportFocus, four existing AutoCAD commands, branded
persistent palette, custom Paper Scale, Ortho creation, BOP/TOS and member
leader annotations remain unchanged. Dimensions are native but **not
associative to model modifications**; manually updating an existing Ortho
will not refresh the plugin-authored dimensions. Generate a fresh Ortho
from a copied Plant 3D 2025 project and visually check the new H/V dimensions
before using it for engineering deliverables.

## REV 22.43 — Vertical Structure/Support overall-height dimensions

Addresses the reported case where a **vertical** support/member appears in
the Ortho model but REV22.42 makes no dimension for it. The previous
dimension reader required explicit `StartPoint` and `EndPoint` properties
and therefore skipped genuine Plant 3D vertical supports lacking those
properties. A new conservative geometric **overall Z height** fallback is
available **only** for already-recognized structure/support entity classes
or native Plant elements verified by the existing PnP-aware source readers.

- If valid Start/End coordinates are present, they remain the authoritative
  true *member length*. Their native DIMLINEAR text is unchanged, e.g. `450`.
- If Start/End is unavailable **but** the entity's real, finite host-WCS
  extents show a clearly upright part at least 150 mm tall and at most
  70% as wide/deep as it is tall, the tool can dimension the measured
  overall Z envelope (bottom to top). It is labeled **`H=450`**, not a
  fabricated fabrication/cut length.
- Only FRONT/BACK/LEFT/RIGHT views where that vertical span is actually
  projected and fits safely are eligible. TOP/BOTTOM and diagonal/isometric
  projections do not get an invented member-height dimension.
- Nested XREFs use each reference's actual World transform, and source
  entity geometry is read-only. Arbitrary solids/blocks cannot qualify
  solely because they are tall. A PnP-vetted native Support may qualify even
  if its runtime ObjectClass reports a generic Plant wrapper.
- REV22.42's **one dimension per type/axis/value** sheet-wide rule remains,
  so the same `H=...` will not repeat across opposite elevations. Pipe
  BOP/line tags, support TOS/Size, XREF drawing-name labels, geometry,
  paper scale, and the persistent palette are unchanged.
- `REV22_43_OVERALL_VERTICAL_HEIGHT_SOURCES`,
  `REV22_43_OVERALL_HEIGHT_DIMENSIONS_PLANNED`, and
  `REV22_41_SOURCE_DIAGNOSTIC=DIM_OVERALL_HEIGHT_VERIFIED` explain
  which geometry-based vertical heights were eligible.

**Engineering distinction:** an overall envelope height may include base
plates, fittings and offsets. Verify that the `H=...` dimension represents
the height you want documented. It must not be used as an exact steel cut
length. If a member is too wide, too short, unavailable, or without native
type evidence, the generator deliberately skips rather than guesses.
Dimensions remain static after a manual Ortho Update View. The Windows CI
tests cover the planner and boundary logic, but a copied Plant 3D 2025
project test is needed to validate real object types and placement.

## REV 22.42 — Fewer/repeated dimensions on Ortho sheets

The uploaded one-page Ortho PDF shows the same `828.28` dimension repeated
in TOP, BOTTOM, LEFT, RIGHT, FRONT and BACK. The default-ON **Auto DIMLINEAR**
option now uses **compact sheet-wide dimensioning**:

- Keep only **one representative dimension for each verified source length,
  physical horizontal/vertical orientation and Plant member category**
  (`STRUCTURE` versus `SUPPORT`) on the entire generated Ortho sheet.
  If multiple equivalent members share the exact displayed value
  (up to the existing 0.001 mm formatting), the duplicate is omitted.
- Prefer views in this deterministic order:
  **TOP, FRONT, LEFT, RIGHT, BACK, BOTTOM** regardless of the selection
  order in the persistent UI. If an earlier view cannot project and place
  a dimension safely, a later eligible view is considered.
- Different lengths, structural/support types and physical horizontal
  versus vertical features are **not** merged. Each retained dimension
  remains anchored to a real verified member with its own witness points.
- The existing native `RotatedDimension`, DIMSTYLE, millimeter value,
  collision/off-sheet checks, XREF transforms, source provenance, and
  `SPTC_AUTO_DIMLINEAR` layer are unchanged. BOP/TOS/pipe and XREF
  leader annotations, all ten selectable views and the four commands
  remain unchanged.
- `REV22_42_REPEAT_DIMENSIONS_SUPPRESSED` records how many duplicates
  were avoided; skipped items are logged as
  `SOURCE_ALREADY_DIMENSIONED_IN_...` or
  `EQUAL_LENGTH_ALREADY_DIMENSIONED_BY_...`.

**To apply to an existing drawing:** generate a fresh Ortho from the
updated DLL in a **copied project**. The plugin does not remove dimensions
from pre-existing DWGs. Dimensions remain static after manual native Ortho
Update View, as disclosed in REV 22.41.

## REV 22.41 — Optional Auto DIMLINEAR on verified Ortho members

The `SPTC_SUPPORTORTHO10` persistent SPTC palette has a default-ON
**Auto DIMLINEAR (horizontal / vertical)** checkbox. It runs after saved
native Ortho viewport orientation/scale and annotations are verified.

- Native Plant 3D **Structure Member** and **Support/Pipe Support** entities
  can receive a true AutoCAD `RotatedDimension` (native `DIMLINEAR`) when
  **explicit, verifiable Start/End points** exist in the source entity's
  actual properties. The same rule applies to recognized support/structure
  members in loaded, resolved nested XREFs; source endpoints are transformed
  to host WCS using the known XREF instance transform.
- Only **TOP, BOTTOM, FRONT, BACK, LEFT, RIGHT** orthogonal views are
  dimensioned, and only if the member projects without foreshortening
  into a **horizontal or vertical** axis in that view. Isometric views,
  diagonal/sloping projections and too-small projected lengths are skipped.
- Each native paper-space `RotatedDimension` uses the existing Ortho
  **DIMSTYLE** and layer `SPTC_AUTO_DIMLINEAR`. The **explicit millimeter
  model length** is applied as display text. AutoCAD's measured paper
  length would be wrong under a 1:N Ortho viewport scale.
- Dimensions stay **inside the actual native viewport frame**, and safe
  separation/collision checks skip overlapping or off-sheet proposed
  dimensions. This is an additional dimension layer: **native Ortho view
  geometry, SupportFocus, BOP/TOS, XREF filenames and member leaders are
  not changed.**
- No endpoint means **no dimension**, with `REV22_41_DIM_ENDPOINTS_NOT_DECLARED`
  or `REV22_41_SOURCE_DIAGNOSTIC` in the result TXT. We never infer the
  member cut length from 3D geometric bounding boxes, nor assume that a
  generic solid/insert is a verifiable Plant support.
- All objects are appended **only to the generated Ortho layout**. Model
  DWGs, source XREFs and PnP rows are read-only. The existing four
  AutoCAD commands remain unchanged.
- Each dimension and reason for skipping is logged using `REV22_41_...`
  fields, including `REV22_41_DIMENSIONS_CREATED`.

**Caution:** Auto DIMLINEAR uses genuine native dimension entities but the
dimension text is a **model-length override**, not a DIMASSOC association
to the 3D source. If someone manually **updates a native Ortho view**,
these annotations **do not automatically refresh**; re-run the plugin
for a fresh drawing or verify/edit them manually. This release has Windows
CI regression tests, but should be tested in a **copied Plant 3D 2025
project** with sample native and XREF structural members before production
use. The internal Autodesk native Ortho generator remains unsupported.

## REV 22.40 — Persistent dockable SPTC palette

Running `SPTC_SUPPORTORTHO10` now opens or focuses a single **modeless,
dockable AutoCAD PaletteSet**. The manager is kept in memory through the
AutoCAD session, and does **not** close after **Run selected**. Use its close
button to hide the palette. Running the command again reopens/reuses the same
palette and retains the configured views, custom Paper Scale, seed, naming,
SupportFocus and annotation switches.

- The palette initially floats at a practical fixed layout size; it can be
  docked to the left/right AutoCAD edge. AutoHide is not enabled.
- Click **Run selected** to queue the chosen operation in a proper AutoCAD
  command context. Plant 3D selection stays in the drawing/editor, while the
  manager remains open. It is **not** run directly from a WinForms click.
- The same four commands remain: `SPTC_SUPPORTORTHO10`,
  `SPTC_SUPPORTEXPORT`, `SPTC_SUPPORTBATCH`, `SPTC_SUPPORT2DBATCH`.
  No extra CAD commands are added.
- **Run selected**, Preflight, and settings are disabled during an active
  operation to prevent overlapping submissions. They become editable again
  after it finishes, fails or is cancelled.
- A repeated `SPTC_SUPPORTORTHO10` focuses the existing palette without
  creating a second panel. The form retains its options between runs.
- Changing to a different project requires opening the command again;
  manual seed selections from another project are reset for safety.
- The existing generation engine, the automatic native 10-view placement,
  XREF/native pipe and structural annotation logic, BOP/TOS source metadata,
  project registration/overwrite protections and branded logo are unchanged.

**Important:** this release is compiled and has a WinForms persistence/busy
smoke test but must be tested in a **copied Plant 3D 2025 project**. Autodesk
native Ortho generation still uses an unsupported internal API, and the CAD
PaletteSet/command interaction cannot be fully simulated in GitHub CI.

## REV 22.39 — Custom 1:N paper scale in WinForms

In the branded `SPTC_SUPPORTORTHO10` > **10-View Ortho** tab,
**Paper scale** now offers `Custom 1:N...` alongside all the existing
`Automatic` and preset values. Selecting **Custom** reveals a numeric
denominator field, default `25`. Enter **N** to request an exact `1:N`
scale, e.g. `37` for `1:37` or `125` for `1:125`.

- Positive **integer** denominators **1 through 1,000,000** are accepted;
  zero, negative values, invalid entries and over-limit values are rejected.
- The Preflight / configuration summary displays the resolved `1:N` value.
- The existing read-only native sheet-fit and title/viewport collision
  preflight rejects unsafe magnification **before** creating or registering
  a new native Ortho DWG. A requested scale smaller than the calculated
  minimum denominator cannot be forced through.
- Exact custom N is forwarded to the existing native camera,
  title/viewport layout, and registration pipeline. **Automatic** mode
  remains the default and preserves the original scale behavior.
- No changes to geometry extraction, XREF discovery, structure/support
  or pipe callouts, BOP/TOS formatting, the SPTC Engineering logo,
  the other three export commands or the total four AutoCAD commands.

The UI becomes slightly taller to accommodate the custom input without
overlapping existing controls. Windows CI tests preset compatibility,
custom denominators, invalid input and unchanged sheet-fit guards.
Test generated native view scales visually inside a copied Plant 3D 2025
project before using this new option on production drawings.

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

Standard Windows GitHub Actions runner, .NET 8 x64, source/geometry regression smoke tests, UI-subset title/callout smoke tests, exact four-command CI allowlist, DLL artifact `SPTC-SupportDrawings-Plant3D2025-REV22_44-ALL-VIEWS-HV`.

Rollback source is preserved in repository Git history and the [full pre-cleanup archive](https://github.com/nichslos/SPTC-SupportDrawings-Plant3D2025/tree/archive-rev22-30-full-test-history).
