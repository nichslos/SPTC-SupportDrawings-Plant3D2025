# REV60 — title-block mapping diagnostics and conservative optional dimension recovery

## Based on actual REV59 TEST5 result

`SPTC_TEST5_8A4_ORTHO_R229XREF_20261116_010947_b711` renders the 10 native views inside the effective 841×594 mm landscape A1 sheet. It reports 10/10 native views, automatic 1:25, 25 dimensions, 12/12 essential H/V overalls, 2 optional omissions, and a verified published PDF. Preserve this geometry, the REV57 PDF publishing method, and the fast native Update View path.

The `_RESULT.txt` identifies **exactly two remaining local omissions**:
- `LEFT/NATIVE:8A4:V_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE`
- `RIGHT/NATIVE:8A4:V_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE`

It also reports `PRODUCTION_TITLEBLOCK_STATUS=NOT_CONFIGURED`. This is not proof that the seed's text is an editable attribute; the uploaded PDF cannot reveal its DWG block name or ATTRIB tags.

## REV60 scope

1. If the production title-block name/mapping is unconfigured or does not match, read only the **generated Ortho paper layout** and add up to 16 `REV60_TITLEBLOCK_CANDIDATE` lines to the result. Each lists the exact editable block-reference name, handle, and ATTRIB **tag names only**; attribute values are never logged. If there are no editable paper-space blocks, report that explicitly.
2. Keep exact, explicitly configured field-to-tag mappings mandatory. Once the mapping is configured, replace blank or unmistakable seed `####` / `????` values without requiring broad overwrite. Only in an **explicitly mapped DrawingNumber ATTRIB**, `SPTC_NATIVE_SEED_01` is also treated as an unissued seed placeholder. Preserve every other nonempty mapped value unless the user opts into overwriting.
3. Verify that title-block name and all tag mappings survive project settings SaveAndVerify, rather than checking only project/revision/PDF options.
4. Try two extra bounded annotation leader placements **only when the safe dim planner reports omitted native local width**. Every accepted candidate must add a native local detail while preserving all existing dimension endpoints, line positions, text positions, provenance, essential overalls, and nonintersection checks. Align relocated label safe-right boundary with REV59's 25% side-title-block exclusion.
5. Preserve the automatic paper/viewport placement, 10 preset views, the production UI's other settings, and the REV57 PDF path; do not artificially write unverified dimensions or claim the drawing is issue-ready.

## TEST5 acceptance

1. Run `SPTC_SUPPORTORTHO10` on a *copy* of TEST5 with REV60 DLL; compare generated native DWG + PDF against REV59.
2. Confirm the two-column, five-row placement remains entirely inside the border and no label enters the side title block.
3. In `*_RESULT.txt`, inspect `REV60_TITLEBLOCK_CANDIDATE` and identify whether the real title block is a unique editable `BlockReference` with ATTRIB tags. If yes, open Production Package settings, enter the **exact** title block name and individual `DrawingNumber=... / Revision=... / Title=...` mappings using *observed* tag names; leave broad overwrite disabled.
4. Rerun on a fresh output DWG. Confirm mapped placeholders change to actual project values and nonmapped text/geometry remain intact. If placeholders are static DBText/MText or belong to an XREF, do not stamp them by guessing; revise seed title-block authoring separately.
5. Check `REV22_64_EXTRA_NATIVE_LOCAL_DIMENSIONS`, `REV22_62_REMAINING_LOCAL_NO_CLEAR_LANES` and `REV22_61_QA_OPTIONAL_OMISSIONS`; improved optional coverage is possible, **not guaranteed**. The 12 essential overall dimensions must remain present.
6. Inspect the PDF at **actual A1 size** for readable callout text; REV60 deliberately does not increase text size without an actual clearance test. Run manual native `PLANTORTHOUPDATE` and verify saved DWG and PDF again.

## Limits

Unit tests and Windows CI cannot inspect your actual title block contents or validate Plant 3D 2025 native Update View. The source PDF has `SPTC_NATIVE_SEED_01` and `####` visible, but we cannot know whether they are ATTRIB, DBText, MText or XREF until title-block discovery or the original DWG is inspected. Leave the draft status intact until visual and source tests pass.
