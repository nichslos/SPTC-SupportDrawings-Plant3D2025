# REV64 — One native paper sheet for all ten views at an exact 1:N scale

## User requirement

Generate **one** native Ortho DWG and **one** PDF containing
TOP, BOTTOM, LEFT, RIGHT, FRONT, BACK, NW, NE, SW and SE, at the
user's exact manually selected 1:N paper scale. Do **not** replace 1:10
with 1:25, silently shrink viewports, crop out of the border, or
automatically split into multiple drawings.

## Why TEST5 needs larger media at 1:10

The supplied REV63 TEST5 run, on its existing **841 × 594 mm A1
landscape seed**, passed only as two individually verified sheets:
- S01 / six orthographics: 6/6 native views, 30/30 annotations,
  25/27 native dimensions and 12/12 required overalls, PDF verified.
- S02 / four isometrics: 4/4 native views, 20/20 annotations,
  zero required H/V dimensions, PDF verified.

The existing exact-scale planner's strict projected frame, caption and
right-side title-block keep-out calculations reject the ten-view group
on that A1 medium. With TEST5-like geometry, the same calculation
permits **one 1189 × 841 mm A0 landscape sheet using three columns**.

## REV64 behavior

- **Automatic (1:25) All10** remains on the original fast first
  registered empty Ortho seed. This preserves the verified REV59
  A1 baseline, paper viewport placement and PDF publisher.
- **Manual 1:N All10** selects **the smallest project-registered, empty,
  native Ortho seed that passes a read-only exact-scale ten-view fit**.
  It inspects effective plotted sheet orientation/media and uses the
  existing REV61 safe paper frame/title/annotation clearances.
  The candidate must be in the project Orthos directory, appear in
  the native Ortho Project Manager register as EMPTY, and pass
  OrthoDocUtils native validation.
- **If a fitting seed exists**, create **one** registered native Ortho
  DWG and the existing optional **single** verified native PDF,
  with ten preset native view names, their exact 1:N CustomScale,
  existing H/V native dimensions, title-band validation, QA status,
  title-block configuration (when mapped), and normal sidecars.
- **If no fitting seed exists** (or user manually chooses a smaller
  unsuitable seed), fail **before Ortho cloning/registration or
  publication**. A concise command message requests a properly plotted,
  registered empty A0 landscape or larger seed. Do not fall back to
  REV63 automatic 2-sheet processing.
- The existing REV63 experimental multi-sheet pure planning code
  is retained for historical testing, but no longer called from the
  production palette's default run.
- Printed paper size is never silently modified inside an A1 seed:
  an eligible A0 drawing must have the corresponding actual plot
  media, viewport border and title block already configured.

## On-machine TEST5 acceptance

1. In a **copied** Plant 3D 2025 project, create/register an empty
   native **A0 landscape (1189 × 841 mm)** Ortho drawing using
   Plant 3D Project Manager. Verify the layout's plotted media and
   actual border/title block match A0. Keep the existing A1 seed.
2. Load the REV64 DLL using NETLOAD. Run SPTC_SUPPORTORTHO10,
   select support handles 8A3 and 8A4, choose **1:10**,
   click **All 10**, and keep automatic registered empty seed on.
3. The command must select the A0 seed, log
   `REV64_ONE_NATIVE_SHEET_REQUIRED=True`,
   `REV64_SELECTED_PAPER_MM=(1189,841,0)`,
   `STEP4_COMMON_SCALE=1:10`, and register **one**
   new Ortho DWG containing exactly all ten native views.
4. Verify single native PDF with **one printed A0 page**;
   neither the 25% right title-block area nor annotation/title
   gutters may be invaded. Confirm the DWG/PDF's actual plot size
   and viewport CustomScale=0.1 for every selected view.
5. Inspect all 50 expected native labels, essential dimensions,
   quality audit, drawing-number metadata and title-block fields.
   Static seed text `SPTC_NATIVE_SEED_01` / `####` is not
   automatically mapped to attributes without appropriate title
   block ATTDEF/ATTRIB definitions.
6. Test native `PLANTORTHOUPDATE` on the new registered A0
   Ortho DWG and inspect regenerated PDF and dimensions.
7. Negative control: with **only** the A1 seed registered and
   **1:10 All10**, preflight must create **zero** drawings or PDFs,
   report that a fitting larger registered seed is needed, and
   must not create REV63 S01/S02 outputs.
8. Regression: A1 Automatic All10 still produces **one 1:25
   drawing** with REV59 view positions and PDF publisher unchanged.

**Limitation:** One 1:10 sheet is contingent on actual paper size.
No software can keep the physical model at true 1:10, retain readable
geometry and legends, and guarantee an arbitrary support fits within
every fixed A1 border. Compilation and synthetic geometry tests
cannot replace an actual Plant 3D 2025 runtime PDF/Update View check.
