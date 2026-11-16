# REV65 — Intelligent one-sheet view placement, exact media selection, seed diagnostics

## User-facing requirement

Keep **one native Ortho DWG** and **one PDF** containing the complete
TOP/BOTTOM/LEFT/RIGHT/FRONT/BACK/NW/NE/SW/SE view set at the exact
selected 1:N paper scale. Do not split into REV63 S01/S02 drawings,
reduce the selected scale, crop viewports, guess title-block sizes, or
alter source model geometry.

## Improvements over REV64

### 1. Bounded intelligent native paper layout

The original REV61 layout still has priority. If it cannot fit, the
planner deterministically tests **height-descending, width-descending,
area-descending, height-ascending and width-ascending** preset groups
across the existing 2/1/3/4 column combinations. Every candidate must
pass the same full-scale model projection envelope, title width,
caption height, right title-block 25% reservation, paper borders,
native viewport nonintersection, and readable annotation gutter
reserves. No new drawing or Project Manager registration occurs during
the layout search.

The output frames are restored to the original ten native view
preset order even if their positions change; native camera preset
directions and source OrthoCube remain unchanged. Where the original
layout fits, it remains first and unchanged.

### 2. Specific reasons for unsuitable registered native seeds

When automatically choosing a sheet for **All 10 at manual 1:N**, the
command now reports actual paper media and results, for example:

```text
SPTC REV65_REGISTERED_EMPTY_SEEDS_CHECKED=2 EXACT_FITS=0 REJECTED=2
SPTC REV65_SEED_CANDIDATE=SPTC_NATIVE_SEED_01.dwg MEDIA=841x594mm RESULT=NO_FIT REASON=... REV65 closest layout: 2x5 ...
SPTC REV65_SEED_CANDIDATE=SPTC_NATIVE_SEED_A0.dwg MEDIA=UNREADABLE RESULT=MEDIA_UNREADABLE REASON=...
```

These are examples of message **format**, not observations from the
user's current two seeds. The diagnostic does not invent their actual
dimensions or what errors occurred. Report at most the first 20
candidates and a count of additional entries. Unregistered files or
drawings containing existing native views are never considered eligible.

If a seed passes, the command still chooses the smallest physical
registered empty native Ortho sheet that fits **all ten at the exact
requested scale**, not the smallest sheet on disk. Existing A1
**Automatic 1:25** uses the REV59 default placement/seed path unchanged.

If no eligible paper exists, the command exits before native Ortho DWG
cloning, registration or PDF publication; it does not create any
multiple-sheet drawing. A0 or another correctly plotted larger
registered EMPTY native seed must be created in Plant 3D Project
Manager when actual projections cannot fit A1.

### 3. QA scope and limitations

Pure tests cover:
- The historic TEST5-like A1 All10 1:10 physical fit failure with
  quantified closest layout shortages.
- Safe All10 1:10 A0 with the right-side title-block and captions.
- A targeted alternating-tall/short geometry that only fits a
  height-aware layout, while preserving all ten names in original order.
- Correct media/status/reasons for each registered empty seed,
  including corrupt/unreadable media and safe diagnostic formatting.
- The REV59 AUTO All10 1:25 baseline remains unchanged.

Windows CI compilation and pure tests cannot guarantee actual native
Plant 3D 2025 Update View, plotted A0 title-block geometry, PDF
appearance or annotation legibility on site. All native project
registration and drawing publishing safeguards remain in place.

## Workstation acceptance

1. Back up the current tested DLL and use a copied Plant 3D project.
2. NETLOAD REV65 and run `SPTC_SUPPORTORTHO10` at 1:10 + All 10,
   selecting the same support members 8A3 and 8A4.
3. Read all `REV65_SEED_CANDIDATE` lines in the AutoCAD command
   window. Record the actual oriented plot media for both existing
   registered empty native Ortho seeds and each reported failure reason.
4. If neither fits, create a correctly plotted/registered empty A0
   landscape native Ortho sheet in the project; rerun.
5. Confirm one Ortho DWG, ten actual native preset views with scale
   0.1, one one-page PDF, valid view titles/annotations/dimensions,
   and complete QA. Verify no paper/title-block overlap.
6. Run native `PLANTORTHOUPDATE` and inspect the saved updated DWG
   and regenerated PDF. Compare registered views and dimension counts.
7. Regression test Automatic + All 10 on the existing A1 seed:
   prior REV59 placement and native PDF publisher must remain stable.

No current changes to title-block ATTDEF/ATTRIB authoring, the
existing minimum printed label height of 1.593 mm on TEST5, or the
two optional LEFT/RIGHT member-width dimensions. Those are
subsequent production-readiness revisions, not hidden parts of REV65.
