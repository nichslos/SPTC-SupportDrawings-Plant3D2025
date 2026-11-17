# REV70 — Title-block reservation bypass for every registered native Ortho paper size

## User requirement

REV69 made title-block area bypass conditional on *A0 landscape* paper dimensions: width ≥1100 mm and height ≥800 mm. The user requested removing that hard-coded restriction and using **all paper sizes actually available as registered, EMPTY native Ortho drawings in the current Plant 3D project**.

## Changes

- Removed the A0 1100×800 minimum and any paper-name or landscape-only eligibility requirement. The exact same physical-page title-block bypass logic now applies to **any valid oriented native media size** returned by the existing Plant 3D registered, EMPTY Ortho seed discovery. This includes portrait, landscape, custom plotter sizes and smaller/larger standard formats where the 10-view layout actually fits.
- Eligibility uses the existing **NativeOrthoEffectivePaper59.FromMedia** finite positive physical size validation. No paper formats are invented and no project registry is modified. Only previously verified *registered EMPTY native Ortho DWGs* may become seeds. The source layout, plotter's real media and output page remain unchanged.
- For explicit manual 1:N **All 10**, every registered empty seed is checked against the original strict title-block reserve **first**. If any strict media fits, the smallest such sheet is selected and its existing drawing placement remains unchanged. **Only if none fits strictly**, the plugin tries the bypass against all physically valid registered media and selects the smallest resulting fit. A manually selected seed gets the same strict-first/physical-fit fallback.
- A valid paper size is not proof of adequate physical space. The exact ten projected native 1:N viewports, ten captions, annotation gutters, dimension witness/leader keepouts, real native post-generation viewport scales, and printable border still must fit inside the selected paper.
- The existing fallback limits are **proportional to the actual oriented paper**: left 3%, right 97%, bottom 10%, top 94%. When bypass is off the original right 75% and bottom 12% guards remain.
- The old A0-only failure text was replaced with format-neutral instructions: if no existing registered Ortho media can fit the assembly, register an appropriately sized **empty native Ortho seed** with supported plotter media; do **not** secretly rescale the assembly or split 10 views across pages.
- New RESULT.txt diagnostic fields:
  - `REV70_PAPER_SIZE_POLICY=ALL_REGISTERED_EMPTY_NATIVE_ORTHO_MEDIA`
  - `REV70_EFFECTIVE_REGISTERED_PAPER_MM=(width,height,0)`
  - `REV70_PROJECT_PAPER_BYPASS_ELIGIBLE=True/False`
  - Existing `REV69_TITLE_BLOCK_RESERVED_AREA_BYPASSED` and `REV69_TITLEBLOCK_OVERPRINT_POSSIBLE` are intentionally retained for compatibility.
- **No edits to title-block display**: title, test1, ####, block contents, attributes, location and PDF publisher are not changed. If the bypass crosses the old title-block reserve the unchanged printed title-block graphics may be overprinted: **DRAFT + PDF review required**.
- All 10 native preset views, exact 1:N scale, one registered DWG + one PDF, four production commands, existing 2.15-mm A0 text, 12 essential H/V overall dimension safeguards, and PLANTORTHOUPDATE fast path are preserved.

## Tests and live acceptance

- Pure regression cases: A1 landscape 841×594, A1 portrait 594×841, custom 920×650, smaller A2-like 594×420. In each case a representative assembly fails strict title-block reservation and fits physically with fallback at 1:10.
- A3-like 420×297 is *eligible for testing* but deliberately fails a physically impossible full-size assembly; no false acceptance.
- Reject unregistered seed candidates and nonfinite/invalid paper dimensions; validate dynamically selected smallest registered fitting seed.
- Existing REV66–REV69 TEST5 A0 strict-first drawing regression stays unchanged.
- Plant 3D 2025 acceptance: run `SPTC_SUPPORTORTHO10` on an existing small/large/custom registered empty Ortho drawing at exact 1:10 with All10; inspect source/actual media, native viewport scale, all ten views, captions, labels, dimensions and PDF title-block overprint warning. Verify `PLANTORTHOUPDATE` manually before production issue.
- Windows CI verifies geometry tests and compiles the DLL; it does not prove actual page media installed on user's workstation or final Acrobat print fidelity.

No source model, titleblock object, registry, native PDF device or previous PR is modified by this change.
