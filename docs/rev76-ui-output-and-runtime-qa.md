# REV76 — Production UI output-mode and runtime QA refresh

## Scope
UI-focused revision based strictly on the validated REV75 draft-overflow
backend, preserving all native Ortho generation, 10-view directions,
geometry, dimensions, annotations, XREF discovery, project/seed
validation, exact scale, production PDF behavior and all four public
AutoCAD commands. No model-specific geometry or paths.

## Changes
- Replace separate REV74 "split sheets" and REV75 "draft overflow"
  checkboxes with ONE dropdown: **Single sheet — fit to paper**
  (default), **One DWG — draft overflow**, **Multiple sheets — exact
  scale**. The new pure helper maps choices exactly to the existing
  `BypassOneSheetLimit` and `AllowDraftOverflow` generator flags.
- Enforce before Run: draft and multi-sheet are eligible only with
  all ten native view presets, an explicit 1:N scale, and Batch OFF.
  Unsupported combinations DISABLE Run and show a visible message.
  No automatic scale change, data loss or silent mode conversion.
- Reorganize the scrollable Ortho settings: clearly labeled output
  section, context explanation, live selected-view count, compact
  project settings, and a separate last-generated QA section.
- On actual native DWG completion, existing optional
  `GenerateOne.onOutputDwg` callback reports the exact saved path
  to the modeless palette. The palette reads the matching existing
  `_RESULT.txt` (if present), displaying actual native view count,
  dimensions, overall dimensions, optional omissions, actual viewport
  overlap flag, and final status. If the source report is missing,
  unknown values are never fabricated.
- Active actions: Open DWG folder; Open RESULT.txt. Both are disabled
  until an actual generated output is known, and safely open
  through Windows Shell rather than launching CAD commands.
- Output list and file actions reset on project change / new run.
  Output mode remains opt-in and **never saved** with local
  project preferences (same REV74/75 privacy policy).
- The existing modeless progress form, persistent palette, all
  save/load/default actions, branding, source/project context and
  read-only Preflight remain unchanged.

## Deliberate non-changes
- Source/Ortho/PnP DWG geometry, camera type, viewport placement.
- OrthoCube XREF pipe detection, proximity/metadata/diagnostics.
- Native dimensions, dimensions PDF QA, annotation writers, title
  block and current native PDF publishing rules.
- External Support Export/Batch/2D Batch commands or forms.
- Physical scale/paper fit, REV74 split mechanics, REV75 draft
  overflow logic, project settings storage schema.
- No graphical geometry preview in this revision; configuration
  feedback is live and measured QA is available *after* generation.
  A true visual geometry preview requires separate source-projection
  validation and will not be fabricated from view names.

## Testing
Run Windows GitHub Actions plus pure smoke tests for output-mode
eligibility/defaults, exclusive mapping, actual reported QA values
and missing-source handling. In an AutoCAD Plant 3D 2025 copied
project, reopen with NETLOAD and test three UI modes. For draft,
select all ten at 1:10 and verify one native DWG with no PDF.
For split, deliberately test a nonfitting 1:10 sheet. Ensure
normal mode stays fail-closed on no-fit. After each completed run,
inspect the new *Last generated DWG / QA* panel, open actions, and
the exact source RESULT.txt. Also test project switch clears
prior project output and does not persist an override choice.
