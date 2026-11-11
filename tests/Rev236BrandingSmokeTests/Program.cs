using System;
using System.Drawing;
using SPTC.SupportDrawings;

using(Bitmap logo=SupportBranding36.CreateLogo())
{
    if(logo.Width!=260 || logo.Height!=165)
        throw new InvalidOperationException("SPTC wordmark PNG size mismatch");
    if(logo.GetPixel(0,0).A!=0)
        throw new InvalidOperationException("SPTC wordmark transparency missing");
}
using(Icon icon=SupportBranding36.CreateIcon())
{
    if(icon.Width!=32 || icon.Height!=32)
        throw new InvalidOperationException("SPTC icon 32x32 conversion failed");
}
using(var picture=SupportBranding36.MakeLogo(0,0,184,118))
{
    if(picture.Image==null || picture.Width!=184 ||
       picture.AccessibleName!="SPTC Engineering logo")
        throw new InvalidOperationException("SPTC branded WinForms PictureBox invalid");
    picture.Image.Dispose();
}
Console.WriteLine("REV22.36 embedded SPTC logo+icon WinForms smoke PASS");
