# REV63 — Automatic native multi-sheet support at exact custom/preset 1:N

## Source issue

REV62 TEST5 proved **1:10 ISO4** passes all native view/annotation QA and
publishes a verified PDF, but **1:10 All10** fails A1 single-sheet preflight.
The project A1 landscape plotting area is constrained by a right title-block
region, annotations, titles and a conservative view/caption clearance.
This is not a Plant 3D failure or a request to change 1:10 to 1:25.

## REV63 implementation

When the user selects **All 10** at an explicit paper scale and the
exact-scale preflight cannot safely place all views on one registered native
Ortho seed, the original single-sheet planner emits a typed physical
fit failure. REV63 then checks the **entire sheet set** in memory *before*
cloning, registering or writing any new DWGs:

1. Prefer one native sheet with TOP, BOTTOM, LEFT, RIGHT, FRONT, BACK.
2. Prefer a second native sheet with NW, NE, SW, SE.
3. If either subgroup cannot fit, divide that subgroup into two further
   groups and check each, recursively, with the same strict original
   single-sheet frame, title, and annotation keep-out rules.
4. If even ONE native view does not physically fit the registered sheet at
   exact 1:N, reject the ENTIRE request, without registering a partial
   sheet set. The correct remedy is a larger registered EMPTY Ortho seed,
   not hidden rescaling or paper crop.
5. Only after all pages fit, process the same selected host-source object
   IDs sequentially using the existing native GenerateOne and verified
   QSAVE/PDF publisher. Do not re-prompt for the same support assembly.
6. Each output gets a distinct filename prefix `S01_OF_02` / `S02_OF_02`
   and unique `-S01` / `-S02` production drawing number. Original
   selected view names, native camera presets and exact 1:N are preserved.
7. Write a standalone `SPTC_REV63_SHEETSET_*.txt` and matching
   `.register.csv` with every produced sheet's actual DWG/PDF status.
   An incomplete sheet stays visible and cannot be misreported as a
   complete successful issue set.

The **automatic** (not manual) 1:25 ten-view REV59 placement still uses the
previous native one-sheet path. Explicit 1:N requests that fit on one sheet
also preserve their original one-sheet path. No modifications to the
REV57/59 PDF publisher, native PLANTORTHOUPDATE fast path, dimensions,
C# command names, or the source project model are intended.

## TEST5 acceptance in AutoCAD Plant 3D 2025

- Use a *copy* of the TEST5 project. Load REV63 DLL via NETLOAD.
- Select **1:10 + All 10**, with source members 8A3 and 8A4.
- Expect the command to announce two preflight-verified sheets:
  `S01_OF_02` (Ortho6), `S02_OF_02` (ISO4).
- Verify **both** native DWG files are registered in the Project Manager;
  each has actual viewport scale 0.1; 6/6 and 4/4 saved native view
  presets, respectively.
- Ortho6: expect all 30 labels (including TOP and BOTTOM), twelve
  essential H/V overall axes, dimension QA PASS, saved DWG and
  `PDF_PUBLISHED_VERIFIED` when enabled.
- ISO4: expect all 20 labels, `REV62_DIMENSION_QA_MODE=
  ISO_ONLY_NO_HV_AXES`, 0 required DIMLINEAR, QA PASS and PDF verified.
- Verify no title, callout, border or right-side title-block overlap;
  currently the title-block seed's `ATTRIB_TAGS=NONE` placeholders remain
  unchanged and must not be presented as completed production metadata.
- Inspect sheet-set TXT + CSV. Drawing numbers must be unique with
  `-S01` and `-S02`. A failed page must be explicitly recorded and
  the overall set marked partial/failed, not issue-ready.
- Test native PLANTORTHOUPDATE on EACH newly registered Ortho DWG.
- Regression: Automatic All10 1:25 must still produce **one**
  verified drawing. Explicit All10 at a fitting scale also remains one
  drawing. A1 at 1:1 for this support must be rejected before writes
  if even one projected native view cannot safely fit.

## Explicit limitations

**This is not one combined multipage PDF.** It creates separate native
Ortho DWGs and their individual native PDF plots, plus a cross-sheet
register. Combining them into an issue-ready multi-page PDF remains
separate work. Exact-scale 1:1 cannot be guaranteed on A1 if an individual
projected view is physically larger than the printable area. Native
Plant 3D generation, PDF appearance and Update View must still be
verified on the user's workstation; CI only runs pure source tests and
compilation.
