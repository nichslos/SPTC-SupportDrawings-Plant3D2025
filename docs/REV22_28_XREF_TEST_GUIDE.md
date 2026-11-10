# REV 22.28 — Auto-discover native and XREF pipes (Plant 3D 2025)

## Commands

- **REV 22.28:** `SPTC_ORTHO22_28_XREF_AUTO_TEST` — selects the structural/piping support, then automatically discovers nearby native Pipe entities in the current drawing and within **loaded XREFs**, including nested XREFs.
- **REV 22.27:** `SPTC_ORTHO22_27_PIPE_BOP_TEST` — unchanged fallback; manual Pipe selection and existing working annotations.

Both commands use Autodesk's **unsupported internal native Ortho generation API**. **Run only on a copied / backed-up test project.** The command retains the explicit test-project confirmation and registered empty Ortho seed requirement.

## REV 22.28 changes

1. Select the same structure member/Plant Support as before; there is **no separate Pipe selection**.
2. Scan nearby native Pipe entities from the host drawing and loaded XREF databases, including nested XREF instances (read-only).
3. Transform each pipe's geometric extents from source drawing coordinates to the host drawing's WCS using its accumulated XREF BlockTransform. Clip extents to the support-centered OrthoCube.
4. Look up **explicit** Plant PnP Line Number Tag and BOP fields using the pipe's actual source entity ID. If data links are unavailable, attempt actual source entity property descriptors.
5. Transform BOP elevation when the XREF preserves horizontal elevation planes; otherwise mark BOP unavailable instead of reporting a potentially incorrect elevation.
6. Preserve the ten automatic native views, existing annotation overlap protection, TOS, and working REV 22.27 command.

## Validation

Test separately:
- Native Pipe in active source DWG, with explicit line tag/BOP.
- One XREF of a **project-registered Plant Piping DWG**; test a translated and Z-rotated insertion.
- Multiple XREF instances and a nested XREF.
- An unresolved/unloaded XREF and a pipe missing PnP properties; the plugin must report the skip, **never invent BOP/Line Number Tag**.
- Compare actual annotations against Plant 3D Properties/Data Manager and compare 10-view native Ortho geometry.

Check the REV22_28_RESULT TXT for `XREF_PIPE_ACCEPTED`, `XREF_PIPE_METADATA_UNAVAILABLE`, `XREF_PIPE_READ_ERROR`, `XREF_BOP_TRANSFORM_UNSUPPORTED`, and `REV22_28_PIPE_DIAGNOSTIC`.

**Important:** Automatic source traversal and .NET compilation were tested in CI; live AutoCAD Plant 3D XREF/PnP metadata binding and drawing behavior have **not** been tested on a project in this environment. If a referenced pipe's metadata cannot be verified, the command stops before creating an Ortho drawing when no XREF pipes can be annotated. This revision does not claim support for unloaded references, converted 3D solids, or third-party proxies.
