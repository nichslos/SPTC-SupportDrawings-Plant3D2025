# REV71 — geometry-driven native annotation capacity

## Grounded failure: larger 12-object assembly

Plant 3D 2025 REV70 TEST5 on 2026-11-17 selected 12 objects (eight native Plant PnP steel members, two in-cube XREF support sources, no native pipes). The original reserved-area preflight failed at 1:10. REV70 successfully bypassed the right-side reserved title-block and generated all ten *native* views on an A0 landscape paper. The output then blocked the PDF for missing **24 mandatory steel annotations**: 56/80 written, 28/40 TOS lines, with `TOP`, `BOTTOM`, `SE` reporting `NOT_ENOUGH_LABEL_HEIGHT`. All 12 essential overall dimensions, 0 witness intersections and 33 native dimensions were present; 9 optional dimensions were omitted.

The original callout planner computed `nextRowGap` as the nearest **vertical camera-center** separation across **all** cameras, even when viewports were in **different columns** and did not overlap a given label column. For the true 3×4 packing, a nearby row center of a different column collapsed the permitted label band, producing false insufficiency. This is NOT solved by further title-block, paper-size or dimension changes.

## Source-derived REV71 implementation

- A new *pure* `SupportOrthoAnnotationCapacity71` calculates the actual right-side annotation column and the **maximal free vertical paper intervals** using each existing native viewport rectangle, native title rectangle, earlier accepted label rectangles, effective registered physical sheet boundaries and typography line/row spacing.
- Both label dimensions and capacity derive from `sourceAnnotations` (Native PnP member/TOS + pipe/BOP), *not* preselected member counts, hard-coded handles, preset names, paper format names or special treatment of TOP/BOTTOM/SE.
- Retain the exact REV70 landing rows first if the old stack already fits **and** is geometrically collision-free. Only crowded views get an adaptive relocation to the nearest sufficiently tall, fully free paper interval in the same established right-hand annotation column. Source anchors and native camera/view positions are unchanged.
- Mandatory native structure/support/pipe labels are never discarded to make room for optional XREF source filenames. Drop XREF filename labels only if their complete stack cannot fit after securing all native annotations. A genuinely impossible native label capacity still fails rather than shrinking below validated typography or silently publishing an incomplete PDF.
- On explicit 1:N All10, preflight checks *mandatory source-derived text height* for every dynamic candidate grid and each project registered EMPTY native Ortho sheet **BEFORE new DWG registration**. If a grid has no sufficient physically safe label area, attempt the next candidate. Don't confuse native viewport/title physical fit with full member annotation fit.
- The native post-creation callout writer uses the same paper geometry and rechecks actual projected WCS source anchors and actual native view labels. The existing physical viewport/title/label collisions, 1:10 scale, 10 native views and QA PDF block are preserved.
- Source `RESULT.txt` now logs `REV71_MANDATORY_NATIVE_LABEL_STACK_MM`, `REV71_ANNOTATION_CAPACITY_POLICY=GEOMETRY_DERIVED_FREE_PAPER_BANDS`, `REV71_REQUIRED_MEMBER_ANNOTATIONS`, and `REV71_PLANNED_NATIVE_MEMBER_ANNOTATIONS`.

## Explicit exclusions

No hardcoded paper-size or view-name exclusions, no special support handles, no title-block display or ATTRIB edits, no native 1:N scale changes, no fewer views, no multi-sheet fallback, no unsupported API conversion, no changes to REV68 dimension source/witness validation, no PLANTORTHOUPDATE fast-path or production PDF publisher changes, and no override to QA for missing mandatory annotations.

## Test acceptance

- CI smoke tests preserve prior REV59/66/69/70 outputs and use a **real logged paper-frame fixture** from the 12-object 3×4 1:10 A0 sample. Production code itself never uses that fixture or its camera labels.
- Smoke asserts source-derived 8×10 native label count and 4×10 TOS line count, no viewport/title/label intersection, same source anchors, and fail-closed capacity for geometrically impossible assemblies.
- In Plant 3D rerun with the same 12-object support (handles 521,524,527,52A,52D,53F,542,545,548,54B,554,557), same registered EMPTY Ortho paper and 1:10 All10. Require `STEP10_MEMBER_ANNOTATIONS_CHECK=PASS` (80/80 mandatory), 40/40 TOS, all ten exact 1:10 viewports, 12/12 essential overall dimensions, no witness collisions, correctly saved DWG and a verified published PDF. Inspect the existing printed title-block region for overprint before issue.
- The independent Windows CI cannot substitute for on-machine Plant 3D PnP/native view and plotter validation; claim actual runtime PASS only after that user's new log and PDF.
