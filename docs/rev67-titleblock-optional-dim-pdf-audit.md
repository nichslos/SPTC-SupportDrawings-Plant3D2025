# REV67 — Fix the A0 20-trial bug; inspect title-block nesting and native PDF structure

## Actual TEST5 baseline (REV66)

The latest **TEST5** A0 drawing was generated with source handles 8A4/8A3
and three discovered native pipes, one registered 1189×841 mm A0 Ortho
sheet, **all ten native views at exactly 1:10**, 50/50 source
annotations, all 10 single-line view titles, 2.15-mm measured
minimum DBText, 25/27 reference dimensions and 12/12 essential
overalls, independent QA PASS and one verified PDF.

Outstanding production-readiness items:
- A0 title block named `PID ISO A0 titleblock` has zero editable
  ATTRIB references and printed `test1` / `####`. REV66 reported
  10 direct static text elements but **zero obvious placeholders**.
- Two optional `NATIVE:8A4` LEFT/RIGHT V_LOCAL_MEMBER_WIDTH
  dimensions were omitted. The REV66 A0 coordinator requested **20**
  trials but its hardcoded `Trials` array had only **14**, causing
  its validation to throw before the extra search could run.
- Although the native PDF was generated and verified with the existing
  header/EOF safeguard, an independent PDF parser produced nonfatal
  syntax/stream-length warnings. Native PlotEngine output should NOT
  be rewritten or reported as Acrobat-compatible on this evidence.
- Native `PLANTORTHOUPDATE` verification remains outstanding.

## REV67 implemented changes

1. Add **six** A0-specific, bounded label/leader alternatives after
   the existing fourteen, making the promised twenty usable. Existing
   source anchors, all previously accepted dimension coordinates and
   measurements, the essential 12/12 overall axes, paper keepouts,
   existing collision detection and zero witness-intersection checks
   remain strict. Log the actual number of available trials and
   rejection reasons. Not all 27 dimensions are guaranteed.
2. Correct annotation dogleg intersection calculation to use the
   current actual sheet's text line advance (REV66 A0=3.5mm, A1=2.7mm),
   so optional dimension preflight and the actual larger label
   rectangles agree.
3. Extend the **read-only** title-block inventory to include up to
   five nested internal block definitions, up to 4096 definition
   entities, text inside ATTDEF defaults that were not instantiated as
   editable attribute references, and MText formatting surrounding a
   true placeholder. Logs include nested-definition count, direct
   static text count, uninstantiated ATTDEF count and obvious
   placeholder count. No template entity, XREF or static text is
   modified. Do not assume the visible `test1` means a drawing number;
   approved exact FIELD=TAG mapping with an actual editable attribute
   reference is still required for population.
4. Add a **read-only** native PDF basic structure inspection after
   the existing PDF has already been successfully plotted and verified.
   Check final `startxref`, byte offset, xref table or object-stream
   target, `%%EOF` and trailing data. Log
   `REV67_PDF_STRUCTURE=BASIC_STRUCTURE_PASS` or a specific review
   diagnostic. Never alter the PDF, change the PlotEngine, weaken native
   QA, or equate a basic cross-reference check to an independent full
   PDF parser/Adobe Acrobat print test. Published PDF remains a
   **DRAFT** pending independent review.

## Acceptance in Plant 3D 2025

1. Back up REV66 DLL and use a copied TEST5 project. NETLOAD REV67,
   choose 1:10 + All10 + automatic registered A0 seed; select the same
   8A3/8A4 support members.
2. Confirm one registered native DWG, all 10 preset viewports, all
   viewports CustomScale=.1, one actual A0 PDF, 50/50 native
   annotations with at least 2.15mm minimum and single-line titles.
3. Verify `REV67_AVAILABLE_LEADER_DIM_TRIALS=20`; compare
   `REV22_64_EXTRA_NATIVE_LOCAL_DIMENSIONS` and
   `REV22_64_LOCAL_OMISSIONS`. Require 12/12 essential dimensions
   and zero unintended witness/geometry collisions. If the two
   optional 8A4 widths still cannot fit, their skip reasons must
   remain explicit; do not force overlaps.
4. Inspect `REV67_TITLEBLOCK_NESTED_INSPECTION` and
   `UNINSTANTIATED_ATTDEF_COUNT`. If no true ATTRIB references exist,
   the title block status must remain NOT_CONFIGURED and printed
   text unchanged. Update an approved **copy** of the A0 Ortho
   template with ATTDEF, instantiate ATTRIB in its title-block
   reference and configure exact FIELD=TAG mappings before stamping.
5. Inspect `REV67_PDF_STRUCTURE` and independently open/print the
   published PDF with Adobe Acrobat; optionally run `qpdf --check`
   externally. Parser warnings require separate investigation of the
   PDF printer/device/font stream, not silent rewriting of the
   native PDF.
6. Open the newly registered A0 DWG and manually invoke
   `PLANTORTHOUPDATE`, verifying all ten views, dimensions, BOP/TOS
   labels and title blocks remain stable. The fast path is untouched.

## Regression protections

- REV59 automatic all-ten A1 1:25 layout unchanged.
- REV65 exact manual 1:10 one-sheet A0 placement and seed selection
  unchanged; no split, autoscale or cropping.
- REV66 measured 2.15-mm A0 label fonts and single-line title checks
  unchanged.
- No new support commands, modifications to native OrthoCube,
  material/annotation source data, or source-drawing geometry.
- No destructive changes to existing registered DWG/PDF or GitHub PRs.

**Runtime limitation:** Windows CI validates pure C# policy and builds
the DLL but cannot prove Plant 3D live Update View behavior or
Acrobat compatibility.
