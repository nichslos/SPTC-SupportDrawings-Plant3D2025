# REV77 – persistent UI and review workflow, no native geometry changes

## Origin and scope

REV75 was runtime-validated by the user: 10 native views in one
Draft Overflow DWG at exact 1:10, with the native PDF deliberately
blocked. REV76 consolidated Output Mode and added a source-derived
QA panel. REV77 builds on REV76 and changes only the WinForms palette,
read-only diagnostics, plus smoke tests.

No changes to source model, native view cameras or placement,
OrthoCube, pipe/XREF proximity, DIMLINEAR, leader annotations, title
block stamps, saved project geometry, PDF publisher, native update
workflow, or the four public SPTC commands.

## UI work

- The existing Ortho fields are reparented to seven collapsible,
  vertically reflowing sections: Source & OrthoCube, Native Views &
  Scale, Drawing Output, Generation History & QA, Dimensions,
  Annotations & XREF, Project Settings. Collapse affects visibility
  only, not option values. The Ortho page remains scrollable.
- Run selected / Preflight / read-only status live in a distinct
  docked footer. The configuration workspace scrolls horizontally and
  vertically when needed at 100%, 125%, and 150% DPI. Footer controls
  remain outside the workspace scroll container.
- Output Mode remains the REV76 single-selector with original
  generation-flag mapping. Draft mode's warning remains visible in
  the Output section, and pre-run eligibility prevents incompatible
  view/count/scale/batch combinations.
- Track actual DWGs generated during the current project/palette
  session. The history persists across separate Run clicks,
  including source changes within the same project. Changing
  the project clears the list and disables output actions.
- Selecting a generated DWG opens the matching existing RESULT.txt
  read-only, updates the measured QA panel and assigns a conservative
  status: UNVERIFIED, REVIEW, FAIL (reported), or PASS (reported).
  A draft-only output or unverified XREF coverage cannot show as
  a fully approved PASS. Missing/stale reports remain unverified.
- Existing Open DWG Folder and Open RESULT.txt are selection-aware.
  Copy QA Summary copies just the selected filename, risk label,
  and measured report fields. No auto-send, clipboard access
  during generation or source/project writes.

## Testing

Windows smoke tests validate the scroll-independent sticky footer,
the seven groups, 4 command tabs, DPI scaling, historical settings
and production dialog controls, source-derived QA reporting,
and risk status for draft/XREF warning/failure/missing sources.
The production DLL must compile in CI. Testing in the actual
Plant 3D 2025 palette is still required, particularly narrow
dock width, section collapsing, project switch, output history and
PDF-blocked Draft Overflow.

## Explicitly deferred

Real graphical viewport-position preview requires actual
post-generation CAD source geometry and is not part of this UI-only
change. No approximate view diagram is invented.
