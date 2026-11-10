using System;
using SPTC.SupportDrawings;

static void Verify(bool ok,string message)
{
    if(!ok)throw new Exception(message);
}
string label=SupportOrthoPipeCallout29.Compose(
    "TEST-001","BOP EL. +3899.9543 mm");
Verify(label=="LINE NO: TEST-001\nBOP EL. +3899.9543 mm",
    "REV22.29 output must have exactly two short lines");
Verify(!label.Contains("XREF:",StringComparison.Ordinal),
    "Drawing text must not expose internal XREF path");
Verify(SupportOrthoPipeCallout29.Compose(null,null)==
    "LINE NO: Not available\nBOP: N/A",
    "Unavailable fields must not be fabricated");
Verify(SupportOrthoPipeCallout29.Compose("A\r\n-01","BOP EL. +4 mm")==
    "LINE NO: A  -01\nBOP EL. +4 mm",
    "Property text must never introduce extra DBText rows");
Console.WriteLine("REV22.29 compact pipe callout smoke tests PASS");
