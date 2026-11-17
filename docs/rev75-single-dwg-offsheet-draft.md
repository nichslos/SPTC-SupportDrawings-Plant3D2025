# REV75 — Keep all ten Ortho views on one native DWG, draft overflow outside paper

## User request / observed layout

The user supplied a Plant 3D paper-layout screenshot containing FIVE small native views.
They want all **TEN** principal/isometric native views within ONE DWG,
with annotation bands/leader geometry outside their respective viewports;
if the physical paper is insufficient, allow the overall view group to
extend outside the real paper border instead of splitting it across DWGs.

This is distinct from REV74's separately plotted safe multi-sheet fallback.

## New opt-in operation

The Ortho tab adds **All 10 on one DWG, outside paper (DRAFT)**.

- Default is OFF and it is never persisted on project-settings Load/Save.
- Mutually exclusive with REV74 **split across sheets**.
- Available only for a non-batch selection of all ten named native
  views at an explicitly selected 1:N scale.
- Existing physical fit attempts and smallest suitable registered EMPTY
  Ortho seed still run first. Only after no physical seed can fit does
  automatic selection opt into REV75 virtual drafting space.
- A valid registered EMPTY native Ortho seed is still required.
  When none fits, REV75 uses the verified seed with the largest actual
  plot area, preserving source and actual seed media.
- The planner derives a virtual layout canvas by expanding the actual
  seed's measured width/height until REV61/65's unchanged ten-view
  exact-scale layout, native title, paper-space overlap, and REV71
  annotation capacity preflights pass. No source-specific handles, layout
  dimensions, plot scales, coordinates or paper format names.
- After cloning the seed, the same virtual coordinates drive native
  Ortho viewport insertion, persistence checks and annotation layout;
  the actual source sheet's plot settings are NOT enlarged or rewritten.
  Native camera directions, common exact 1:N and non-overlapping
  viewport and caption checks remain mandatory.
- Explicit per-output report lines:
  `REV75_DRAFT_OVERFLOW_ACTIVE`,
  `REV75_VIRTUAL_LAYOUT_MM`,
  `REV75_ACTUAL_NATIVE_PLOT_MM`,
  `REV75_PLOT_FIT_STATUS`.
  The legacy physical-page PASS log lines only apply to the normal path.
- **No native PDF is published** from an offsheet virtual draft.
  Resulting production sidecar is `DRAFT_OVERFLOW_NOT_FOR_ISSUE`
  with `QaStatus=REVIEW`. A formally issuable plotted drawing requires
  changing to a physically large enough registered paper or using REV74
  split across separately validated sheets.
- A physical fit uses normal REV74 behavior without virtual expansion.
  A manual registered seed may also explicitly use the virtual
  planner; this is still draft-only and nonpublishable.

## Important limitation

This changes the paper-space *drafting canvas* only. The underlying
native Plant 3D paper layout remains its original physical size.
Offsheet views and labels can exist in a DWG but WILL NOT appear in a
conventional PDF plotted to that original media.

No tool can make ten large 1:10 views printable on a physically
undersized sheet without changing paper size, scale or view count.

## Testing

Windows CI compiles real Plant 3D 2025 DLL and exercises pure
virtual-canvas tests. Actual native behavior must be tested in a copied
Plant 3D 2025 project: select the same support, All10, 1:10, AutoSeed,
check draft overflow and turn REV74 split OFF. Confirm ten native
views in ONE DWG without viewport intersections, exact native 1:10,
proper view title positions, all existing annotations, and report
`OUTSIDE_REAL_PAGE_DRAFT_ONLY_NO_PDF`. Native GenerateOrthos remains
an unsupported Autodesk internal API, so workstation verification
is indispensable.
