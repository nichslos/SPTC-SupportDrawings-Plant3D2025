# PLANTORTHOUPDATE performance diagnosis

This plugin does not replace or accelerate Autodesk Plant 3D's native Ortho regeneration. It observes successful `PLANTORTHOUPDATE` completion and, for supported SPTC-generated drawings with a valid saved manifest, refreshes plugin-owned paper-space dimensions.

## Post-update safeguards

- The native command event performs no geometry reads or writes.
- Repeated successful native update events for one document are coalesced. Post-processing does not run between them.
- Work is scheduled in the correct, active AutoCAD document context after a short quiet period. Cancelled or failed native commands do not trigger new dimension writes.
- Unmanaged drawings, missing sources, and unchanged model/viewport/owned-dimension states skip unnecessary source loading.
- When the source model is unchanged and only the viewport changes, the existing verified source geometry snapshot is reused.
- On a required rebuild, paper-space scanning also counts owned DIMLINEARs in that same scan, eliminating the previous second full traversal.
- Only plugin-owned dimensions may be rewritten; user dimensions, source geometry, XREF dimension exclusions, title blocks and labels stay untouched.
- The reference dimension planner and drawing quality audits are retained.

## Reading performance measurements

After a **completed** native update and deferred plugin check, AutoCAD displays a compact command-line status line with stable diagnostic names:

```text
ORTHO_PERF_NATIVE_MS=... POST_REFRESH_MS=... COALESCED_NATIVE_COMMANDS=...
MODE=... MANIFEST_MS=... FAST_PAPER_MS=... SOURCE_MS=... LAYOUT_MS=... WRITE_MS=...
```

`ORTHO_PERF_NATIVE_MS` measures command start to command end **including any user interaction inside the native command**, and cannot distinguish prompting from Autodesk processing. The `POST_REFRESH_MS` duration includes the plugin's deferred command-context scheduling and work. The stage times split the actual managed refresh.

Common `MODE` values:
- `NON_SPTC_DRAWING`: plugin does not change this drawing.
- `UNCHANGED_FAST_PATH`: original saved source and native viewport pose remain current and owned dimensions are intact.
- `UNCHANGED_PLAN_CACHE_UPDATED`: deeper verification found no need to replace dimensions.
- `REPLACED_PLUGIN_OWNED_DIMENSIONS`: verified plugin-owned dimensions were rebuilt.
- `FAILED_PRESERVED_ORIGINAL`: guarded refresh threw; the plugin reports the exception instead of claiming an update succeeded.

## Interpreting bottlenecks

If native milliseconds dominate, inspect model/XREF complexity, source DWG location, Plant 3D native Ortho settings and project/network I/O. This plugin does not safely alter Autodesk's private Ortho generation APIs or promise to shorten their runtime.

If `FAST_PAPER_MS` dominates, consider the drawing's paper-space entity count. If `SOURCE_MS` dominates, read-only source DWG loading and source geometry extraction are the likely bottleneck. If `LAYOUT_MS` dominates, inspect view count, structural sources, annotation keepouts and dimension collision candidates.

Use the same saved 3D model and a copy of the Ortho drawing to compare before and after. Confirm original line tags, BOP/TOS, all ten native views, overall dimension QA, and the current verified local dimensions remain correct. Keep the previous compiled DLL as a rollback option.
