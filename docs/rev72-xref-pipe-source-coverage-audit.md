# REV72 – actual XREF pipe detection, evidence and independent coverage QA

## Grounded defect in REV71 workstation log

The user's TEST5 12-object support assembly, built on Plant 3D 2025:
- **XREF member checkbox was enabled** (`REV22_37_XREF_MEMBER_NAMES_ENABLED=True`).
- Five nearby XREF structural/support objects were found and two intersected the OrthoCube; twenty structure/source-drawing labels were generated.
- **XREF/native pipe scanner found zero pipes**, `REV22_29_PIPE_CANDIDATES=0`, `REV22_29_PIPE_ANNOTATION_SOURCES=0`, and `STEP10_PIPE_BOP_EXPECTED=0`.
- The separate *support-focus bounding-box* calculation said two candidates were accepted, but those were derived from a combined set of XREF structural members and cannot authenticate pipe entities.
- Ten native views, 80/80 structure labels, 40/40 TOS and PDF production QA passed **without establishing any XREF pipe coverage**. The user saw missing XREF pipes.

These logs do not prove that any actual native Plant 3D pipe exists in the currently loaded resolved XREF databases. They establish that **pipe discovery did not authenticate one**, which is different.

## REV72 scope

1. **Repair a definite fail-open defect**: `SupportOrthoPipeDiscovery28.Scan` reports `REV22_28_SCAN_FAILED`, but the generator previously checked only `REV22_29_SCAN_FAILED`, treating scan exceptions as "zero pipes." The generator now rejects a failed scan before registering any new native Ortho DWG.
2. **Scan and audit every actually loaded XREF** recursively and read-only. Report number of resolved/unresolved references, source DWG paths, true source entity class frequency, proxy-like entities, verified Plant pipe object classes, pipe entities within the geometric search region, and candidates outside the region. This is diagnostic evidence, not inferred pipe geometry.
3. **Recognize authentic XREF pipe identity** using Autodesk source runtime/RX/DXF class and managed type inheritance. The canonical Plant native pipe classifier already present in REV27 is reused. Do NOT guess native pipes from `Pipe` in a DWG filename, layer, generic `Solid3d`, XREF structure entity, support proximity, proxy, or an assembly handle. Unrecognized proxy classes remain explicit investigation items.
4. **Keep actual native OrthoCube and property verification separate**. Only real XREF pipe `ObjectId`s intersecting the final OrthoCube enter the XREF source PnP/Properties palette Line Number Tag/BOP extractor. Do not invent BOP from top/center pipe elevation or a nearest neighboring pipe, and do not modify referenced DWGs or PnP.
5. **Expose independent pipe coverage in RESULT.txt and AutoCAD command line**. New report includes `REV72_XREF_PIPE_SOURCE_COVERAGE`, `REV72_XREF_PIPE_COVERAGE`, `REV72_XREF_SOURCE_AUDIT`, source-pipe candidate/annotation counts, separate `REV72_XREF_PIPE_QA_SEPARATE_FROM_STRUCTURE`, and `REV72_XREF_PIPE_VISUAL_NATIVE_ORTHO_VERIFICATION`.
6. **No false PDF claim**: Structural drawing QA may still be `PASS` with zero XREF pipe sources; REV72 explicitly warns `NO_VERIFIED_NATIVE_PIPE_ENTITIES` (or the actual more precise reason) if the XREF member-name option was on and loaded sources are available. The PDF cannot be certified as containing XREF pipes simply because it passes structural QA. Actual native Ortho visibility still needs PDF/model overlay inspection.
7. If source pipe candidates have no usable native Line/BOP metadata, the existing source-authentication refusal remains. Partial metadata is diagnosed separately and does not produce a claim of complete XREF coverage. The original XREF member-name checkbox behavior is retained; the pipe scanner remains independent.

### Source coverage classes

- `SCAN_FAILED`: hard stop before registration.
- `XREF_NOT_LOADED`, `NO_RESOLVED_XREF_SOURCE`: check external references.
- `NO_VERIFIED_NATIVE_PIPE_ENTITIES`: loaded sources contain no recognizable native Plant pipe; class histogram points to possible proxies/generic drawings.
- `VERIFIED_PIPES_OUTSIDE_SCAN_REGION`, `VERIFIED_PIPES_OUTSIDE_ORTHOCUBE`: native source pipe exists but not eligible for this support.
- `XREF_PIPE_METADATA_NOT_VERIFIED`, `XREF_PIPE_ANNOTATION_COVERAGE_PARTIAL`: source pipe exists, but Line Tag and/or BOP evidence is incomplete.
- `XREF_PIPE_METADATA_COVERAGE_VERIFIED`: all in-cube source pipes have sufficient source metadata, **not proof that pipe lines were drawn in native Ortho PDF**.

## Safety / non-hardcoded constraints

No project-specific DWG names, source handles, pipe tags, object IDs, member counts, paper sizes or view presets. The existing REV71 geometry-derived ten-view native exact-scale planner, annotations, source H/V dimensions, 12/12 essential overalls, title-block bypass, production PDF QA, `PLANTORTHOUPDATE` fast path and four commands are unchanged.

## Workstation acceptance

1. Preserve REV71 DLL. Extract and load REV72 in a fresh Plant 3D session via `NETLOAD`.
2. Run `SPTC_SUPPORTORTHO10` on the SAME 12-object TEST5 assembly with loaded XREF sources and enable **Nearby XREF Member Names**.
3. Confirm the source per-XREF summaries and whether any true loaded source has recognized Plant pipe objects; if `NO_VERIFIED_NATIVE_PIPE_ENTITIES`, open the source DWGs, inspect the pipe with Properties, verify the current project XREF is loaded rather than an image/proxy or just a structural support drawing.
4. If any authentic XREF pipes are found, confirm `REV22_29_PIPE_ANNOTATION_SOURCES`, `STEP10_PIPE_BOP_EXPECTED` and all pipe labels, plus inspect that actual native XREF pipe geometry is visible in the generated PDF (rather than only annotations).
5. Verify unchanged REV71 annotation coverage, source-derived layout, exact 1:10, all ten views, published PDF and dimension QA. The source CAD model and title block must not be modified.
6. Send `*_RESULT.txt`, production JSON, CSV, and PDF. A real XREF source DWG or a read-only Type/Properties report will be required if all source XREF classes remain generic proxies; do not relabel plain solids as Plant pipes to force success.

CI checks source-evidence policy, regressions and DLL compilation; the actual XREF databases cannot be inferred from the user's REV71 drawing report alone.
