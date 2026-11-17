# REV82 — Native PLANTSTEELPLATE dimensions

## Scope

The user requested **dimensions for PLANTSTEELPLATE** models.
Autodesk's `PLANTSTEELPLATE` command creates *plate or grating*
entities and Plant 3D has a dedicated **StructurePlate** class,
distinct from **StructureMember**. Older SPTC source readers accepted
native StructureMember and pipe Support, so selected plates did not
enter the verified native DIMLINEAR source list.

REV82 is based on REV81. All prior native ten-view generation, REV80
Project Manager Ortho metadata, REV81 witness collision recovery,
XREF source exclusions, output scale, annotations and publish policy
remain unchanged.

## Native identity and real geometry

- Inspect explicitly selected **HOST DWG** object IDs read-only.
- Recognize only an actual `StructurePlate` native managed/RX class
  or a positive linked PnP `ClassName=StructurePlate`; a generic
  AutoCAD 3D solid, block or piping object whose description contains
  "plate" does not qualify.
- Inspect the actual native entity `GeometricExtents` in WCS.
  Require finite bounds and real source geometry within the
  calculated OrthoCube. Never guess plate standard dimensions.
- Add plate label from its actual linked Long Description (Size)
  when available; otherwise use a neutral native plate/grating label.
  No assumed plate TOS is added.
- Add plate geometry as `Kind=PLATE` with a verified `NATIVE:`
  source key. Existing XREF exclusion still applies; source data is
  neither exploded nor written back to model/PnP files.

## Dimensions

- In each orthographic viewport (TOP, BOTTOM, FRONT, BACK, LEFT,
  RIGHT), compute candidate **horizontal and vertical projected
  envelope** dimensions directly from the plate's native WCS
  extents transformed by that view's saved native camera.
- The measured values are dynamic and use actual project source
  units and native view scale; no fixed 210×210×9 or other sizes.
- Label provenance is always
  `NATIVE_STRUCTURE_PLATE_PROJECTED_ENVELOPE_NOT_CUT_LENGTH`.
  A rotated plate can have a larger WCS projected envelope than
  its true fabrication face length; the label must never imply a
  fabrication cut length or hole pitch.
- Candidate dimension lines and both witness lines must satisfy
  actual frame, page, text, annotation and source-geometry checks.
  Use the same coordinated profile layout and REV81 post-audit
  safety checks. Never move/omit a required overall, chain or
  terminal-remainder dimension to fit a plate.
- If an H or V plate dimension cannot be placed safely, record a
  `PLATE82_*_NO_SAFE_NATIVE_DIMENSION_LANE` diagnostic instead
  of silently fabricating the measurement or drawing intersecting
  witnesses. The optional plate-local measurement may be omitted
  when needed to preserve essential dimension safety.
- Prevent the legacy generic member profile selector from
  separately treating the same native plate as an ordinary
  StructureMember and drawing duplicates.

## Acceptance in a copied Plant 3D 2025 project

1. Model and SELECT a plate or grating made by `PLANTSTEELPLATE`
   together with the support steel. Run `SPTC_SUPPORTORTHO10`
   with Auto DIMLINEAR and Reference Detail Dimensions enabled.
2. Check RESULT.txt for
   `PLATE82_NATIVE_COUNT`, `PLATE82_NATIVE_DIMENSION_SOURCES`,
   and matching `PLATE_` dimension source names.
3. Verify H/V dimensions reflect actual native projected plate
   extents, and the plate thickness appears only in views where
   projected geometry permits a measurable thickness dimension.
   Distinguish geometric projection from fabrication cut sizes.
4. Inspect witness placement, labels, AutoCAD native DIMLINEAR
   properties, Edit View and Update View. No prohibited XREF dims.
5. If plate dimensions are omitted, inspect the exact diagnostic
   rather than assuming all plate members should fit at every scale.

Windows CI validates pure type identity, native-only source
filter, H/V projection planner and witness collision safety.
The true `PLANTSTEELPLATE` runtime object class, PnP row,
Ortho editor and DWG/viewport dimensions require workstation
testing; no assertion that CI proved actual Plant runtime behavior.

## No hard-coding

No plate size/shape/thickness, material standard, source coordinates,
OrthoCube box, paper scale, project path, PnP row ID, model handle,
view positions or PDF media are hard-coded.
Only native type/schema names and existing paper-space drafting
rules are used.
