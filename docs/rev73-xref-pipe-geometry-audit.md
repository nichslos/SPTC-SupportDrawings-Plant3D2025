# REV73 – measured XREF pipe geometry and exclusion diagnostics

## Reason for this diagnostic-only revision

The 2026-11-17 REV72 native Plant 3D source report identified one
`AcPpDb3dPipe` in a loaded nested XREF, but counted it as outside the
pipe discovery proximity region. It did not retain the verified pipe's
transformed world-space extents, so a user screenshot showing pipes
overhead could not establish whether the exclusion was caused by XY
separation, Z separation, or final OrthoCube filtering.

## Implementation

- Keep the exact REV72 `Scan()` recognition, existing 3D scan
  envelope, `SupportFocusedCubePlanner.BuildAutomatic()`,
  `NativeOrthoLayout.Build()`, and metadata authenticity requirements.
- For each verified source-native pipe for which `GeometricExtents`
  succeeds, retain read-only **host WCS** bounds after applying the
  actual nested XREF instance transform. The resulting evidence
  includes verified source file, nested instance/handle path, unit
  conversion and original WCS extents.
- Compare that same WCS pipe box against (a) selected support bounds,
  (b) REV72 pipe discovery scan region, and (c) the final native
  OrthoCube bounds. Emit minimum XY and Z separation in actual
  millimetres using the source drawing unit multiplier.
- Distinguish `SCAN_XY_EXCLUDED`, `SCAN_Z_EXCLUDED`,
  `SCAN_XY_AND_Z_EXCLUDED`, `ORTHOCUBE_XY_EXCLUDED`,
  `ORTHOCUBE_Z_EXCLUDED`, `ORTHOCUBE_XY_AND_Z_EXCLUDED`,
  and `IN_SCAN_AND_ORTHOCUBE`. Inclusive bounding-box contact
  follows the existing scanner's boundary rule.
- Emit `REV73_XREF_PIPE_GEOMETRY_SUMMARY` and
  `REV73_XREF_PIPE_GEOMETRY` in the AutoCAD command window and
  `*_RESULT.txt`. Source paths and dimensions are observed at runtime
  rather than coded into the plugin.
- A source pipe with unavailable geometric extents remains visible
  through the existing `PIPE_EXTENTS_SKIPPED` diagnostic; this
  revision does not fabricate bounds.

## Explicit non-changes

**No project-specific hard-coding.** No changed scan clearances, false
nearby pipe acceptance, added geometry to the cube, artificial BOP,
modified XREF or source DWG, PnP writes, new public commands, altered
native view layout, labels, dimensions, PDF publisher or file package.
The recorded shortest distance is **bounding-box separation**, not
pipe centerline-to-support distance or an optical estimate.

## Validation

Pure tests cover XY-only, Z-only and combined scan exclusions;
scan-accepted but cube-excluded cases; actual cube inclusion;
touching boundary behavior; unit-converted XY/Z gaps; coordinate
report format and read-only action indicator. Windows CI additionally
builds the real Plant 3D 2025 DLL.

Workstation verification: load the new DLL in a fresh Plant 3D process,
run the unchanged `SPTC_SUPPORTORTHO10` on the same selected support,
and compare `REV73_XREF_PIPE_GEOMETRY` lines and the generated PDF.
This revision intentionally does **not** assert that the pipe will
become visible: measured source WCS evidence must first establish
whether the current exclusion is correct.
