# REV62 — TEST5 1:10 Ortho6 annotation gutter + ISO4 QA/PDF preflight

## Exact observed inputs

2026-11-16 TEST5 on REV61, both requested `1:10`:
- `022315_ee8d` selected TOP/BOTTOM/LEFT/RIGHT/FRONT/BACK.
  Six native views registered, scale preserved. The right neighbor
  search used `abs(centerYDelta) < Max(viewportHeights)`, which
  incorrectly treated the disjoint *next* row as a horizontal neighbor,
  producing `TOP:NO_SAFE_TEXT_GUTTER` and
  `BOTTOM:NO_SAFE_TEXT_GUTTER`. The result had 20/30 annotations,
  dimensions 25/27, essential overalls 12/12, generation status FAIL,
  and PDF blocked.
- `022458_3a44` selected NW/NE/SW/SE. Four native views and 20/20
  annotations succeeded, actual 0 DIMLINEAR (as expected for ISO).
  QA erroneously raised `NO_VALID_ORTHOGRAPHIC_VIEWS`, marking
  REVIEW and blocking native PDF.
- Both runs reported `TITLEBLOCK_STATUS=NOT_CONFIGURED`; the paper
  candidate `BLOCK=Title Block;ATTRIB_TAGS=NONE` has no editable
  attribute references. Mapping or automatic title filling is *not*
  claimed here.

## Focused source changes

1. When deciding whether a paper viewport is a right-side neighbor,
   use **actual vertical rectangle overlap**
   `abs(y1-y2)<(height1+height2)/2`, rather than the larger viewport
   height. The existing independent viewport/title/annotation collision
   validation is still mandatory. No changes to cameras, text sizes,
   PDF scale, native dimensions or automatic REV59 view placement.
2. In the independent dimension audit, a nonempty native viewport set
   containing **only unique, finite, positive-scale ISO presets**
   `NW/NE/SW/SE` has zero required orthographic H/V dimensions.
   Still flag unrecognized/invalid cameras, missing vetted members,
   unsupported isometric DIMLINEAR, duplicate source keys, writer
   mismatch, and any other existing integrity issues.
3. Log `REV62_DIMENSION_QA_MODE=ISO_ONLY_NO_HV_AXES` on an ISO-only
   selection and retain `REV22_61_QA_STATUS=PASS` only when all
   relevant checks pass. The unchanged production policy then permits
   the **existing** PDF publisher to run, subject to DWG verification,
   configured title-block mapping, real native plot and PDF validation.
   PDF verification is not automatic proof of engineering issuance.
4. Add pure regression tests reproducing the exact 1:10 Ortho6 camera
   frames, five labels per view, the 1:10 ISO4 audit, and PDF policy
   gates.

## Validation checklist

- With a copied TEST5 model: Ortho6 `1:10`, check
  `STEP10_MEMBER_ANNOTATIONS_CHECK=PASS` and 30 native labels,
  plus 25 DIMLINEAR and 12/12 essential overalls.
- ISO4 `1:10`: check four native preset views, twenty annotations,
  `REV62_DIMENSION_QA_MODE=ISO_ONLY_NO_HV_AXES`,
  `REV22_61_QA_STATUS=PASS`, 0/0 dimension overalls. This does
  *not* imply ISO dimension lines should be generated.
- Confirm native PDF publication with verified bytes and persisted
  `production.json`/`register.csv` in both runs; no false PDF PASS
  when PlotEngine or QSAVE fails.
- Confirm REV59 automatic all-ten `1:25` remains unchanged and
  native `PLANTORTHOUPDATE` retains the fast path.
- Examine the actual PDF to ensure no label intrudes on another view
  or the right-side title block before accepting production output.

**Out of scope:** automatic multi-sheet tiling for 10 views at 1:10,
changing A1 geometry/scale to fit impossible layouts, and converting
static title-block text into mapped ATTRIBs.
