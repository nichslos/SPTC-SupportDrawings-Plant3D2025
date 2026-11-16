# REV59 — native Ortho landscape sheet coordinates and ten-view containment

## Live TEST5 cause

The Plant 3D 2025 `Title Block` layout uses raw plot media
`594 x 841 mm` with an effective 270-degree rotation.
In the native DWG screenshot for
`SPTC_TEST5_8A3_ORTHO_R229XREF_20261116_005117_e9d5.dwg`,
the actual border and right-hand title block form a **landscape
841 x 594 mm** sheet. The pre-REV59 SPTC ten-view planner used the
raw unrotated 594 x 841 media, so TOP/BOTTOM were generated above the
physical drawing border and LEFT/RIGHT crossed its top edge.
REV57 plotted the corresponding DWG accurately: its PDF was valid
and published successfully, but faithfully showed the pre-existing
off-sheet native Ortho view placements. Fix the DWG, not the PDF.

## Implemented scope

- In both read-only preflight and actual active Ortho DWG generation,
  compute **oriented paper-space dimensions** from real
  `Layout.PlotPaperSize`, `PlotPaperUnits` and `PlotRotation`.
  Swap width/height for 90 or 270 degrees. The observed media
  594x841, rotation270 therefore becomes effective 841x594.
- Use that SAME effective page size in 10-view scale fitting,
  automatic placement, titles, annotation/dimension/QA paper bounds
  and post-native viewport inspection; 0 and 180-degree layouts
  preserve their original physical media dimensions.
- Reserve 3% left, 25% right for the side title block, 12% bottom
  and 6% top; reject if any of 10 native view rectangles or view
  captions leaves these conservative safe bounds.
  Current 25% right keep-out is conservative for TEST5; it is not
  yet an automatic polygon detection of arbitrary title-block blocks.
- Check actual native viewport envelopes AFTER generation, not just
  requested centers, and reject out-of-bounds title placement.
  This intentionally does not drag/move Plant-generated registered
  viewports after the fact: modifying the native frame independently
  could corrupt native Update View behavior.
- The change is limited to the native ten-view paper-space planner,
  title keep-out and generator pre/post guards. It does not touch
  the REV57 native PDF publication and QSAVE implementation, native
  OrthoCube/source model, view enum/preset direction, XREFs, support
  annotations, DIMLINEAR calculation, four public SPTC commands,
  or `PLANTORTHOUPDATE` fast path.
- Automatic 1:N scale may legitimately select a larger denominator
  when fitting 10 rows into the actual 594 mm high sheet. Explicit
  1:N choices that do not fit must be rejected BEFORE drawing
  creation, rather than silently cropping views.

## Diagnostics

The new `*_RESULT.txt` records:
`REV59_RAW_MEDIA_MM`,
`REV59_NATIVE_PLOT_ROTATION_DEG`,
`REV59_EFFECTIVE_PAPER_MM`,
`REV59_TITLE_BLOCK_SAFE_RIGHT_MM`,
`REV59_PREFLIGHT_ALL_TEN_INSIDE_EFFECTIVE_SHEET=PASS`,
`REV59_ACTUAL_NATIVE_VIEWPORTS_IN_SAFE_SHEET=PASS`,
`REV59_ACTUAL_NATIVE_TITLES_IN_SAFE_SHEET=PASS`.

## Acceptance

1. Install this DLL on a copied Plant 3D 2025 TEST5 project,
   preserving the previous REV57 DLL as a rollback.
2. Run `SPTC_SUPPORTORTHO10`; select all 10 native views with
   automatic scale and enable draft PDF publishing.
3. The generated DWG `Title Block` layout should show all ten
   native viewports and captions within the landscape drawing
   border and to the LEFT of the side title block.
4. Confirm no overlapping viewports, no title-block overlap,
   legible dimensions and annotations, and a normal native
   `PLANTORTHOUPDATE` test (not just generated manifest).
5. Open the new PDF and visually inspect *all ten* views,
   boundaries, sheet orientation, scale, labels, dimensions and
   title block. A `PDF_PUBLISHED_VERIFIED` flag proves file
   integrity only, not visual completeness.
6. Review the dimension count; auto-scale/placement may differ
   from the earlier 25/27 baseline, but the dimension algorithm
   and required essential H/V coverage were not modified.
7. Title-block attribute placeholders (`NOT_CONFIGURED`) are
   a separate follow-up and are not changed by REV59.

Windows CI can compile/test the sheet policy but cannot inspect
Plant 3D's internal native Ortho viewports or plot your PC3 driver.
This fix is not considered visually verified until a new TEST5
DWG screenshot and generated PDF pass the acceptance checks.
