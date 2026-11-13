# Phase 2 — Native PDF publishing for SPTC SupportDrawings

**State:** opt-in test candidate, not a live-validated issue/release process.

## Design and safety

This change is based on Phase 1 production package work and retains all existing production commands:
`SPTC_SUPPORTORTHO10`, `SPTC_SUPPORTEXPORT`, `SPTC_SUPPORTBATCH`, and `SPTC_SUPPORT2DBATCH`.

Native Autodesk Ortho generation, the 10 cameras, dimension calculations, XREF policy, native title/view annotations, `PLANTORTHOUPDATE` scheduling/fast path and the existing safe 25/27 dimension plan are not modified.

New native PDF plotting is **off by default**. It only affects the `SPTC_SUPPORTORTHO10` single/batch native Ortho workflow when a valid per-project production package is enabled and its **Auto-save generated Ortho DWG and publish native PDF** option is explicitly enabled.

In the existing **Production package...** dialog, the operator can choose:
- `DWG To PDF.pc3`
- `AutoCAD PDF (General Documentation).pc3`

These are approved device names; a requested device must also actually be installed on the local AutoCAD machine.

### PDF publication sequence

1. Generate the registered native Ortho drawing normally, preserving existing native view/dimension QA checks and the result TXT log.
2. Build the draft production package entry with the stable project/series/support-derived drawing number.
3. **Preflight:** require QA PASS, the expected number of registered native views, verified essential H/V overall dimensions, no reported witness intersections, and complete explicitly mapped title-block attributes if configured. Optional local omissions may remain if the operator has not requested full dimension coverage.
4. Reject a missing DWG or a pre-existing same-name PDF. Never overwrite or delete a pre-existing issued PDF.
5. Verify the active target document is the newly generated Ortho DWG and not the source model; reject model-space plotting.
6. Explicitly save the already-open, named Ortho DWG using native AutoCAD `_.QSAVE` in its **own document command context** (awaited from the Session command). Do not call `Database.SaveAs` a second time against the same open target file: TEST5 returned `eFilerError` at that step. Verify the DWG was actually rewritten (timestamp/bytes), rather than trusting the earlier empty-seed clone.
7. Use native `Autodesk.AutoCAD.PlottingServices.PlotFactory.CreatePublishEngine()` for a **single recorded paper-space layout**. Copy the layout's page setup to an in-memory override; use an exact installed PC3 and exactly matching canonical paper-media name. Disable paper-size auto-matching to avoid silently changing sheet dimensions.
8. Temporarily set foreground plotting (`BACKGROUNDPLOT=0`) and restore the previous value in a finally block. Keep Autodesk's plot progress dialog hidden because the existing SPTC progress window is visible.
9. Confirm the resulting new PDF physically exists and has a PDF file header and EOF trailer before recording `PDF_PUBLISHED_VERIFIED`.
10. Update the per-drawing JSON sidecar and single/batch CSV register with the PDF output path, PDF byte count, saved DWG byte count, and plot device. On failed/blocked jobs record an explicit `PDF_FAILED_*` or `PDF_BLOCKED_*` status and no fabricated PDF path.

### Generated outputs

For each successful job:
- Native project-registered drawing: `<generated drawing>.dwg`
- Verified PDF after explicit opt-in: `<generated drawing>.pdf`
- Existing Ortho generation report: `<generated drawing>_RESULT.txt`
- Production metadata sidecar: `<generated drawing>_RESULT.production.json`
- CSV register: `<generated drawing>_RESULT.register.csv` (single) or `SPTC_DRAWING_REGISTER_*.csv` (batch)

For batch runs, failures and blocked PDFs appear as failed package rows, even when DWG generation succeeds. The existing per-support sequential progress bar counts completed jobs, not just generated PDFs.

**All successful PDFs remain marked `DRAFT_SAVED_DWG_PDF_NEEDS_REVIEW`, not issue-verified.** PDF file integrity checks are not a substitute for inspecting the plotted layout, lineweights, dimensions, title block and company drawing standards. No automatic engineering approval or revision issue is performed.

## Test procedure (copied Plant 3D 2025 project only)

1. Close Plant 3D and back up the last accepted DLL before `NETLOAD` of this test DLL.
2. On the copied TEST5 project, set the actual registered EMPTY native Ortho seed and select known support geometry. Confirm old baseline: 10 views, 25/27 dimensions, 12/12 essential overall dimensions, 10/10 TOS, zero clashes, and successful update scheduling.
3. Configure the production package with real project/series/title and **exact** company title-block attributes. Check that an unknown or ambiguous mapped title block blocks publishing instead of guessing.
4. Enable PDF publishing and choose a PDF PC3 actually installed on that machine. Confirm the existing native Ortho sheet media is available on that PC3.
5. Generate **one** Ortho first; inspect the saved DWG, PDF page size/orientation/scale, viewport content, dimensions, TOS/BOP annotations, filename and sidecars.
6. Repeat for 2–3 selected supports using batch Ortho; inspect the CSV's individual DWG, PDF and QA statuses.
7. Test an unavailable PDF plotter, missing/invalid title block, already present output PDF, and missing dimension QA. No silent overwrite or false `PDF_PUBLISHED_VERIFIED` status should occur.
8. Run `PLANTORTHOUPDATE` and verify existing fast-path behavior is unchanged. PDF auto-publishing is **not** attached to `PLANTORTHOUPDATE` events.
9. Retain the old DLL rollback package until the new PDF workflow passes a real Plant 3D run.

## CI coverage and limitations

