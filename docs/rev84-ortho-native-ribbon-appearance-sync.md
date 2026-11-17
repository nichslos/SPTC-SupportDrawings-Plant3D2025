# REV84 — SPTC vs native Plant 3D Ortho ribbon mismatch

## Reproduction (user screenshots, 2026-11-17)

The SPTC Manager showed **All Hidden Lines** and **Hidden Line
Insulation**, but Plant 3D 2025 native Ortho Editor's Output
Appearance ribbon showed **No Hidden Lines** and **No Insulation**.

A preceding user run selected **All Hidden Lines** with **No
Insulation**. Each GenerateOrthos call logged:

`REV83_<view>_HIDDEN_LINE_REQUEST=All Hidden Lines NATIVE_KEYWORD=Allhiddenlines HOST_READBACK=0 VIEW_OUTPUT_VERIFICATION=PENDING`

`REV83_<view>_HIDDEN_MODE_RESTORE=PASS`

`REV83_<view>_INSULATION_MODE_RESTORE=PASS`

This exposed two separate defects:
1. REV83 first tried `SetSystemVariable("PLANTORTHOHIDDENLINEMODE",
   "Allhiddenlines")` on a numeric-backed native host variable.
   The call could succeed while returning `0`. It never proved
   that the native command accepted the user's selection.
2. REV83 deliberately restored BOTH appearance values after each
   generated view, so the native ribbon subsequently returned to
   its former No/No choices.

## REV84 behavior

- Use Autodesk's documented native **PLANTORTHOHIDDENLINEMODE**
  command with its own documented keyword; do NOT set a string
  directly on a numeric-backed system variable.
- On first explicit hidden-line override, probe each documented
  command mode in the *actual running Plant 3D installation*
  and read the corresponding host integer. Accept the mapping
  ONLY if the three native command readbacks are distinguishable.
  Restore the host's exact original number after probing.
  Never assume fixed hidden-line integer codes or use model values.
- Apply the requested keyword through the native command and
  compare its actual system-variable readback against the
  runtime-discovered mapping. If the native command cannot run
  in the required context, or readback is contradictory, refuse
  the override with a specific diagnostic rather than report success.
- For **each view's native GenerateOrthos call** keep REV83's
  exception-safe temporary state/rollback.
- **After the entire Ortho generation succeeds**, apply explicitly
  selected native appearance to the active target drawing and
  retain the setting in the current Plant 3D session; do not
  immediately restore the ribbon to the old mode. No change is
  made if both selectors say Plant 3D Default. If final
  synchronization fails, log
  `REV84_FINAL_NATIVE_RIBBON_SYNC=FAILED` and warn the operator
  without damaging the successfully generated DWG.
- The user chose specific non-default appearance modes, so this
  is an explicit host session state request. The UI labels and
  Preflight note disclose that Plant 3D may propagate appearance
  to other views referencing the same model during refresh.
  Native Ortho Edit View must still be visually checked.
- Native ribbon verification is not a claim of pixel-perfect
  view-output equivalence or per-drawing state isolation.

## Protected native geometry and legacy workflows

Preserve REV75–83 10 native camera directions, exact common scale,
OrthoCube, paper placement, native drawing metadata and Edit
View support, REV81 essential dimension collision protection,
REV82 PlantSteelPlate, structural/XREF source filtering,
layout annotations and existing PDF publishing rules.

No direct DCF/PnP SQL edits. No hard-coded drawing IDs,
geometry sizes, coordinates, hidden-line enum numbers or
project-specific paths.

## CI and runtime

Windows CI tests:
- Native hidden-line mode mapping discovered from three arbitrary
  mock host numeric readbacks (not 0/1/2).
- Detect the old all-modes-read-as-zero failure and reject it.
- Reject a mismatching native requested-mode readback.
- Explicit successful selection remains in native session.
- Failed/abandoned generation restores both original values.
- Plant Default leaves host settings unchanged.
- All prior WinForms, geometry, dimension, XREF and DLL tests.

Workstation test: use a **copy** of Plant 3D 2025 project. Generate
new Ortho with All Hidden Lines + Hidden Line Insulation. In
RESULT.txt, review `REV83_...HOST_READBACK` and
`REV84_FINAL_NATIVE_RIBBON_SYNC`. In the active Ortho Editor, visually
compare its native ribbon to the SPTC dropdowns; save/close/reopen
and test Edit View and Update View. If the native command is
unavailable in the generator context, switch both selectors to
Plant 3D Default and manage the appearance manually in the
native Ortho Editor, rather than forcing unsupported API values.

Autodesk documents that refreshed Ortho views can propagate
appearance across drawings sharing one 3D model, so do not
assume per-drawing isolation.
