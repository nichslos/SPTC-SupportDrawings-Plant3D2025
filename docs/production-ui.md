# SPTC Support Drawing Manager — production UI

This release enhances the **existing modeless Plant 3D 2025 palette**, not the native geometry engine.

## Views and output

- **All 10** selects TOP, BOTTOM, LEFT, RIGHT, FRONT, BACK, NW, NE, SW and SE.
- **Ortho 6** selects the six orthographic views.
- **ISO 4** selects NW, NE, SW and SE isometric views.
- **Clear** unchecks the list; select at least one native view before running.
- Individual view checkboxes remain editable after choosing a preset.
- The existing automatic, preset 1:N and custom 1:N scale handling, OrthoCube SupportFocus, annotation options, native DIMLINEAR settings and batch Ortho checkboxes are unchanged.

## Saved settings

**Save settings** explicitly stores this project's checked native views, scale, optional prefix, focus mode, annotation switches, native dimension toggles and batch setting in the current Windows user's local AppData. **Load saved** reapplies those settings; **Defaults** restores the original working 10-view configuration without writing a preferences file.

The palette restores previously saved settings when reopened for the same Plant project. Switching projects selects only that project's settings (or the working defaults). The stored filename is derived from the normalized project-root SHA-256; a source drawing, native entity handle, queued command or manual seed file path is never stored. In particular, an operator must reselect any manual Ortho seed each session. The settings do not change a drawing until the operator explicitly runs a production command.

Invalid or corrupt settings files are ignored and never prevent AutoCAD or native SPTC generation.

## Progress and reports

The existing Ortho progress window continues displaying native generation events and TXT paths. For a single drawing or a batch of support BLOCK references, it now reports **processed / total / passed / failed** jobs. A failed batch support still counts as processed, but never as passed. Partial or failed runs are not presented as 100% successful.

The preflight remains read-only: it does not open a model, mutate the project, or generate geometry. For the four export/Ortho commands, use the established SPTC menu and palette; the underlying selection dialogs and output destinations are unchanged.

## Production checks

After loading the compiled DLL in AutoCAD Plant 3D 2025:
1. Open the persistent palette using `SPTC_SUPPORTORTHO10` and try All 10 / Ortho 6 / ISO 4 / individual selection.
2. Verify saving project settings, closing and reopening the palette, and switching projects. No manual seed from another project should be reused.
3. Run Preflight; confirm the selected view count, scale, annotation flags, batch and optional prefix.
4. Run the existing TEST5 10-view case. Compare the baseline **25/27 dimensions**, **12/12 overall dimensions**, **10/10 TOS**, zero witness intersections and QA PASS. Native `PLANTORTHOUPDATE` should still take the unchanged fast path when source geometry/viewports have not changed.
5. For batch testing, use a **copied Plant 3D project** and selected support BLOCK references. Confirm process counts, success/failure details and the batch TXT summary. Native AutoCAD Ortho calls cannot safely be cancelled in the middle of generation.

This UI-only improvement does not resolve the two previously deferred LEFT/RIGHT local dimension omissions. They remain subject to the existing native collision checks.


## Docking and Windows display scaling

The manager retains its full existing 805-by-765 reference layout inside a DPI-aware scrollable canvas. A narrow docked AutoCAD palette (or display scaling of 125%/150%) can therefore scroll horizontally and vertically rather than hiding preflight, view selection, or Run buttons. The native view selection and generation settings are unchanged. The Ortho settings tab also scrolls vertically to reveal project preference buttons.

The batch progress dialog scales with Windows DPI and keeps its progress bar and processed/passed/failed counters anchored when resized.

Windows Forms regression tests simulate 100%, 125%, and 150% control scaling and narrow host widths, checking that all four operation tabs, Run/Preflight buttons, saved settings panel, and progress accounting remain available. **These tests are not screenshots of the actual Plant 3D application**. Before deployment, check the palette with your monitor scaling and docking configuration and confirm no label is cut off. If necessary, use the scrollbars rather than changing native generation settings.
