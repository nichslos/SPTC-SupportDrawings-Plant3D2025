# REV81 — Native DIMLINEAR recovery and legacy REV dimension audit

## Report-backed defect

A current Plant 3D 2025 REV80 run, generated from two native channel
members and three native pipe sources in TEST5, has:

- **10/10 native Ortho viewports generated**, metadata version preflight
  and readback passed, but no dimensions
- Automatic paper scale resolved to **1:15** (not 1:10)
- The verified dimension planner selected **12 native chain,
  12 independent overall, 4 local profiles**
- The final REV22.58 witness audit found **two unintended crossings**
- The safety guard threw before the first native DIMLINEAR write.
  Production records correctly reported QA REVIEW / 0 dimensions.

The existing REV22.58 fail-closed safety guard is NOT removed.
This patch keeps model-derived native source measurements, XREF
exclusions, per-view title/label keepouts, dimension style and
native viewport geometry unchanged.

## Root cause and legacy code fixes

The older REV22.60 profile coordination can select mutually compatible
optional candidates but the final complete view still requires an
independent pairwise witness audit. Until now, the final audit had
only a count, and one remaining pair prevented **all** dimension writing.

REV81 introduces a pure geometry-based final audit:
- Read the exact accepted paper-space baseline, two witness segments,
  and actual text keepout of every chosen dimension.
- Record each conflicting pair with the **view, both source IDs,
  exact paper-space witness segments, baseline segments and reason**.
- Preserve all essential overall, verified endpoint chain and
  terminal remainder dimensions.
- If an intersection involves an optional native member-profile width,
  drop only an optional participant using the number of resolved
  conflicts and degree of verified native evidence as deterministic
  criteria.
- Recheck ALL remaining pairs before releasing any dimension for
  native writing; count and label optional omissions in RESULT.txt.
- If mandatory dimensions cross, leave the existing safety guard
  in force — no unsafe writing, fabricated dimensions or hard-coded
  member-specific placement.

The preexisting revision-independent SupportOrthoDimensionReflow
previously returned early whenever optional omission and
chain/overall hierarchy counts were zero, **ignoring nonzero final
witness intersections**. The REV81 fix adds independent triggering
for witness conflicts and allows lane-rank improvements that remove
crossings even if omission and hierarchy counts do not change.

A repaired optional omission is exposed as a reflow candidate,
allowing existing geometrically verified lane alternatives to
recover an optional detail before accepting the final drawing.
Required source witnesses, measurements, overall counts, chain
counts and verified native member IDs stay immutable.

## Scope of 'old REV code'

The user's request to fix old REV code is applied to the **legacy
dimension conflict and layout/reflow path** identified in the real
runtime failure. Historical REV identifiers appearing in diagnostics
are retained for existing register automation, tests and backward
compatibility. There is no indiscriminate removal of unrelated
legacy branches (REV74/75 paper placement, REV76–79 UI, REV80 native
Ortho metadata, XREF classification, title block, etc.), because
that would invalidate the user's previously verified baselines.

## Non-hardcoding guarantee

No target material size, project root, filename, native entity handle,
coordinates, output camera, dimension measurement, member length,
paper width/height, A0 size or fixed witness-station correction is
added. Existing drafting style policy uses bounded paper-space
tolerances independently of project geometry; REV81 introduces no
new model-specific tolerance.

## Source-proof tests

Pure smoke tests verify a witness crossing an essential overall,
deterministic optional omission, preservation of a higher evidence
native profile, safe no-op on an already clear drawing, refusal to
remove essential dimensions, per-pair source/witness diagnostics,
and a crossing-only legacy reflow retry. Existing REV58/60/71/75,
native metadata REV80, paper, XREF, dimensions and UI tests all run
in the same Windows GitHub workflow.

## Workstation acceptance

Test **a copy** of Plant 3D 2025 project with the same source support
and paper/scale options. Validate:

1. Ortho still registers and the 10 native views remain correct.
2. `DIM_FINAL_AUDIT_OPTIONAL_PROFILES_OMITTED` and
   `DIM_FINAL_AUDIT_DETAIL` explain any optional omissions.
3. `REV22_58_ACCEPTED_UNINTENDED_WITNESS_INTERSECTIONS=0`,
   followed by `REV22_41_DIMENSION_RESULT=PASS` and
   `REV22_41_DIMENSIONS_CREATED` nonzero.
4. Verify all required overall/chain dimensions and no overlapping
   witness lines visually in the actual DWG.
5. Edit View / Update View in Plant 3D (REV80 registration remains).
6. No unsupported XREF dimensions, no silent drawing-scale change,
   and no PDF publication when production QA remains REVIEW.

GitHub CI cannot assert actual native Plant 3D drawing behavior.
Do not issue or merge until real DWG validation.
