#:project ../src/SignalSafe.Excel/SignalSafe.Excel.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property PublishAot=false
using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;
using SignalSafe.Core.Layouts;
using SignalSafe.Excel;

// Run from the repository root: dotnet run tools/make-samples.cs
var dir = args.Length > 0 ? args[0] : "samples";

// Westbrook: drafted tables with five planted mistakes, matching the prototype.
var w = ControlTableBuilder.Draft(LayoutLoader.FromFile($"{dir}/westbrook-junction.json")).ToList();
w = w.Where(r => r.RouteId != "S7(S)-S6").ToList();                                     // R01 missing route
w = w.Select(r => r.RouteId switch
{
    "S1(M)-S3" => r with { ConflictingRoutes = [] },                                    // R05 conflict not listed
    "S1(M)-S5" => r with { PointsNormal = ["P1"], PointsReverse = [], ConflictingRoutes = [] }, // R03 wrong position (+R05)
    "S2(M)-S4" => r with { Overlap = [] },                                              // R07 overlap missing
    "S4(M)-S6" => r with { SectionsClear = ["T21", "T10"] },                            // R10 unknown T21, R04 T12 missing
    _ => r,
}).ToList();
ControlTableExcel.Write($"{dir}/westbrook-control-tables.xlsx", w,
    "Westbrook Junction control tables, revision B (invented example with deliberate mistakes)");

// Kestrel: one crossover mistake.
var k = ControlTableBuilder.Draft(LayoutLoader.FromFile($"{dir}/kestrel-lane-crossover.json")).ToList();
k = k.Select(r => r.RouteId == "S31(M)-S33" ? r with { PointsNormal = ["P11"] } : r).ToList(); // R09 crossover end missing
ControlTableExcel.Write($"{dir}/kestrel-control-tables.xlsx", k,
    "Kestrel Lane Crossover control tables (invented example with a deliberate mistake)");

ControlTableExcel.Write($"{dir}/control-table-template.xlsx", [], "SignalSafe control table template");
Console.WriteLine("ok");
