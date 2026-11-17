# REV69 — Bypass reserved title-block AREA for large exact-scale A0 Ortho layouts

## Explicit user request

The user tested a **different support assembly with 12 selected structure/support objects, 8 PnP linked members and 5 nearby XREF structure objects**. REV68 correctly rejected a **single A0 at exact 1:10, all 10 views** because the best **3-column × 4-row** layout required **1021.9 × 692.3 mm**, versus **854.1 × 687.6 mm** allowed by hard-coded 75%-right-side title-block and 12%-bottom reservations. That is a *paper-space planning restriction*, not a model-geometry failure. The user explicitly instructed the plugin to **bypass the reserved title-block area**, but **not edit the title-block display**.

## REV69 implementation

1. **Strict old planner first**. Preserve previously successful REV68 8A3/8A4 TEST5 drawings, REV59 A1 automatic generation, every native axis view preset, source OrthoCube and the existing exact manual 1:N scale without relocation.
2. If **explicit manual All 10 views** at 1:N fails the strict registered empty A0 sheet layout, try a **bounded physical-page fallback** for a landscape paper layout of at least 1100×800 mm:
   - left = **3%** of actual oriented paper width, unchanged
   - right = **97%** of paper width (previously 75%, i.e. more space across the printed title-block side)
   - top = **94%** of paper height, unchanged
   - bottom = **10%** of paper height (previously 12%, yielding 2% additional row height).
   - On A0 1189×841, available grid width (before the existing 2-mm planner buffer) grows from ~856 mm to ~1118 mm; available grid height from ~690 mm to ~706 mm, making the reported 1021.9 × 692.3 target *plausibly* within physical bounds. True title and native viewport collision validations still decide the result.
3. The alternate layout is **one native Ortho DWG + one PDF**, with the **same ten true Plant 3D native view presets and exact 1:10**. It never scales down the request, chooses fewer views or creates split drawings. The exact-scale planner, registered-empty-seed discovery and preflight all use the same fallback policy.
4. The alternate layout passes through **all** physical-page checks: native title band geometry, actual AutoCAD paper Viewport position/scale, after-generation registered view verification, separate leader/callout spacing, existing DIMLINEAR source/witness/overall collision checks and the normal QA/publisher chain. On a default strict sheet, these existing checks are unchanged.
5. **Printed title block is NEVER edited.** Its title, numbers, `test1`, `####`, layer, block data, attributes, field mapping and project registration are identical to REV68. The fallback permits *paper graphics to share the title-block region*, so a native viewport or label may be **drawn over the printed title-block graphics**. This is deliberate per the user's bypass request; the tool must disclose `REV69_TITLEBLOCK_OVERPRINT_POSSIBLE=True` whenever the accepted frame/title crosses the old 75% boundary. **DRAFT + PDF visual review required**. The plugin does not pretend the title block disappears or has been safely relocated.
6. **New RESULT.txt diagnostics**:
   - `REV69_TITLE_BLOCK_RESERVED_AREA_BYPASSED=True/False`
   - `REV69_EFFECTIVE_RIGHT_MARGIN_MM` and `REV69_EFFECTIVE_BOTTOM_MARGIN_MM`
   - `REV69_TITLEBLOCK_OVERPRINT_POSSIBLE`
   - `REV69_PRINTED_TITLE_BLOCK_UNMODIFIED=TRUE` on bypass
   - Seed preflight `REV65_SEED_CANDIDATE=...` with `REV69_BYPASS_FIT` for a registered, valid, empty physical A0 candidate, and explicit user-facing warning
7. Any source/view genuinely too large for **97% page width + 84% usable height**, any off-sheet actual native viewport, intersecting native viewports, overlapping captions, no safe annotation gutters or unacceptable DIMLINEAR crossings **still fails closed**. No infinite retries or fabricated success. Proper registered larger paper is the fallback when physical constraints remain impossible.

## Workstation test

1. Copy the TEST5 project before loading REV69. Retain REV68 DLL and the existing approved registered empty A0 landscape seed.
2. In a clean TEST5 drawing, first reproduce the previous **12 selected structure members** + nearby **5 XREF** support objects with **1:10 + All 10 + AutoSeed or the registered empty A0 seed**.
3. Confirm the strict attempt is NO_FIT, the fallback reports BYPASS_FIT and **no new DWG is registered until fallback preflight passes**.
4. Verify exact 1:10 on all 10 real native viewports, zero actual native overlaps/outside physical page, all ten caption bands and source annotations; compare native dimension results and 12/12 essential dimensions. Inspect the PDF for **title-block overprint**, label readability and title collisions, especially in the far-right column.
5. Check `REV69_TITLEBLOCK_OVERPRINT_POSSIBLE` and the project-generated PDF, do not issue it without checking.
6. Repeat original successful **8A3+8A4 TEST5 1:10 A0**: strict-first must produce the same layout, labels, and scale; `REV69_TITLE_BLOCK_RESERVED_AREA_BYPASSED=False`.
7. Repeat A1 automatic All 10 (typically 1:25): A1 never gets a physical-sheet bypass. Run native `PLANTORTHOUPDATE` on the new A0 DWG to verify view/annotation/dimension update persistence.

## What REV69 intentionally does NOT do

- No modification of the existing title-block display, block definitions, metadata, or `test1`/`####`.
- No claim that the automatically plotted title block itself is invisible, moved or nonoverlapping.
- No unregistered template, model re-scaling, multi-sheet split, crop, hardcoded view geometry, or disabled dimension/viewport collision gate.
- No modification of the native PDF PlotEngine or fast update path.
- Windows CI provides geometry and build checks only; actual Plant 3D runtime and physical printed PDF must be tested separately.
