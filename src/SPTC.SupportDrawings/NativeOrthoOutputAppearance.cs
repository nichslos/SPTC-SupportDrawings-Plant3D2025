using System;

namespace SPTC.SupportDrawings;

/// <summary>Applies the native generator inputs and checks each completed view.</summary>
internal sealed class NativeOrthoOutputAppearance : IDisposable
{
    internal const string HiddenVariable = "PLANTORTHOHIDDENLINEMODE";
    internal const string InsulationVariable = "PLANTORTHOINSULATIONMODE";

    private readonly Setting hidden;
    private readonly Setting insulation;
    private readonly Func<string, int> read;

    internal NativeOrthoOutputAppearance(string hiddenSelection, string insulationSelection,
        Func<string, int> resolveHidden, Func<string, int> read, Action<string, int> write,
        Action<string> log)
    {
        this.read = read;
        hidden = new Setting(HiddenVariable, NativeOrthoAppearance83.ValidateHidden(hiddenSelection),
            () => resolveHidden(NativeOrthoAppearance83.HiddenEnumName(hiddenSelection)!),
            read, write, log);
        insulation = new Setting(InsulationVariable, NativeOrthoAppearance83.ValidateInsulation(insulationSelection),
            () => NativeOrthoAppearance83.InsulationValue(insulationSelection)!.Value,
            read, write, log);
    }

    internal void Generate(string view, Action generate, Func<int, int> checkSavedHidden)
    {
        bool hiddenReady = hidden.Prepare(view);
        bool insulationReady = insulation.Prepare(view);
        generate();
        if (hiddenReady)
            hidden.Check(view, "SAVED_VIEW_MATCH", checkSavedHidden);
        if (insulationReady)
            insulation.Check(view, "GENERATION_INPUT_MATCH", _ => read(InsulationVariable));
    }

    internal bool Complete(int viewCount)
    {
        bool hiddenOk = hidden.Complete(viewCount);
        bool insulationOk = insulation.Complete(viewCount);
        return hiddenOk && insulationOk;
    }

    public void Dispose()
    {
        insulation.Restore();
        hidden.Restore();
    }

    private sealed class Setting
    {
        private readonly string variable, selection;
        private readonly Func<int> resolve;
        private readonly Func<string, int> read;
        private readonly Action<string, int> write;
        private readonly Action<string> log;
        private int? original, requested;
        private bool changed, keep;
        private int matched;

        internal Setting(string variable, string selection, Func<int> resolve,
            Func<string, int> read, Action<string, int> write, Action<string> log)
        {
            this.variable = variable;
            this.selection = selection;
            this.resolve = resolve;
            this.read = read;
            this.write = write;
            this.log = log;
        }

        internal bool Prepare(string view)
        {
            if (selection == NativeOrthoAppearance83.ProjectDefault) return false;
            try
            {
                int value = requested ??= resolve();
                int before = read(variable);
                original ??= before;
                if (before != value)
                {
                    // Account for a setter that changes the host and then throws.
                    changed = true;
                    write(variable, value);
                    Require(value, read(variable));
                }
                return true;
            }
            catch (Exception ex)
            {
                Report(view, "INPUT_FAILED " + ex.GetBaseException().Message);
                Restore();
                return false;
            }
        }

        internal void Check(string view, string status, Func<int, int> readResult)
        {
            try
            {
                int expected = requested!.Value;
                int actual = readResult(expected);
                Require(expected, actual);
                matched++;
                Report(view, status + " VALUE=" + actual);
            }
            catch (Exception ex)
            {
                Report(view, "CHECK_FAILED " + ex.GetBaseException().Message);
            }
        }

        internal bool Complete(int viewCount)
        {
            if (selection == NativeOrthoAppearance83.ProjectDefault)
            {
                Report("SUMMARY", "PLANT_DEFAULT");
                return true;
            }
            keep = viewCount > 0 && matched == viewCount;
            Report("SUMMARY", "MATCHED=" + matched + "/" + viewCount);
            return keep;
        }

        internal void Restore()
        {
            if (keep || !changed || !original.HasValue) return;
            try
            {
                write(variable, original.Value);
                Require(original.Value, read(variable));
                changed = false;
            }
            catch (Exception ex)
            {
                Report("RESTORE", "FAILED " + ex.GetBaseException().Message);
            }
        }

        private void Report(string view, string message) =>
            log("ORTHO_APPEARANCE " + view + " " + variable + " [" + selection + "] " + message);

        private static void Require(int expected, int actual)
        {
            if (actual != expected)
                throw new InvalidOperationException("Expected " + expected + ", read " + actual + ".");
        }
    }
}
