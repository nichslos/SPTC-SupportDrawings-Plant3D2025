# REV61 — exact paper-scale presets, selection-aware placement, and physical A1 limits

## Reason

The REV60 preflight refused every manually selected denominator smaller than
its conservative **automatic 10-view** denominator (for TEST5: 25), before
checking the selected view set or physically proving the actual 1:N frame fit.
This wrongly treated `1:20` and even a six-view `1:10` configuration
as categorically impossible.

## Implemented

- **Automatic**: preserves the verified REV59 two-column/five-row native A1
  placement and the existing automatic scale (TEST5: 1:25).
- **Manual presets and Custom 1:N**: use their exact requested denominator
  for both preflight and actual native generation. Never silently rescale.
- When the selected denominator magnifies beyond the conservative automatic
  ten-view fit, run the **same bounded physical paper-space packing** in
  the read-only seed preflight and in the generated Ortho drawing.
  Try grid layouts with 2, 1, 3, or 4 columns using the actual selected
  native views, projected model extents, the requested exact scale, the
  existing title width/height, 3% left / 25% right / 12% bottom / 6% top
  paper keep-out, and a conservative 136.5-mm right annotation clearance.
- Validate each title and viewport before native project registration.
  Preserve original camera, OrthoCube, view type and 1:N ratio for every
  actually generated native view; the same paper geometry is used for
  dimensions and labels. Do not create a partially clipped DWG.
- Strong physical limit: even the largest selected scale is *accepted by the
  UI*, but cannot be placed on a specific fixed sheet if the model cannot fit.
  For 1:1 on a ~1.25-m support, a single TOP view alone is taller than
  A1's ~487-mm safe height. Use a larger **registered empty native Ortho
  seed with matching plot media**, or split into suitable view sets.
  True multi-page tiled Ortho PDF publishing is **not implemented**.

## Regression examples

A simulated TEST5-sized model on the 841×594 mm effective A1 sheet:
- AUTO 10 views: legacy 1:25 remains unchanged.
- Explicit 1:20 and all 10 views: adaptive safe grid fits at true 1:20.
- Explicit 1:10 and six orthographic views: adaptive safe grid fits at true 1:10.
- Explicit 1:10 and all 10 views: safely rejects if the five-row 1:10
  layout (and alternate grid candidates) cannot meet title/annotation clearance.
- Explicit 1:1 and one TOP view: rejects on A1 but can fit when a sufficiently
  large registered custom sheet is provided.

These are pure planner regression tests, **not** a claim of successful
AutoCAD Plant 3D native runtime generation at each scale.

## On-machine acceptance

1. Keep a backup of REV59/REV60 DLL and a copy of TEST5.
2. Run `SPTC_SUPPORTORTHO10` with auto 10 views and compare the 841×594
   native Ortho/PDF placement against REV59.
3. On a separate copied run, choose **1:20 all 10**. Confirm log
   `REV61_SELECTED_VIEW_ADAPTIVE_LAYOUT=True` and
   `REV61_NATIVE_SCALE_PRESERVED=1:20`, actual viewport
   CustomScale=.05, titles, DIMLINEAR, and native Edit View.
4. Try **1:10 Ortho 6** and verify six registered views and paper-space
   positions. Do not force ten 1:10 views into A1; select a larger sheet
   if physically necessary.
5. Test Custom 1:N, verify the exact denominator is not silently altered.
6. Inspect produced PDF, OrthoDWG and `PLANTORTHOUPDATE` and the
   usual RESULT/production/register sidecars. Do not issue drawings
   with native Update View or QA not yet verified.
