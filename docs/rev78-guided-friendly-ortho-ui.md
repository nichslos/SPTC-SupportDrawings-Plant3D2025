# REV78 — User-friendly guided Support Drawing Manager

## Baseline and scope

REV78 starts from the REV77 code, which includes the REV76 single
output-mode chooser and the runtime-validated REV75 native single-DWG
Draft Overflow engine. The revision changes **only the modeless
WinForms palette** and its smoke tests; all Plant 3D native geometry,
OrthoCube, cameras, XREF scan logic, dimensions, annotation, paper
scales, source/project registration, output file writes and PDF rules
are unchanged.

## Workflow refinements

- **Show essentials**: displays Source/Scale/Seed, Views, and Drawing
  Output. If there is actual generation history, that section remains
  visible too. Other settings are simply collapsed, not reset.
- **Show all settings**: expands all seven existing sections; users can
  still expand or collapse individual sections by clicking a header.
- Three core sections are numbered **1. Source, Scale & Seed**,
  **2. Choose Native Views**, and **3. Drawing Output**.
- **Draft: All 10 Views** shortcut explicitly selects all ten native
  cameras, turns Batch OFF and selects the existing REV75 Draft Overflow
  mode. It **does not change the user's paper scale**. If the selected
  scale is Automatic, Run remains unavailable until the user chooses
  an explicit 1:N; the UI explains this directly.
- **Printable Sheet** shortcut returns to the original physical-paper
  mode without changing view selections, scale, source or annotations.
- The Output section shows a live action-ready statement (view count,
  exact scale, output mode), or a specific correction when ineligible.
  Draft mode visibly says **DRAFT ONLY / native PDF blocked**.
- The read-only Preflight summary uses an easy-to-scan numbered
  description of views, scale, output, seed, annotations, dimensions,
  and the chosen mode's actual constraints. Native fit verification
  still only happens in the generator.
- Tooltips explain SupportFocus, registered EMPTY Ortho seeds,
  1:N scale, xref label sources, batch mode, production output
  action buttons and the two quick mode shortcuts.
- The Generation History & QA section starts collapsed when
  there are no drawings; after the native generator reports a real
  saved DWG it expands automatically to show measured RESULT.txt QA.
- Existing project-switch history isolation and saved-preference
  rules remain unchanged. The new quick choice is **not persisted**.

## Regression / acceptance

Windows Forms smoke tests cover all ten view presets, chosen scale
preservation (including an explicit 1:20 test), existing native options
mapping, single-sheet reset, automatic-scale rejection, actionable
status/warnings, basic/all section toggling and numbered Preflight.
REV77 tests for 100/125/150% DPI, sticky Run/Preflight, history,
collapse/expand, report QA and project-switch isolation still run.

Test the new DLL in a copied Plant 3D 2025 project, select
SPTC_SUPPORTORTHO10, use the quick Draft setup, select the desired
exact scale, Preflight, Run, verify native DWG and report. PDF remains
blocked for off-sheet draft layouts. Do not merge until workstation
validation.

No hard-coded model coordinate, drawing path, support size, pipe
clearance, paper dimensions or native view placement.
