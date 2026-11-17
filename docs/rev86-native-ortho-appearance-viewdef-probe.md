# REV86 — Native Ortho ViewDef appearance discovery (diagnostic only)

## User-reported failure

TEST5 generated ten native orthographic views and a PDF on 2026-11-17.
The Support Drawing Manager requested **All Hidden Lines**, but native
Plant 3D 2025 Edit View reported **No Hidden Lines**.

The captured REV85 result log shows
`REV85_HIDDEN_LINES=UNSUPPORTED_OR_UNVERIFIED`,
`REV85_INSULATION=DEFAULT_UNCHANGED`, and
`REV85_NATIVE_VIEW_APPEARANCE_VERIFIED=False`.
REV85 correctly did not report successful appearance synchronization.

## Why not write an assumed numeric value?

REV84 tried the documented PLANTORTHOHIDDENLINEMODE command's three
keywords and read PLANTORTHOHIDDENLINEMODE after each. On this workstation
all three readbacks were indistinguishable. A fixed numeric 0/1/2 guess,
string-to-integer coercion, or direct Ortho database mutation would be
unsafe and might silently select No Hidden Lines.

REV86 therefore adds a non-mutating, read-only probe of the **actual
native PlantOrthoView and saved IDwgViewDef metadata** after TOP is generated.
It logs only reflection metadata (member names, types, read/write flags)
for appearance-related properties/fields/methods. For saved ViewDef it
also records property names so a correct, version-appropriate host-backed
access path can be identified.

## Expected new RESULT.txt records

- `REV86_APPEARANCE_REQUEST_HIDDEN=All Hidden Lines INSULATION=...`
- `REV86_APPEARANCE_SCHEMA_PLANT_ORTHO_VIEW=...`
- `REV86_VIEWDEF_PROBE=READ_ONLY_COMPLETED ...` or an explicit unavailability reason
- `REV86_APPEARANCE_SCHEMA_SAVED_VIEWDEF=...`
- `REV86_APPEARANCE_MEMBER_SAVED_VIEWDEF=...` if a candidate exists
- `REV86_SAVED_VIEWDEF_PROPERTY_NAMES=...`

The existing REV85 unsupported/verified statuses are retained. The
probe **does not fix or claim to fix** generated hidden-line appearance.
It cannot prove native persisted appearance until the required Plant 3D
API/storage path is discovered and a subsequent runtime patch is tested.

## Protected scope

- Four production commands only.
- All ten native views, ViewDef types, viewport camera and exact scale.
- REV81 dimension collision protection; REV82 PlantSteelPlate evidence.
- XREF handling, source metadata, native Edit View compatibility.
- Production sidecars, PDFs, title blocks, preflight.
- No fixed hidden-line enum values, no project-specific identifiers,
  no manual/synthetic dashed line geometry, and no native ViewDef writes.

## Acceptance

After Windows CI, run the existing `SPTC_SUPPORTORTHO10` on a **copy** of
the project with All Hidden Lines. Supply the full `_RESULT.txt`.
Use the REV86 diagnostic to determine whether the current native Plant
assemblies expose writable appearance metadata. Treat the original
mismatch as **unresolved** until Edit View shows All Hidden Lines for
each saved view after save/reopen and Update View.
