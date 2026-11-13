# SPTC SupportDrawings production package — staged release plan

Status: **development / opt-in, never automatically approved for issue**.

This phase builds on the already tested 10 native view, 25/27 local/overall
dimension, 12/12 essential overall, 10/10 TOS, zero dimension intersection,
and fast post-\`PLANTORTHOUPDATE\` acceptance results. It does not change
native Ortho geometry, DIMLINEAR planning, Plant PnP records, or XREF rules.

## Existing supported commands (unchanged)

- \`SPTC_SUPPORTORTHO10\`
- \`SPTC_SUPPORTEXPORT\`
- \`SPTC_SUPPORTBATCH\`
- \`SPTC_SUPPORT2DBATCH\`

No new AutoCAD commands or hard-coded development revision numbers have been
introduced. The original modeless palette, all 10/6/4 native view controls,
existing paper scale/annotation options, and project-specific UI preferences
remain.

## Production package configuration

1. Open a **saved, project-registered source 3D DWG**.
2. Enter \`SPTC_SUPPORTORTHO10\` to display the existing modeless palette.
3. Click **Production package...** on the 10-View Ortho tab.
4. Check **Enable production package metadata and draft register**.
5. Fill **Project code**, **Drawing series**, **Revision**, and **Drawing title**.
6. To fill existing title-block attributes, enter the **exact title block
   block name** and a mapping such as
   \`DrawingNumber=DRAWING_NO\`,
   \`Project=PROJECT_NO\`, \`Revision=REV\`,
   \`Title=DRAWING_TITLE\`, and \`Scale=SCALE\`.
   These are **illustrative attribute tags**. The actual names must be read
   from your company title block; the plugin does not assume them.
7. Leave **Overwrite nonempty, mapped title block attributes** unchecked to
   preserve any existing text. If checked, only explicitly mapped attributes
   in the unique matching paper-space title block may change.
8. Optionally set **Support tag source attribute** to an actual attribute
   tag found on a selected native support BLOCK. Multiple conflicting tags
   are not trusted; the drawing number still includes the stable selection
   fingerprint to avoid collision.
9. Save configuration. It is stored in **local Windows AppData**, isolated
   by a digest of the Plant 3D project root, not in your shared PnP/DCF database.

By default the production package is **disabled**, retaining the identical
original Ortho workflow and output. Project switching does not transfer
another project's package configuration.

## Output naming and register

An enabled package produces a unique, deterministic drawing number:
\`<PROJECT>-<SERIES>-[OPTIONAL-EXPLICIT-SUPPORT-TAG-]<SOURCE-FINGERPRINT>\`.

The 12-character fingerprint is based on the source DWG path and explicitly
selected native object handles, sorted independently of selection order.
It is not fabricated from line numbers or ambiguous XREF metadata.
This ID is stable between runs but source geometry changes must still be
evaluated; the separate source file timestamp/length in the register flags
possible revisions.

Generated sidecars in the native Ortho output folder:

- \`<generated DWG>_RESULT.production.json\` (evidence, drawing number,
  source identity, QA/PDF/revision status)
- \`<generated DWG>_RESULT.register.csv\` for single runs
- An aggregated \`SPTC_DRAWING_REGISTER_YYYYMMDD_HHMMSS.csv\`
  in the source DWG folder for batch runs (only drawings with sidecars).
- Existing \`*_RESULT.txt\` and existing batch TXT reports are preserved.

A failed support that never reaches output creation remains in the existing
batch failure TXT; an output created but incomplete is recorded as
\`GENERATION_FAILED\` in the package register.

## QA and issue safety

The package parser records existing proven markers from the native
generation report, including view count, 12/12 essential overall coverage,
dimension count, QA PASS, witness intersections and optional omissions.

- QA PASS is not fabrication approval.
- **Every generated sidecar starts as \`DRAFT_NEEDS_SAVED_DWG_AND_QA\`.**
  Existing native project APIs may hold the new DWG in memory; a cloned disk
  file is not proof that the annotated DWG was saved.
- PDF is \`PDF_REQUIRED_NOT_PUBLISHED\` when required. **Phase 2 implements opt-in native PDF plotting** after a verified
  DWG save, passing essential QA, exact media and physical PDF checks.
  Any generated PDF remains a draft requiring engineering review.
- The existing attribute-only material register remains the safe BOM
  starting point. Actual fabrication takeoff requires verified class/spec
  and unit rules, so no quantities or cut lengths are fabricated.
- \`CanSkipVerifiedExisting\` is a pure safeguard for future
  resumable batch handling; actual auto-skipping is intentionally disabled
  until a separately verified issue workflow and real saved DWG/PDF status
  exist. New draft drawings are NEVER silently skipped.
- \`ChangeStatus\` is a **file-stamp/configuration candidate**, not a
  geometric revision comparison. No change order is issued automatically.
- A malformed config fails closed and cannot break native drawing creation.

## Release consolidation and dependency order

The working main branch is behind many development branches. Do not
mass-merge open PRs based solely on a successful compilation:

1. Preserve a **backup and rollback DLL** of the last live-verified build.
2. Review feature-branch ancestry in order through the native dimension
   layout, fast update, signed dimension integrity, production UI, and
   DPI/docking improvements. The immediate dependencies for this work are
   PR #47 (dimension integrity), PR #48 (project settings), PR #49 (DPI UI).
3. Confirm the exact candidate SHA and GitHub Actions results, then
   test the candidate on a **copied Plant 3D project**.
4. Verify 10 native views, 12/12 overall axes, at least the previous
   25/27 dimensions, 10/10 TOS, QA PASS, and unchanged update performance.
5. Test existing four commands, batch selection, local saved configuration
   and mapped title block attributes at the user's DPI.
6. Only after live verification and explicit approval, merge or tag a
   production release in dependency order. Keep its compiled artifact as
   a rollback package.

## Follow-on stages, NOT implicitly enabled by this phase

- Native PDF plotting is implemented in Phase 2 as an **opt-in test
  candidate**; runtime operation, output fidelity and company sheet
  compatibility must be validated in Plant 3D 2025 before release.
- Explicit approved/released revision state and issue-number allocation,
  with rollback and no overwriting of issued PDFs.
- Safe batch resume/skip with confirmed saved drawing and verified PDF.
- Structural/XREF read-only expansion with separate source provenance.
- Fabrication material BOM only from explicit supported component
  properties and unit conversions.
- Recover the remaining LEFT/RIGHT 8A4 optional local dimensions only
  if an unambiguous collision-free source witness lane is verified.

At no point should publishing, XREF or materials work bypass native
dimension and witness-intersection guards, change saved source model
geometry, or add undocumented automatic Plant 3D command execution.
