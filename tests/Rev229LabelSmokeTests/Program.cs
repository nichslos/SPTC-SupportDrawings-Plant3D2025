using System;
using SPTC.SupportDrawings;

static void Verify(bool ok,string message)
{
    if(!ok)throw new Exception(message);
}
string label=SupportOrthoPipeCallout29.Compose(
    "TEST-001","BOP EL. +3899.9543 mm");
Verify(label=="TEST-001\nBOP EL. +3899.9543 mm",
    "REV22.38 output must have two lines without LINE NO prefix");
Verify(!label.Contains("XREF:",StringComparison.Ordinal),
    "Drawing text must not expose internal XREF path");
Verify(SupportOrthoPipeCallout29.Compose(null,null)==
    "Not available\nBOP: N/A",
    "Unavailable fields must not be fabricated");
Verify(SupportOrthoPipeCallout29.Compose("A\r\n-01","BOP EL. +4 mm")==
    "A  -01\nBOP EL. +4 mm",
    "Property text must never introduce extra DBText rows");
Verify(!label.StartsWith("LINE NO:",StringComparison.Ordinal),
    "The LINE NO prefix must not appear on the drawing");
Console.WriteLine("REV22.38 simplified pipe callout smoke tests PASS");
