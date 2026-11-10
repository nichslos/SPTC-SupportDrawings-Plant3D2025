# REV 22.29 — Compact XREF Pipe Labels and Update View Audit

## What changed
- New command: `SPTC_ORTHO22_29_XREF_COMPACT_TEST`.
- The source PIPE still comes from the active drawing or loaded nested XREF, as in REV22.28.
- The drawing callout for each pipe is now exactly two DBText lines:
  ```text
  LINE NO: TEST-001
  BOP EL. +3899.9543 mm
  ```
- The internal identifier (e.g. `XREF:MODEL/XREF:73D:TEST6/409`) remains in `REV22_29_PIPE_SOURCE` and `STEP10_MEMBER_LABEL`'s SOURCE value **in the TXT log**, not in the printed pipe label.
- Steel/Support callouts, TOS handling, 10 automatically placed views, OrthoCube sizing and other behaviors are unchanged from REV22.28.
- Both previous commands `SPTC_ORTHO22_27_PIPE_BOP_TEST` and `SPTC_ORTHO22_28_XREF_AUTO_TEST` are preserved.

## Test in a copied/backed-up Plant 3D 2025 project
1. `NETLOAD` the new `SPTC.SupportDrawings.dll`.
2. From the project-registered source 3D DWG (with loaded pipe XREFs), run `SPTC_ORTHO22_29_XREF_COMPACT_TEST`.
3. Select a structural/piping support; you **do not** have to select each pipe.
4. Continue the existing registered empty Ortho seed and explicit copied-project confirmation.
5. Verify all 10 saved native views, the compact printed labels, the exact BOP and the unchanged Support TOS. The TXT is alongside the generated Ortho DWG as `<Ortho DWG stem>_REV22_29_RESULT.txt`.
6. **Save** and keep a copy/screenshot of the generated Ortho drawing before native updates.
7. Manually run Plant 3D **Update View** on the generated Ortho drawing. Autodesk handles the native update; the plugin does not invoke it.
8. In the generated Ortho DWG (active), run `SPTC_ORTHO22_29_AUDIT_UPDATE`.
9. Read `<Ortho DWG stem>_REV22_29_UPDATE_AUDIT.txt` and inspect `REV22_29_UPDATE_AUDIT=PASS` or `FAIL`.

## Audit interpretation
The audit compares all generated pipe first/second lines in the original log against `DBText` on the `SPTC_MEMBER_CALLOUTS` paper-space layer, including expected positions. It uses no DWG writes.

- **PASS** — every original pipe label text line still exists at its original paper-space position.
- **FAIL** — a label disappeared or moved significantly.
- **NOT VERIFIED** — properties may have changed in the source DWG; leaders may point at stale geometry; XREF line tag/BOP may need rereading. A PASS does not prove accurate annotation after model changes.

Inspect leader/geometry and property freshness manually against the XREF source following native Update View.

## Safety and boundaries
The 10-view generator still relies on Autodesk's unsupported internal Ortho API. Do not use on an unbacked production project. REV22.29 does not claim persistent regeneration hooks or automatic annotation reapplication after Update View. The post-update command is read-only and reports the actual result.