Windows CI compiles against the existing AutoCAD 2025 references and runs pure regression tests for opt-in defaults, exact page/device policy source invariants, QA gating, protected overall dimensions, missing title blocks, non-overwriting, PDF magic/EOF audit, batch status reporting and the old dimension/refresh tests.

The GitHub Actions runner does **not** launch licensed Plant 3D to execute the real native PlotEngine or to inspect rendered PDF geometry. Therefore this build is **not** certified as a field-tested plotting workflow until the user-side TEST5 checks above succeed. A native plotting failure must not invalidate or overwrite the original registered native Ortho drawing.

## Publisher diagnostics (2026-11-13)

If the generated `*_RESULT.production.json` says `PDF_FAILED_NATIVE_PLOT`
but `SavedDwgVerified=false`, the actual error has not been established
from the Phase 2 report alone. In particular, this does **not** prove
that an installed PDF plotter was unable to produce a PDF; failure may
have occurred earlier during explicit DWG saving or native document setup.

The publisher now writes a stage-specific failure code and an exception
summary to `IssueNote` in `*_RESULT.production.json` and to the
`PDFDiagnostic` field in `*_RESULT.register.csv`. Look for:

- `PDF_FAILED_PREPARE` — target document/layout preflight exception
- `PDF_FAILED_DWG_SAVE` — `Database.SaveAs` or document lock failed
- `PDF_FAILED_DWG_VERIFY` — saved DWG verification failed
- `PDF_FAILED_PLOT_SETUP` — PDF plot setup, PC3, or media configuration failed
- `PDF_FAILED_NATIVE_PLOT` — AutoCAD PlotEngine failed after beginning publication
- `PDF_FAILED_FILE_VALIDATION` — generated PDF failed physical integrity check

The original native Ortho `*_RESULT.txt` and dimension planner remain
unchanged. Capture and share both the new JSON and CSV after a new copied
TEST5 run; they identify the operation and exception needed for a targeted
fix. Avoid deleting the previous working DLL until the actual exception
has been isolated and the PDF passes a real in-Plant-3D print review.

## TEST5 SaveAs eFilerError — targeted fix (2026-11-13)

The live uploaded `074645_e81b` production JSON recorded `PDF_FAILED_DWG_SAVE STAGE=DWG_SAVE EXCEPTION=Exception: eFilerError`, despite `PRODUCTION_PACKAGE_ENABLED=True`, `PRODUCTION_PDF_ENABLED=True`, QA PASS, 10/10 views and 12/12 essential dimensions. The native PDF PlotEngine had not yet run. A `Database.SaveAs(targetPath, DwgVersion.Current)` performed on an already-open registered Ortho document was the failing call.

The dependent fix replaces that **specific** call with an awaited `Editor.CommandAsync("_.QSAVE")` executed through `DocumentCollection.ExecuteInCommandContextAsync` against the verified active target document. It does **not** alter the separate seed-clone `Database.SaveAs` used when creating a new drawing. Native save errors are trapped within the command-context callback and propagated as `PDF_FAILED_DWG_SAVE` with exception text. An existing DWG without a physical new save is `PDF_BLOCKED_DWG_SAVE_NOT_VERIFIED` and cannot proceed to plotting.

The plugin must continue to report separate plot failures, as passing a save test does **not** demonstrate that the configured PDF device and page media work on this Plant 3D installation. Validate on a copied TEST5 project, inspect the result JSON `PdfStatus` and `IssueNote`, confirm DWG content persisted and inspect any resulting PDF before using it for fabrication or issue.

## TEST5 2026-11-13 — A1 PDF media negotiation (Phase 2)

On the user's copied TEST5 drawing, native QSAVE was confirmed by
`SavedDwgVerified=true` and `SavedDwgLength=127142`, but PDF publishing
returned `PDF_FAILED_PLOT_SETUP STAGE=PLOT_SETUP EXCEPTION=Exception: eNoMatchingMedia`.
This proves the save issue is resolved in that run; it **does not** prove
that native PDF plotting has succeeded.

The original Ortho layout is `Title Block`, with physical sheet dimensions
`594 x 841 mm` (ISO A1). The former publisher copied the layout's canonical
paper name directly into `SetPlotConfigurationName` for a different PDF PC3
device. Canonical paper names are **plot-device-specific** and must not
be assumed interchangeable.

This fix now:

1. Copies the native layout's page setup to a disposable in-memory
   `PlotSettings` override, leaving native geometry and the original sheet
   untouched.
2. Switches to the specifically approved, installed PDF PC3 **without
   supplying the incompatible seed media name**.
3. Enumerates that PDF device's canonical media names, selecting each
   temporarily and measuring `PlotPaperSize` in millimeters.
4. Selects only a sheet matching the original physical dimensions within
   1 mm per edge, favoring unchanged physical orientation and, where
   available, full-bleed variants. If only the opposite physical orientation
   is available, compensates 90 degrees in the temporary plot override.
5. Logs the chosen PDF device/media/orientation; fails closed without
   publishing if no physically matching media exists. It does not silently
   scale A1 onto A2 or A0.
6. Preserves the already verified QSAVE, QA gates, batch status, four commands,
   dimension layout, XREF rules and fast `PLANTORTHOUPDATE`.

**Live acceptance:** Load this dependent DLL into Plant 3D 2025 on a copied
TEST5 project; inspect generated PDF for full A1 page dimensions, expected
orientation, title-block edges, viewports, annotations and all dimensions.
A passing Windows build tests selection policy and API signatures, not the
behavior of the locally installed PC3 device. All published outputs remain
draft until visual engineering review.

The selected PDF PC3 media list may contain null, invalid or device-specific entries; these are skipped without changing the native paper layout. The final PDF remains subject to a live PC3 layout review on TEST5.
