# REV68 — Optional native member-width dimensions and PDF audit reporting

## Scope chosen by the user

**Do not fix or change the title-block display.** The printed A0
`test1` and `####`, its block definitions, attributes, registration,
title-block writer and production attribute mappings remain **exactly
unchanged** in REV68. Do not infer fields or edit template text.

Keep the verified REV67 baseline for TEST5:
- One project-registered A0 Ortho DWG (1189×841 mm), **10 views**
  (TOP/BOTTOM/LEFT/RIGHT/FRONT/BACK/NW/NE/SW/SE) at the
  **exact 1:10** viewport scale.
- 50/50 native support/member/pipe annotations, 2.15 mm minimum
  printed text, and single-line view titles.
- **25 accepted DIMLINEAR of 27 requested**, including all **12/12
  essential H/V overall dimensions**, 0 accepted witness crossings,
  QA PASS and one published verified PDF.
- Two omitted optional `NATIVE:8A4`
  `V_LOCAL_MEMBER_WIDTH` in LEFT and RIGHT. The REV67 log shows
  `REV67_AVAILABLE_LEADER_DIM_TRIALS=20`, 80 layouts proposed, 65
  valid, but zero extra local dimensions. Rejection causes include
  witness-to-witness intersections, line/text-to-current-view leaders
  and text crossing native member silhouettes. Simply increasing
  label relocation trials is insufficient.

## REV68 changes

1. The original native witness stations (verified Plant Start/End
   straight axis fractions -.25,+.25,-.375,+.375) and dimension-line
   lanes remain **first and unchanged**.
2. Only for an actual large A0-or-larger paper drawing and only
   when earlier methods cannot fit an optional local member width,
   examine more source-verified, interpolated physical member-axis
   sections along the same supported straight Plant Start/End axis.
   No XREF member, opaque/unverified axis, fabricated bolt-hole datum
   or unsupported member qualifies. This still carries projected
   *envelope approximation* provenance, not an invented exact native
   BREP edge.
3. At these optional width sections, evaluate additional measured
   paper-offset dimension baselines, **not** only repositioned
   annotation leaders. Bound supplementary trials by separate
   400-station and 240-offset candidate budgets per local
   member so the latter cannot exhaust the former. Preserve every
   earlier accepted dimension, protected overall lane, text/leader
   keepout and actual native DIMLINEAR witness crossing test.
   Unsuccessful optional widths remain explicitly marked as omitted;
   **27/27 is not guaranteed** until a live Plant 3D test.
4. Report `REV68_A0_EXTRA_WITNESS_AND_LINE_GEOMETRY_TESTED`,
   `REV68_A0_EXTRA_LOCAL_DIMENSIONS_RECOVERED` and the existing
   `REV22_46_DIMENSION_SKIPPED` collision causes in the native
   generation `RESULT.txt`. Baseline QA still requires all 12
   essential H/V overall axes, zero unexpected witness crossings
   and safe body/title dimensions.
5. After the native PDF has **already passed** the original publisher's
   byte/header/EOF verification, append the independent basic
   `REV67_PDF_STRUCTURE`, xref position and external-reader warning
   to the **existing RESULT.txt**. Never rewrite PDF bytes, fabricate
   missing logs, turn a parser warning into a silent PDF change,
   or modify the published production issue status. The structural
   check remains a lightweight audit, **not** an Acrobat/qpdf
   conformance declaration.
6. Keep `PLANTORTHOUPDATE` fast path, all four existing commands,
   native view positioning, scale 1:10, PDF publisher, and
   REV66 typography unchanged. No title-block source files changed.

## Workstation acceptance

1. Use a **copy** of the Plant 3D 2025 TEST5 project, load REV68 DLL
   with NETLOAD, select `1:10 + All 10 + Auto empty A0 seed`,
   choose 8A3/8A4 support members and generate.
2. Confirm one native Ortho DWG + one A0 PDF, 10/10 native views,
   exact CustomScale 0.1 on each, 50 annotations and 2.15 mm
   minimum readable text. The title block remains as in REV67
   **by explicit user request**.
3. Check requested/created dimensions and
   `REV68_A0_EXTRA_LOCAL_DIMENSIONS_RECOVERED`. If recovery
   is possible, require the extra genuine dimension(s) without
   changing any prior accepted dimension. Otherwise accept
   a truthful 25/27 with explicit skip causes.
4. Confirm QA PASS, **12/12 essential overalls** and zero witness
   intersections, not merely the dimension count.
5. Read `REV67_PDF_STRUCTURE` in the saved RESULT.txt
   (after PDF publication). Independently open/print PDF in Adobe
   Acrobat or use `qpdf --check` if available. Do not infer
   full compatibility from BASIC_STRUCTURE_PASS alone.
6. Open the new registered A0 DWG and run native
   `PLANTORTHOUPDATE`; manually compare view scales, annotation
   positions and dimension count before considering issue.
7. Regression: Automatic 1:25 All10 A1 path and previously verified
   REV66/REV67 single-sheet 1:10 A0 paths remain intact.

No PR merged, no title block changed, and no files deleted.
