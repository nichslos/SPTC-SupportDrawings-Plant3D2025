# REV83 — Native Ortho hidden lines and insulation output appearance

## User-requested modes

Two controls are added under **3. Drawing Output**, with the exact
Autodesk Ortho Editor options:

| Hidden Lines | Insulation |
|---|---|
| No Hidden Lines | No Insulation |
| Hidden Line Piping | Solid Line Insulation |
| All Hidden Lines | Hidden Line Insulation |

Both selectors also have **Plant 3D Default** as the original,
non-mutating choice. This additional choice preserves the REV82
production baseline and native project Ortho appearance defaults
unless the user explicitly chooses an override.

## Source of native modes

Autodesk Plant 3D documentation:
- PLANTORTHOHIDDENLINEMODE prompts
  `Nohiddenlines/hiddenPipingonly/Allhiddenlines`
- PLANTORTHOINSULATIONMODE has documented Integer values:
  `0=hide insulation`, `1=hide pipe behind insulation`,
  `2=show pipe behind insulation`.
- Ortho Editor contextual tab exposes both as **Output Appearance**
  controls.

The code maps UI choices only to these documented native keywords/
values. There are no project, model, support-size, viewport or
appearance-geometry values hard-coded.

## Native generation and safety

The queued Ortho UI options and project-specific preference values
include both selections, with backward-compatible defaults.

For **each native GenerateOrthos call**, the plugin:
1. Validates both selected modes in the UI and before generating.
2. Reads the currently installed Plant 3D native host states
   before making any change. If read is unsupported, fails closed.
3. Temporarily applies selected hidden line mode through the
   native PLANTORTHOHIDDENLINEMODE variable or documented command
   keyword; sets documented PLANTORTHOINSULATIONMODE when selected.
4. Validates native insulation readback (and available hidden-line
   keyword readback); logs source, selection and verification state.
5. Invokes the **same** native Plant GenerateOrthos API used by
   REV82 without changing its camera, scale, cube, viewport geometry,
   dimension, XREF, title, annotations or PDF path.
6. Restores both prior host values in reverse order, even if native
   generation fails. Cannot silently claim a successful restore if
   the native API rejects it.

This makes no manually generated dashed lines or insulation
wireframes, does not alter source models or Ortho.dcf, and does not
guess undocumented numeric hidden-line enum values.

## Important workstation limitation

Windows CI verifies UI/mode mappings, safe application and rollback
through a fake host adapter, and unchanged native generation code.
It **cannot** prove that a particular Plant 3D 2025 installation
exposes all appearance commands in the same programmatic context
or that mode overrides persist after Edit View/Update View.
If the installed host refuses them, the new REV83 generator
reports the incompatibility rather than claiming an output PASS.

## Smoke tests and acceptance

- Three native options each, plus safe non-mutating default
- Native keywords and 0/1/2 insulation mappings
- Settings passed intact from modeless palette to queued Ortho
- Read-only Preflight shows both choices
- Per-project save/load and older JSON compatibility
- Restoring original hidden-line and registry insulation settings
- Partial failure rolls back the first successful setting
- All existing REV75–82 native views, dimensions, PlantSteelPlate,
  XREF exclusions, UI and production regression suite

Workstation: in a COPY of the Plant 3D project, run
SPTC_SUPPORTORTHO10 with Auto DIMLINEAR and desired mode, then
inspect RESULT.txt for REV83 mode readback/restore records, the
native DWG appearance, Edit View and PLANTORTHOUPDATE. Test each
of the six explicit modes and Default separately.

Do not merge this PR until visible DWG verification succeeds.
