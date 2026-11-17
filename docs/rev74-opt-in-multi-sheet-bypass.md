# REV74 — opt-in bypass of the all-10 exact-scale one-sheet limit

## Actual trigger

REV73 workstation case, 14 selected objects and all ten native views at
exact 1:10, reports no valid one-sheet fit among the project-registered
empty Ortho sheets. The closest reported layout required approximately
753.6 × 1100.1 mm of available layout area; selected landscape medium
provided approximately 854.1 × 687.6 mm after reservations.

This is a real **page-fit** shortfall, not an XREF pipe recognition
failure. Skipping physical bounds checks would fabricate an apparently
passing drawing with off-sheet/overlapping viewports, captions or labels.

## User-facing override

New Ortho-tab checkbox: **If All 10 won't fit, split across sheets**.

- **Default OFF**: preserves REV73 one DWG/one PDF at requested 1:N,
  or no drawing when no seed is large enough.
- **Opted IN**: attempt the original all-ten exact-scale layout FIRST.
  If its sheet-fit preflight cannot fit registered empty native Ortho
  media, leave the original source and project untouched and
  deterministically divide the ten selected native views into balanced
  subsets, retaining native view order. For every subset, run the same
  native generator with exact original 1:N, automatic registered-empty
  seed discovery and all original frame, title, dimension/annotation,
  saved-DWG and PDF publication checks. A subset that is still too
  large is bisected again until fitted or reduced to one view.
- Only **actual physical no-fit failures before registration** may
  trigger splitting. Invalid source DWGs, source/Plant API failures,
  missing metadata, invalid seeds, or model errors may not.
- Each successful subset produces a **separate native Ortho DWG**;
  production PDF publishing creates its **corresponding PDF** with
  per-sheet JSON/TXT and combined register of generated entries.
  The plugin never fakes an all-ten single-sheet output, changes scale,
  shrinks titles or secretly relaxes viewport validity.
- No automatic persistence of the checkbox: opting in is deliberate
  each time the user resets/loads UI preferences or changes project.
- The feature is not available to batch mode or automatically scaled
  ten-view runs. Existing four public commands remain unchanged.

## Runtime QA

For the existing one-sheet fit, output is byte-for-byte equivalent
modulo normal timestamp/Guid metadata because no new fallback runs.
For the no-fit case, inspect SPTC REV74 console messages, individual
`*_RESULT.txt`, sheet PDF files and combined register. Incomplete
subsets are reported as **INCOMPLETE** and are never presented as
a complete production issue.

## No project hard-coding

No stored project paths, sheet media dimensions, pipe tags, source
handles, camera placements, model coordinates, drawing IDs, revisions
or paper overrides. Each chosen seed is still validated and belongs
to the active Plant 3D project's registered EMPTY Ortho drawings.
The existing REV73 geometry-based XREF pipe audit remains independent.

## Acceptance test

Use an unmodified copy of the test project, load the REV74 DLL in a
fresh Plant 3D 2025 session, choose All10 at 1:10 with AutoSeed and the
new checkbox ON, and select the original 14 source objects. Confirm
all ten view presets across the generated separate sheets and exact
1:10 on each, each result's native QA and actual PDF plot appearance.
If a subset is not feasible, report its failure rather than forcing
invalid placements.
