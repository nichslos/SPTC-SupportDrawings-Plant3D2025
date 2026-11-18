using System;
using System.Collections.Generic;
using SPTC.SupportDrawings;

internal static class Rev88OutputAppearanceTests
{
    private const string H = NativeOrthoOutputAppearance.HiddenVariable;
    private const string I = NativeOrthoOutputAppearance.InsulationVariable;

    private sealed class Host
    {
        internal readonly Dictionary<string, int> Values = new() { [H] = 0, [I] = 0 };
        internal readonly List<string> Accesses = new();
        internal readonly List<string> Log = new();
        internal Action<string,int>? OnWrite;

        internal NativeOrthoOutputAppearance Start(string hidden, string insulation)
        {
            // Deliberately non-consecutive. A combo index is not a native enum lookup.
            return new NativeOrthoOutputAppearance(hidden, insulation, name => name switch
            {
                "NoHiddenLines" => 17, "HiddenLinePiping" => 3, "AllHiddenLines" => 42,
                _ => throw new ArgumentException(name)
            }, key => { Accesses.Add(key); return Values[key]; },
            (key, value) =>
            {
                Accesses.Add(key);
                if (OnWrite == null) Values[key] = value;
                else OnWrite(key, value);
            }, Log.Add);
        }
    }

    internal static void Run()
    {
        var hidden = new[] { ("No Hidden Lines", 17), ("Hidden Line Piping", 3), ("All Hidden Lines", 42) };
        var insulation = new[] { ("No Insulation", 0), ("Solid Line Insulation", 1), ("Hidden Line Insulation", 2) };
        foreach (var (h, expectedH) in hidden)
        foreach (var (i, expectedI) in insulation)
        {
            var host = new Host();
            using (var run = host.Start(h, i))
            {
                foreach (string view in new[] { "TOP", "NW" })
                {
                    host.Values[H] = -1;
                    host.Values[I] = -1;
                    int savedH = -1;
                    run.Generate(view, () =>
                    {
                        Need(host.Values[H] == expectedH && host.Values[I] == expectedI,
                            "Both native inputs must match at projection for " + h + " / " + i);
                        savedH = host.Values[H];
                    }, _ => savedH);
                }
                Need(run.Complete(2), "Both views verified");
                Need(host.Log.Exists(x => x.Contains("TOP") && x.Contains("SAVED_VIEW_MATCH")) &&
                     host.Log.Exists(x => x.Contains("NW") && x.Contains("GENERATION_INPUT_MATCH")),
                    "View and input readbacks distinguished");
            }
            Need(host.Values[H] == expectedH && host.Values[I] == expectedI, "Native ribbon on success");
        }

        const string projectDefault = NativeOrthoAppearance83.ProjectDefault;
        foreach (var h in new[] { projectDefault, "All Hidden Lines" })
        foreach (var i in new[] { projectDefault, "Hidden Line Insulation" })
        {
            var host = new Host();
            using var run = host.Start(h, i);
            bool readView = false;
            run.Generate("TOP", () => { }, _ => { readView = true; return host.Values[H]; });
            Need(run.Complete(1), "Default and explicit choices compose");
            Need(host.Accesses.Contains(H) == (h != projectDefault) &&
                 host.Accesses.Contains(I) == (i != projectDefault) &&
                 readView == (h != projectDefault), "Default leaves that channel alone");
        }

        foreach (string failed in new[] { H, I })
        {
            var host = new Host();
            host.OnWrite = (key, value) =>
            {
                host.Values[key] = value;
                if (key == failed && value != 0) throw new InvalidOperationException("Setter failed after writing");
            };
            using var run = host.Start("All Hidden Lines", "Hidden Line Insulation");
            int count = 0;
            run.Generate("TOP", () =>
            {
                count++;
                Need(host.Values[failed] == 0, "Partial write restored before rendering");
                Need(host.Values[failed == H ? I : H] != 0, "Other channel reached renderer");
            }, _ => host.Values[H]);
            Need(count == 1 && !run.Complete(1), "Independent failure does not block or pass the drawing");
        }

        var mismatch = new Host();
        using (var run = mismatch.Start("All Hidden Lines", "Solid Line Insulation"))
        {
            run.Generate("TOP", () => { }, _ => 17);
            Need(!run.Complete(1), "Matching host cannot hide a wrong saved view");
        }
        Need(mismatch.Values[H] == 0 && mismatch.Values[I] == 1, "Only verified channel retained");

        var ignored = new Host();
        ignored.OnWrite = (key, value) => { if (key == I) ignored.Values[key] = value; };
        using (var run = ignored.Start("All Hidden Lines", "Solid Line Insulation"))
        {
            bool rendered = false;
            run.Generate("TOP", () => rendered = true, _ => throw new Exception("Unexpected view check"));
            Need(rendered && !run.Complete(1) && ignored.Values[I] == 1,
                "Silent setter failure is caught before projection and cannot suppress insulation");
        }

        var failedGen = new Host();
        try
        {
            using var run = failedGen.Start("All Hidden Lines", "Solid Line Insulation");
            run.Generate("TOP", () => throw new OperationCanceledException(), _ => 42);
            throw new Exception("Renderer failure swallowed");
        }
        catch (OperationCanceledException) { }
        Need(failedGen.Values[H] == 0 && failedGen.Values[I] == 0, "Canceled output restores both inputs");
        Console.WriteLine("REV88 appearance: all 9 native-input combinations, defaults, per-view readback and recovery PASS");
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new Exception("REV88: " + message);
    }
}
