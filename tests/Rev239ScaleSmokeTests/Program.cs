using SPTC.SupportDrawings;
using System;
using System.Collections.Generic;

static void Assert(bool condition,string message)
{
    if(!condition)throw new Exception("REV22.39 scale FAIL: "+message);
}
Assert(SupportDrawingPaperScale39.Resolve("Automatic",25)==0,
    "Automatic must retain unchanged autofit");
foreach(var preset in new Dictionary<string,int>
{
    ["1:10"]=10,["1:20"]=20,["1:30"]=30,["1:40"]=40,
    ["1:50"]=50,["1:75"]=75,["1:100"]=100,["1:150"]=150,
    ["1:200"]=200,["1:250"]=250,["1:500"]=500
})
    Assert(SupportDrawingPaperScale39.Resolve(preset.Key,999)==preset.Value,
        "preset "+preset.Key+" must ignore custom field");

foreach(int denominator in new[]{1,12,25,37,45,65,125,333,10000,1000000})
{
    int scale=SupportDrawingPaperScale39.Resolve(
        SupportDrawingPaperScale39.Custom,denominator);
    Assert(scale==denominator,"custom 1:"+denominator+" must be exact");
    Assert(SupportDrawingPaperScale39.Display(scale)=="1:"+denominator,
        "custom display 1:"+denominator);
}
Assert(SupportDrawingPaperScale39.Display(0)=="Automatic",
    "autofit display");
foreach(var invalid in new[]{0,-1,1000001,int.MaxValue})
{
    bool rejected=false;
    try
    {
        SupportDrawingPaperScale39.Resolve(
            SupportDrawingPaperScale39.Custom,invalid);
    }
    catch(ArgumentOutOfRangeException){rejected=true;}
    Assert(rejected,"reject invalid custom denominator "+invalid);
}
foreach(var bad in new[]{"", "Custom","1:0","1:-25","1:abc","1:12.5"})
{
    bool rejected=false;
    try{SupportDrawingPaperScale39.Resolve(bad,25);}
    catch(ArgumentException){rejected=true;}
    Assert(rejected,"reject invalid scale selector "+bad);
}
Console.WriteLine("REV22.39 custom 1:N and automatic/preset compatibility PASS");
