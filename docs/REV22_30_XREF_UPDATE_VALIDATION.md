# REV 22.30 — XREF Property and Leader Alignment Verification

## Commands

- **Generate with REV22.29**: `SPTC_ORTHO22_29_XREF_COMPACT_TEST` (unchanged).
- **Check text persistence with REV22.29**: `SPTC_ORTHO22_29_AUDIT_UPDATE` (unchanged).
- **NEW read-only verification**: `SPTC_ORTHO22_30_VALIDATE_XREF_UPDATE`.

## Procedure — copied / backed-up Plant 3D 2025 test project

1. `NETLOAD` the REV22.30 DLL.
2. From a **project-registered source model** with loaded Pipe XREFs, generate an Ortho using the preserved `SPTC_ORTHO22_29_XREF_COMPACT_TEST` command, saving its DWG and `_REV22_29_RESULT.txt` in the same Orthos folder.
3. To test stale metadata/geometry, change a **copy** of the referenced Pipe's Line Number Tag, Bottom of Pipe property, or model location (one at a time). Reload the XREF in the registered source model. Do not use a production project.
4. Open **both** the source model and the resulting Ortho DWG in Plant 3D 2025.
5. Make the Ortho DWG the active document. Manually run native **Update View** as normal, and save.
6. Run `SPTC_ORTHO22_30_VALIDATE_XREF_UPDATE` from that Ortho DWG. The command reads the original `_REV22_29_RESULT.txt` to locate the source model, expected XREF keys, saved line tags/BOP, WCS pipe centers, ten native viewport positions and original pipe annotations.
7. Review the new `_REV22_30_XREF_UPDATE_VERIFICATION.txt` file beside the Ortho DWG.

## Interpretation

The command checks:

- Whether original pipe XREF keys still resolve uniquely in **currently loaded source-model references**.
- Whether current source Pipe Line Number Tag and explicit BOP match the original labels.
- Whether the original two printed DBText lines are still visible at the expected paper positions.
- Whether each leader's first segment still connects the original anchor to its recorded lane.
- Whether each current source Pipe WCS center projects onto the original leader anchor using each **current saved native viewport camera**.

It reports:

- `REV22_30_XREF_UPDATE_VERIFICATION=PASS` — all the above checks passed against currently loaded data.
- `FAIL` — a difference was detected, such as `LINE_TAG_CHANGED`, `BOP_CHANGED`, `STALE_VISIBLE_LINE_NUMBER`, `PIPE_LEADER_SOURCE_MISALIGNED`, or `PIPE_LEADER_SEGMENT_MISSING`.
- `NOT_VERIFIED` — the source drawing was not open, a referenced Pipe or field could not be resolved reliably, the log was incomplete, or an API read failed.

**PASS does not prove native generated model linework was refreshed after Update View.** It checks leader projection relative to the saved viewport and currently loaded XREF geometry, not pixel-by-pixel or 3D Ortho linework regeneration. Disk XREF reload status and whether Update View was actually executed are also not independently observable and are flagged `NOT_VERIFIED` in those dedicated fields.

## Safety

The REV22.30 command does not change drawing geometry, project databases, PnP properties, viewports, labels, or leaders, and does not invoke Update View. It writes a new TXT report only. All prior REV22.27–22.29 commands remain available. The native 10-view generator in REV22.29 still relies on an unsupported Autodesk internal Ortho API: run it only in a copied or backed-up test project.
