# REV87 — Native Ortho Editor mode capture (safe investigation, NOT a fix)

## Why REV87 is necessary

User TEST5 output on 2026-11-17 08:25 showed
`REV86_APPEARANCE_REQUEST_HIDDEN=All Hidden Lines`, but native
Ortho Editor showed **No Hidden Lines**. Post-generation REV85
calibration still failed because all three native command/readback
values were indistinguishable in that context.

The REV86 probe found a saved native `DwgViewDef` with a property
named `HlrTrans`, but **no verified link** between that property and
Plant 3D's three hidden-line modes. Do NOT assign any numeric value
to it or directly patch the Ortho database.

Autodesk documents `PLANTORTHOHIDDENLINEMODE` as a command in the
**Ortho Editor contextual ribbon**. Calling it outside that context
did not produce verifiable results in the user's REV84/85 builds.

## REV87 additions

1. Under the existing REV86 TOP-view probe, report **metadata only**
   for `HlrTrans` and any other appearance members of the actual
   running native `DwgViewDef` and `PlantOrthoView`.
2. Search loaded Autodesk Ortho UI / Drawings2d **type metadata** for
   likely hidden-line/insulation mode controller names. Strict cap
   100 members; no unknown native method, getter, command or
   property setter is invoked.
3. In the existing Support Drawing Manager, click
   **Capture Native Editor Mode (REV87)** and select the label that
   matches the mode the operator has just chosen in the native
   Ortho Editor ribbon. This read-only WinForms event is handled by
   the existing palette, **not by a new AutoCAD command**. The
   sidecar is appended as
   `<ActiveOrthoDrawingName>_REV87_APPEARANCE_CAPTURE.txt` next
   to the active copied test drawing.

No existing production command is removed or renamed. No fixed hidden
enum values, geometry edits, source model changes, PnP/DCF writes, fake
dashed line generation, or attempt to automatically set appearance
before actual runtime verification.

## Workstation experiment (use COPIED project only)

1. NETLOAD REV87 in Plant 3D 2025.
2. Generate `SPTC_SUPPORTORTHO10` with All Hidden Lines, as before;
   send the generated `_RESULT.txt` for `REV87_` metadata.
3. In that generated native Ortho DWG, select **Edit View** on TOP
   and enter native Ortho Editor.
4. In **Output Appearance**, select **No Hidden Lines**.
   In the persistent Support Drawing Manager, click **Capture Native Editor Mode (REV87)**, then choose **No Hidden Lines**.
5. While still in the native Ortho Editor, switch to
   **Hidden Line Piping**. Click the same Manager button and choose **Hidden Line Piping**.
6. Switch to **All Hidden Lines**. Click the Manager button again and choose **All Hidden Lines**.
7. Exit via the native **OK**, save drawing and reopen TOP Edit View.
   Confirm which mode persisted. Send the capture TXT.

If the Manager is inaccessible during the native Ortho Editor session,
**do not bypass native controls**. Report this limitation and send
screenshots of each selected mode. The four production commands
remain exactly unchanged.

## Acceptance / known limitation

REV87 validates whether the real native Ortho Editor provides three
distinguishable mode readbacks and whether a candidate settings API is
discoverable. This is **not a confirmed appearance fix**. The user
request remains open until generated native Ortho views actually
retain their selected appearance after Save/Reopen/Edit/Update.

The existing REV85 nonblocking fallback, 10 native views, DIMLINEAR,
REV82 PlantSteelPlate, XREF logic, native metadata and PDF remain
unchanged.
