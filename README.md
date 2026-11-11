# SPTC SupportDrawings — AutoCAD Plant 3D 2025

**REV 22.32 — Production-only plugin.** The verified ten-view native Ortho generator, automatic native/XREF pipe detection, compact Line Number Tag / BOP annotations, structural member and support TOS, and existing support export and inspection commands remain intact.

## Production commands

| Function | AutoCAD command |
| --- | --- |
| Generate 10 native Ortho views, with Pipe/Support annotations | `SPTC_SUPPORTORTHO10` |
| Export one support | `SPTC_SUPPORTEXPORT` |
| Start native interactive Ortho workflow | `SPTC_SUPPORT2D` |
| Inspect selected support | `SPTC_SUPPORTINSPECT` |
| Batch export support drawings | `SPTC_SUPPORTBATCH` |
| Other existing support/export commands | Retained without modification |

**The completed validation commands have been removed:** `SPTC_ORTHO22_29_AUDIT_UPDATE` and `SPTC_ORTHO22_30_VALIDATE_XREF_UPDATE`. Their associated audit implementation and audit-only smoke tests are absent from the active production DLL and codebase.

## Generate ten native views

1. Work in a copied/backed-up AutoCAD Plant 3D 2025 project.
2. Open a saved project-registered source DWG with its pipe XREFs resolved.
3. `NETLOAD` the new `SPTC.SupportDrawings.dll`.
4. Run `SPTC_SUPPORTORTHO10`; select your support and follow the existing empty Ortho seed and copied-project safety prompts.
5. The generator auto-places ten native Ortho views and adds the same two-line pipe `LINE NO` / `BOP EL.` labels plus Support/Structure annotations as the tested REV 22.29 code.

**Compatibility note:** Internal REV22.29 labels and `_REV22_29_RESULT.txt` generation naming are intentionally left unchanged. They are provenance of the validated generator, not evidence that the removed test commands are still present. No annotation or geometry logic was modified in this revision.

## CI build and tests

GitHub Actions runs automatically on `main` pushes and pull requests (standard `windows-latest`, .NET 8), verifying the production command and its copied-project guards, ensuring the obsolete audit commands and files are gone, testing geometry/view-helpers and compact XREF callout formatting, compiling `SPTC.SupportDrawings.dll`, and uploading `SPTC-SupportDrawings-Plant3D2025-REV22_32-PRODUCTION`.

## Recovery / historical test code

Previously validated REV22.29/22.30 source and audit code remain available in repository Git history, including the dedicated [pre-cleanup full test-history branch](https://github.com/nichslos/SPTC-SupportDrawings-Plant3D2025/tree/archive-rev22-30-full-test-history). This branch is not loaded in the production DLL.

The generator still invokes an unsupported internal Autodesk Ortho API, so a successful CI build does not replace live testing in a copied or backed-up project.
