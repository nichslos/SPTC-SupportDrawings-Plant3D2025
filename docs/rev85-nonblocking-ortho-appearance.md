# REV85 — Nonblocking Ortho appearance after REV84 calibration failure

## Exact workstation evidence

User's `SPTC_TEST5_956_ORTHO_R229XREF_20261117_074751_9322_RESULT.txt`
from 2026-11-17 07:47:51 (REV84) showed:
- Selected two native StructureMembers, three native pipe sources.
- Native 1:10 and ten-view paper-fit preflight PASS.
- Registered empty native Ortho drawing and REV80 DwgVersion readback PASS.
- First TOP native Viewport constructed; before native GenerateOrthos
  produced output it entered the REV84 three-mode calibration.
- Calibration failed with
  `Plant 3D native hidden-line command did not produce three
  independently verifiable host values`.
- `NATIVE_DOCUMENT_CONTEXT_ERROR`; `NativeViews=0`,
  `DimensionsCreated=0`, `GENERATION_FAILED`, PDF blocked.
- No evidence of corrupted source model, false XREF dimensions,
  or a failure in REV81 dimension planning: it was never reached.

## Root cause

REV83/84 called native appearance setters/calibration
**inside every `GenerateOrthos`**. On this Plant 3D 2025
installation, the Ortho Editor's native command cannot be calibrated
reliably in that context, so its exception aborted the essential
ten-view generator and all downstream dimensions.

The previous REV84 final-session-sync code ran only if generation
completed, so the failure prevented it from running at all.

## REV85 fixed execution policy

1. **Never call appearance setters or numeric calibration inside
   native `GenerateOrthos`.** Use exactly the existing native
   GenerateOrthos invocation and mark each view
   `REV85_<VIEW>_APPEARANCE=DEFERRED_NO_NATIVE_HOST_MUTATION`.
2. Complete native ten views, verified source labels, native
   dimensions, final collision QA, and metadata registration
   through the already validated REV75–82 logic, independently
   of optional Output Appearance.
3. Only after successful generation, attempt native host-session
   appearance synchronization as an optional step. Hidden-line
   and insulation are **independent**; a failing/unsupported
   hidden-line command must not suppress the documented insulation
   mode (or vice versa).
4. Preserve the old session value when the requested native
   command or readback is invalid, via the existing scoped
   Begin/Dispose rollback. Keep explicitly verified host values
   in the current session on success. Plant 3D Default performs
   no host reads or writes.
5. Log one of `REV85_HIDDEN_LINES=HOST_READBACK_VERIFIED` or
   `REV85_HIDDEN_LINES=UNSUPPORTED_OR_UNVERIFIED` (likewise
   `REV85_INSULATION`); additionally log a report-wide
   `REV85_POST_GENERATION_APPEARANCE` status.
6. **Important: native host/session readback is not proof that
   appearance was persisted in each actual Ortho ViewDef.**
   `REV85_NATIVE_SAVED_VIEW_APPEARANCE=UNVERIFIED` remains
   accurate until a Plant 3D Edit View visual check confirms it.
   If native hidden mode is unsupported, instruct the user to
   select it manually through **Edit View → Output Appearance**.
7. Native appearance failures never alter existing successful
   DWG generation status, dimensions, PDF eligibility or the
   overall source verification. They are reported separately
   and visibly, not silently treated as PASS.

## Preserved

- REV75 10 native views, all camera orientations and insertion.
- REV80 Plant 3D registered drawing metadata; Edit View code.
- REV81 essential chain/overall dimensions and witness safety.
- REV82 native PlantSteelPlate source/dimension evidence.
- XREF and pipe scan logic, title block, exact scale and
  native PDF publication policy.
- REV79–84 UI commands, mode choices and project preferences.

No hard-coded hidden-line numeric enum mapping, plate sizes,
support coordinates, member tags, paper dimensions, project paths,
PnP identities or XREF inclusion exceptions.

## Tests

New standalone WinForms/pure helper tests:
- Unsupported hidden calibration still permits independently
  verified native insulation mode.
- Two unsupported modes return manual guidance without throwing.
- No `SavedViewAppearanceVerified=true` from a host readback.
- Supported explicit modes are kept at the end of successful
  generation only.
- Plant 3D Default never writes appearance state.
- REV84 keyword calibration/mapping tests remain in CI, as
  diagnostic validation of the native bridge, but no longer
  execute in the required generation path.

## User workstation acceptance

Install REV85 in a COPY of the Toshiba Plant 3D 2025 project,
then re-run `SPTC_SUPPORTORTHO10` on the same TEST5 handles with
All Hidden Lines + Hidden Line Insulation.

Required drawing checks:
- `STEP8_TOP_PRIVATE_GENERATEORTHOS_RETURNED`, all 10 native
  view count, 1:10 preserved, native DIMLINEAR and post-audit
  status.
- If the native hidden command still cannot be verified,
  `REV85_HIDDEN_LINES=UNSUPPORTED_OR_UNVERIFIED`, while
  `REV85_INSULATION` should have its own independent status.
- Inspect Ortho Editor native ribbon and rendered line/insulation
  style. If status is unsupported, choose the requested hidden-line
  mode manually through native **Edit View → Output Appearance**.
- Reopen from Project Manager and test Edit View and Update View;
  verify PDF separately (older eGraphicsNotGenerated error is
  independent).

CI passes compile and simulated behavior only. It cannot
prove that hidden-line/insulation styling is persisted inside
Plant 3D's saved native views.
