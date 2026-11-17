# REV80 — Native Edit View registration metadata handshake

## User-reported problem

After REV79 generated ten native Plant 3D Ortho viewports, selecting
Edit View displayed:

> Command is disabled in this drawing created in earlier version of Plant 3D

The earlier source report does NOT establish that the DWG truly used an
older binary format. `Database.SaveAs(dest,DwgVersion.Current)` had
already saved the clone using the running AutoCAD DWG version.

The report confirmed `STEP2_PROJECT_REGISTERED_EMPTY_ORTHO=PASS`,
ten native viewports, 1:20 and source drawing registration. This only
demonstrated that `AddPnPDrawingFile` accepted the new file: it did not
check the project Ortho metadata field `PnPDrawings.DwgVersion`.

A 2026 Autodesk .NET Community investigation of the EXACT same error
found that API-created Ortho rows could have a NULL DwgVersion even
though the native Project Manager wizard created rows had a value.
The report did NOT independently establish that the user project
has NULL DwgVersion — REV80 tests that, rather than assuming it.

## Native-only, NO hard-coded version repair

1. **Before cloning or registering any new drawing**, ask the current
   Ortho part's native Plant DataLinksManager for the PnP database.
   Locate only `PnPDrawings`, read its rows, and find an existing,
   physically present and native-validated registered Ortho DWG
   IN THE SAME active project.
2. Prefer the chosen registered native Ortho seed's DwgVersion.
   If its value is absent, use only a *single unambiguous* version
   among the project's other verified native Ortho drawings. The
   version value is never guessed from the Plant year and never
   hardcoded as an integer/string in source.
3. Abort BEFORE creating a DWG if there is no trustworthy version
   reference or if schema/DLM access is unavailable. This might
   require creating one blank Ortho through Project Manager in a
   **copied project** before retrying.
4. Keep the existing `Database.SaveAs(DwgVersion.Current)` and
   native `AddPnPDrawingFile`. After native registration and the
   existing `OrthoDocUtils` / empty native view catalogue checks,
   identify the *single* new PnP row using the returned GUID or
   its exact full file path. Reject ambiguous IDs, mismatching GUIDs
   and existing conflicting DwgVersion properties.
5. If the target's DwgVersion is NULL, use ONLY the **host Plant
   DataLinksManager** `SetProperties(rowId, [DwgVersion], [reference])`,
   followed by `SavePnPDatabaseChanges()`. Verify the exact
   target PnP row and value again through the native DLM API.
   There is NO direct SQLite, SQL Server, Ortho.dcf, XRecord or
   fabricated version write. No other row/column is modified.
6. Abort BEFORE generating ten views if metadata update/readback
   cannot be proven. If the native registration was already completed,
   do not silently delete the registered DWG; the user may remove
   that empty drawing through Project Manager in a test project.
7. Log `REV80_NATIVE_ORTHO_VERSION_REFERENCE`,
   `REV80_DWG_VERSION_POLICY`,
   `REV80_EDIT_VIEW_VERSION_READBACK` and status.
   **Readback is NOT an automated proof that Edit View UI works**.
   Manual Plant 3D 2025 Edit View and Update View verification is
   still required.

## Scope preserved

No changes to the validated REV75 10 native view directions, scale,
OrthoCube, native view generation and placement, DIMLINEAR, XREF
source discovery, annotation, REV74–79 form options, physical media,
PDF rules or all four original SPTC commands.

No project paths, model handles, PnP row IDs, pipe tags, viewport
locations or Plant version-number values are hardcoded. Schema
identifiers `PnPDrawings`, `PnPID` and `DwgVersion` are native
Autodesk field names, not model-specific constants.

## Important independent existing issue

The user's 2026-11-17 report also shows
`REV22_41_DIMENSION_RESULT=FAILED`, with 2 unintended witness
intersections. That is a separate native DIMLINEAR safety failure.
REV80 deliberately does not bypass this dimension guard.

## Validation

Windows CI: all previous structural/view/geometry/UI tests plus
REV80 fake-native-DLM tests for source version inheritance, GUID
matching, one-row-only SetProperties, save/readback, no rewrite
on already-matching version, and fail-closed behavior on absent or
ambiguous reference.

Plant 3D workstation: copy the entire project first. Confirm
that a Project Manager-created native Ortho seed is available;
run SPTC_SUPPORTORTHO10 and look for
`REV80_EDIT_VIEW_VERSION_READBACK=PASS`. Save/close/reopen the
new DWG from Project Manager, select a viewport and test both
Edit View and PLANTORTHOUPDATE. Report any runtime errors.
