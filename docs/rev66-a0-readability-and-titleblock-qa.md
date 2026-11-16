# REV66 — A0 typography, nonwrapping native captions, safe optional dimensions and title-block status

## Verified baseline and scope

The actual TEST5 generated on 16 November 2026 at 06:22:16 UTC contains **one A0
native Ortho DWG (1189×841 mm), all ten views at exact 1:10, 50/50 annotations,
25/27 auto dimensions, 12/12 essential H/V overalls, zero reported witness
intersections, QA PASS and a verified single-page PDF**. It used registered
empty seed `test1.dwg`. It remains a **draft**; the title block still visibly
contains `test1` and `####`, and it has no editable ATTRIB tags.
The minimum actual printed annotation text height was 1.593 mm and ten labels
had to shrink. Several slim-view captions wrap into two lines.

REV66 improves presentation **without changing** the one-sheet native
10-view source/camera, OrthoCube, paper scale, registered-seed detection,
view placement, PDF plot path, or PLANTORTHOUPDATE fast path.

## What changes

1. For **A0 landscape (at least 1000×700 mm)** only, plan wider external
   callout lanes (up to 160 mm instead of 114), 3.5 mm two-line spacing
   instead of 2.7, and 2.2 mm row gaps instead of 1.8. The native AutoCAD
   DBText writer targets **2.15 mm**, measures glyph extents, and only
   accepts a font that fits the label rectangle. It may still shrink as
   low as **1.50 mm** when actual AutoCAD text metrics require it; that
   remains explicitly flagged for visual review. No fallback silently
   overlaps native geometry/title/labels.
2. Native A0 view titles reserve an independently wider caption band
   (`MinimumFrameWidth(view)+14 mm` when larger than the viewport).
   The **viewport** remains unchanged. A0 MText `ActualHeight` is tested
   against the reserved 6 mm caption height; the title is shrunk (not
   lower than 3.0 mm) to remain **one line**. If no legal line fits,
   generation fails closed rather than issuing an overlapping title.
3. When the local optional dimension optimizer runs on A0, increase
   its bounded trial budget from 14 to **20** per native view. The
   existing candidate collision tests and **add-only/preserve all
   accepted source witness and 12/12 essential overall dimensions**
   remain mandatory; the two missing 8A4 LEFT/RIGHT local vertical widths
   are NOT promised to be recovered.
4. Read-only audit of the **actual generated title-block definition**
   reports direct static DBText/MText counts, obvious placeholders,
   existing ATTRIB tags, and a template action. Static `test1`,
   `####`, approved text, and XREF title blocks are never guessed or
   overwritten. Existing opt-in exact `FIELD=TAG` attribute stamping
   remains available for an approved template with editable ATTDEF/ATTRIB.
   If none exist, `TitleBlockStatus=NOT_CONFIGURED` remains truthful.

## Not included

- No automatic rewriting of static title-block definitions (not safe
  without explicit field→entity mapping and template approval).
- No claim that 27/27 dimensions or 2.5 mm minimum text will be achieved.
- No automatic conversion of A1 to A0, rescaling from 1:10 to 1:25,
  multi-page output, or new production commands.
- No modification of the source project drawings, native PDF publisher,
  or update-view dimension/witness policy.

## Workstation acceptance checklist

1. In a **copied** TEST5 project, load the REV66 DLL via `NETLOAD`.
   Select support members 8A3 and 8A4, **1:10 + All 10 + automatic registered
   empty seed**, with the existing A0 `test1.dwg` candidate.
2. Verify **ONE** registered native DWG with all ten native preset
   directions, viewport CustomScale=0.1 and **ONE** A0 PDF.
3. Compare the exact native viewport positions and source OrthoCube
   against REV65; **no unapproved movement or scaling** is acceptable.
4. Check `STEP10_INDIVIDUAL_MEMBER_ANNOTATION_COUNT=50`,
   `STEP10_VIEW_TITLE_COUNT=10`, `REV66_A0_LABEL_PREFERRED_TEXT_MM=2.15`,
   `REV60_ANNOTATION_MIN_RENDERED_TEXT_MM`,
   `REV66_LABELS_BELOW_2MM_REVIEW_REQUIRED`, and the new A0 title
   setting. Inspect all 50 printed labels and 10 **single-line** titles
   at true A0 size, particularly TOP, BOTTOM, FRONT and BACK.
5. Confirm 12/12 essential dimensions, zero witness intersections,
   no new label/viewport or label/title collisions, and record any
   change from 25/27 optional coverage. This revision never forces
   the missing 8A4 widths over other content.
6. Inspect `REV66_TITLEBLOCK_CANDIDATE` and its
   `STATIC_TEXT_COUNT` / `OBVIOUS_STATIC_PLACEHOLDERS` fields.
   If `ATTRIB_TAGS=NONE`, modify an **approved copy of the actual A0
   native seed template** in Plant 3D to contain appropriately tagged
   ATTDEFs and create/register a new empty Ortho seed. Only then map
   `DrawingNumber=...;Revision=...;Title=...` using genuine tags
   through Production Package settings and rerun. Do not manually
   replace or guess `test1`.
7. Confirm QA PASS, saved DWG verification and actual
   `PDF_PUBLISHED_VERIFIED`, then run native `PLANTORTHOUPDATE`
   on the generated Ortho DWG to verify the persistent dimensions
   and titles. Keep issue status DRAFT until the native Update View,
   title-block attributes and print legibility are verified.
8. Regression: A1 Auto All10 retains the exact REV59/REV65
   1:25 view placement and 1.75 mm annotation style.

## CI limitation

Pure smoke tests verify A0 vs A1 spacing, safe exact 1:10
placement, caption width and static placeholder policy. Windows
compilation is not a substitute for real Plant 3D 2025
`MText.ActualHeight`, printed glyph widths or native Update View
verification.
