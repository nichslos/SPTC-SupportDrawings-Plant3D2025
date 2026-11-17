# REV79 — Responsive Ortho palette and live operator guidance

## Source and unchanged engine

Built from REV78's exact tested GitHub head, inheriting validated
REV75 native ten-view Draft Overflow and REV76–78 WinForms behavior.
This is an **interface-only** revision. It does not change native CAD
model geometry, OrthoCube, camera orientation, view placement,
DIMLINEAR, annotations, line/BOP/TOS data, PDF publication,
project registration, source metadata, output naming, or any of the
four production SPTC commands.

## Changes

1. **Responsive docking.** Narrow WinForms PaletteSet widths now
   use a real single-column layout of the SAME existing source,
   native view, drawing output, QA, dimension, annotation and project
   preference controls. Wide docking restores the original two-column
   control positions. The section list reflows after resizing; it
   does not silently discard or reset any setting. Scrollbars are
   reserved without hardcoded CAD/paper geometry.
2. **Persistent status line.** The sticky Run/Preflight footer always
   displays selected view count, configured scale, chosen output mode
   and whether the current mode prohibits native PDF publishing.
   It remains independent of scroll position.
3. **Useful collapsed section headers.** Section headers continuously
   summarize measured UI choices: 10/10 views, 1:10, SupportFocus,
   One DWG Draft/No PDF, H/V dimensions, annotation/XREF choices
   and number of real generated DWGs. Full text is in tooltips.
4. **Fix Setting.** When incompatible settings are selected,
   the existing Run action is disabled, an explicit reason is shown,
   and Fix Setting expands/scrolls to the relevant actual field.
   Example: Draft mode + Automatic scale navigates to Paper Scale,
   without replacing it with a hard-coded denominator.
   Manual seed and filename prefix validation also occur before Run.
5. **Generation history navigation.** Actual saved DWGs display their
   local file modified timestamp, filename and source-report-derived
   Draft/No PDF versus Native Sheet state. A new Open DWG action and
   history double-click open the verified file through the user's
   Windows shell association; they do not inject AutoCAD commands.
   Previous Open DWG Folder, Open RESULT.txt and Copy QA remain.

## Safety

No project-specific source paths, support dimensions, pipe positions,
XREF clearances, physical paper media dimensions or native viewport
coordinates are embedded. No scale or output mode is silently changed.
Actual plot/paper fit remains a generator decision, not an invented
UI preview. Data read from RESULT.txt is never assumed available.

## Test checklist

Windows CI: existing command/geometry/DIMLINEAR/WinForms suites,
plus REV79 responsive docking and 100/125/150% DPI, live summary,
collapsed section details, invalid-scale fix navigation and
DWG-history interaction checks. Run inside a copy of the user's
Plant 3D 2025 project before promoting to the validated baseline:
SPTC_SUPPORTORTHO10, all ten views, explicit 1:10, Draft; then
dock left narrow, undock wide, generate, inspect QA and open DWG.
