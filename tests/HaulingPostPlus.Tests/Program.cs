using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HaulingPostPlus.Core;
using Microsoft.VisualBasic.FileIO;

int passed = 0;
void Check(string name, Action action)
{
    action();
    Console.WriteLine("PASS " + name);
    ++passed;
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; got {actual}.");
}

foreach (var (input, max, expected) in new[] {
    (int.MinValue, 1000, 1), (0, 1000, 1), (1, 1000, 1), (75, 1000, 75),
    (1000, 1000, 1000), (int.MaxValue, 1000, 1000), (500, 50, 50), (5000, 5000, 1000), (10, 0, 1) })
    Check($"clamp {input} / {max}", () => Equal(expected, WorkerCounts.Clamp(input, max)));

Check("presets include original capacity and requested expansion values", () =>
    Equal(true, WorkerCounts.Presets.SequenceEqual(new[] { 10, 50, 100, 500, 1000 })));

foreach (int count in new[] { 1, 10, 50, 75, 100, 500, 1000 })
    Check($"apply {count}", () =>
    {
        var fake = new FakeWorkplace(5, 1000);
        Equal(count, WorkerCounts.Apply(fake, count));
        Equal(Math.Abs(count - 5), fake.Calls);
    });
Check("decrease 1000 to 10", () =>
{
    var fake = new FakeWorkplace(1000, 1000);
    Equal(10, WorkerCounts.Apply(fake, 10));
    Equal(990, fake.Calls);
});
Check("same value is a no-op", () =>
{
    var fake = new FakeWorkplace(75, 1000);
    Equal(75, WorkerCounts.Apply(fake, 75));
    Equal(0, fake.Calls);
});
Check("actual lower capacity wins", () => Equal(50, WorkerCounts.Apply(new FakeWorkplace(5, 50), 1000)));
Check("blocked setter terminates", () =>
{
    var fake = new FakeWorkplace(5, 1000) { Step = 0 };
    Equal(5, WorkerCounts.Apply(fake, 1000));
    Equal(1, fake.Calls);
});
Check("wrong-direction setter terminates", () =>
{
    var fake = new FakeWorkplace(5, 1000) { Step = -1 };
    Equal(4, WorkerCounts.Apply(fake, 1000));
    Equal(1, fake.Calls);
});
Check("overshooting setter terminates", () =>
{
    var fake = new FakeWorkplace(5, 1000) { Step = 2000 };
    Equal(2005, WorkerCounts.Apply(fake, 1000));
    Equal(1, fake.Calls);
});
Check("bounded calls with abnormal initial state", () =>
{
    var fake = new FakeWorkplace(5000, 5000);
    WorkerCounts.Apply(fake, 1);
    Equal(1000, fake.Calls);
});
Check("only two native hauling templates match", () =>
{
    Equal(true, SupportedTemplates.Contains("HaulingPost.Folktails"));
    Equal(true, SupportedTemplates.Contains("HaulingPost.IronTeeth"));
    foreach (string name in new[] { "HaulingPost.Custom", "DistrictCenter.Folktails", "FarmHouse.IronTeeth", "", "haulingpost.Folktails" })
        Equal(false, SupportedTemplates.Contains(name));
});
Check("drag previews then commits once", () =>
{
    var fake = new FakeWorkplace(5, 1000);
    var draft = new SelectionDraft<FakeWorkplace>();
    draft.Select(fake);
    long token = draft.Begin(5, 1000);
    draft.Preview(500, 1000);
    Equal(5, fake.DesiredWorkers);
    Equal(true, draft.TryTake(token, out var target, out int value));
    Equal(fake, target);
    Equal(500, WorkerCounts.Apply(target!, value));
    Equal(false, draft.TryTake(token, out _, out _));
});
Check("switch cancels old release; new drag remains valid", () =>
{
    var first = new FakeWorkplace(5, 1000);
    var second = new FakeWorkplace(10, 1000);
    var draft = new SelectionDraft<FakeWorkplace>();
    draft.Select(first);
    long old = draft.Begin(5, 1000);
    draft.Preview(1000, 1000);
    draft.Select(second);
    long current = draft.Begin(10, 1000);
    draft.Preview(75, 1000);
    Equal(false, draft.TryTake(old, out _, out _));
    Equal(true, draft.TryTake(current, out var target, out int value));
    Equal(second, target);
    Equal(75, WorkerCounts.Apply(target!, value));
    Equal(5, first.DesiredWorkers);
});
Check("unselect cancels draft", () =>
{
    var draft = new SelectionDraft<object>();
    draft.Select(new object());
    long token = draft.Begin(5, 1000);
    draft.Select(null);
    Equal(false, draft.TryTake(token, out _, out _));
});
Check("Escape cancels draft", () =>
{
    var draft = new SelectionDraft<object>();
    draft.Select(new object());
    long token = draft.Begin(5, 1000);
    draft.Cancel();
    Equal(false, draft.TryTake(token, out _, out _));
});
Check("no selection cannot commit", () =>
{
    var draft = new SelectionDraft<object>();
    Equal(false, draft.TryTake(draft.Begin(50, 1000), out _, out _));
});

if (args.Length != 2)
    throw new ArgumentException("Pass repository root and game installation directory for integration preflight.");
string repo = Path.GetFullPath(args[0]);
string game = Path.GetFullPath(args[1]);
using (var localizations = ZipFile.OpenRead(Path.Combine(game, "Timberborn_Data", "StreamingAssets", "Modding", "Localizations.zip")))
    foreach (string locale in new[] { "enUS", "zhCN", "zhTW" })
        Check($"{locale} localization matches game locale, UI keys and format placeholders", () =>
        {
            Equal(true, localizations.GetEntry($"{locale}.csv") != null);
            using var csv = new TextFieldParser(Path.Combine(repo, "mod", "Localizations", $"{locale}_HaulingPostPlus.csv"), new UTF8Encoding(false, true));
            csv.SetDelimiters(",");
            csv.HasFieldsEnclosedInQuotes = true;
            Equal(true, csv.ReadFields()!.SequenceEqual(new[] { "ID", "Text", "Comment" }));
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            while (!csv.EndOfData)
            {
                string[] fields = csv.ReadFields()!;
                Equal(3, fields.Length);
                Equal(false, string.IsNullOrWhiteSpace(fields[1]));
                entries.Add(fields[0], fields[1]); // Duplicate IDs must fail.
            }
            string[] keys = { "Title", "Desired", "Status", "Preview", "Hint", "StaffingHint", "CapacityWarning" };
            Equal(true, entries.Keys.Order().SequenceEqual(keys.Select(key => "HaulingPostPlus." + key).Order()));
            foreach (var entry in entries)
            {
                string[] expected = entry.Key switch
                {
                    "HaulingPostPlus.Status" => new[] { "{0}", "{1}", "{2}" },
                    "HaulingPostPlus.Preview" => new[] { "{0}" },
                    _ => Array.Empty<string>()
                };
                var placeholders = Regex.Matches(entry.Value, @"\{\d+\}").Select(match => match.Value).Order();
                Equal(true, placeholders.SequenceEqual(expected));
                // Also reject malformed braces or unsupported argument indices.
                _ = string.Format(entry.Value, 9, 10, 1000);
            }
        });
using (var archive = ZipFile.OpenRead(Path.Combine(game, "Timberborn_Data", "StreamingAssets", "Modding", "Blueprints.zip")))
    foreach (string faction in new[] { "Folktails", "IronTeeth" })
        Check($"{faction} blueprint changes exactly two fields", () =>
        {
            string relative = $"Buildings/DistrictManagement/HaulingPost/HaulingPost.{faction}.blueprint.json";
            using var reader = new StreamReader(archive.GetEntry(relative)!.Open());
            var original = JsonNode.Parse(reader.ReadToEnd())!;
            var patch = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, "mod", relative)))!.AsObject();
            Equal(2, patch.Count);
            Equal(1, patch["WorkplaceSpec"]!.AsObject().Count);
            Equal(1, patch["EnterableSpec"]!.AsObject().Count);
            Equal(10, original["WorkplaceSpec"]!["MaxWorkers"]!.GetValue<int>());
            Equal(5, original["WorkplaceSpec"]!["DefaultWorkers"]!.GetValue<int>());
            var merged = original.DeepClone();
            foreach (var spec in patch)
                foreach (var field in spec.Value!.AsObject())
                    merged[spec.Key]![field.Key] = field.Value!.DeepClone();
            Equal(1000, merged["WorkplaceSpec"]!["MaxWorkers"]!.GetValue<int>());
            Equal(1000, merged["EnterableSpec"]!["CapacityFinished"]!.GetValue<int>());
            Equal(5, merged["WorkplaceSpec"]!["DefaultWorkers"]!.GetValue<int>());
            Equal(true, merged["EnterableSpec"]!["LimitedCapacityFinished"]!.GetValue<bool>());
            merged["WorkplaceSpec"]!["MaxWorkers"] = 10;
            merged["EnterableSpec"]!["CapacityFinished"] = 10;
            Equal(true, JsonNode.DeepEquals(original, merged));
        });

Check("manifest identity and sole Harmony dependency", () =>
{
    var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, "mod", "manifest.json")))!;
    Equal("HaulingPostPlus", manifest["Id"]!.GetValue<string>());
    var required = manifest["RequiredMods"]!.AsArray();
    Equal(1, required.Count);
    Equal("Harmony", required[0]!["Id"]!.GetValue<string>());
    Equal("2.4.1", required[0]!["MinimumVersion"]!.GetValue<string>());
});

string managed = Path.Combine(game, "Timberborn_Data", "Managed");
Check("game private UI patch targets and fields exist", () =>
{
    using var stream = File.OpenRead(Path.Combine(managed, "Timberborn.WorkSystemUI.dll"));
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var type = FindType(metadata, "Timberborn.WorkSystemUI", "WorkplaceFragment");
    var create = FindMethod(metadata, type, PortraitPanelContracts.CreateViews);
    var update = FindMethod(metadata, type, PortraitPanelContracts.UpdateViews);
    var updateFragment = FindMethod(metadata, type, PortraitPanelContracts.UpdateFragment);
    Equal(MethodAttributes.Public, updateFragment.Attributes & MethodAttributes.MemberAccessMask);
    Equal(0, updateFragment.GetParameters().Count);
    Equal(MethodAttributes.Private, create.Attributes & MethodAttributes.MemberAccessMask);
    Equal(MethodAttributes.Private, update.Attributes & MethodAttributes.MemberAccessMask);
    Equal(1, create.GetParameters().Count);
    Equal(0, update.GetParameters().Count);
    var fields = type.GetFields().Select(h => metadata.GetString(metadata.GetFieldDefinition(h).Name)).ToHashSet();
    foreach (string field in new[] { "_workplace", "_views", "_workplaceUsers" })
        Equal(true, fields.Contains(field));
});
string secondShiftDll = Path.GetFullPath(Path.Combine(game, "..", "..", "workshop", "content", "1062090", "3614598709", "version-1.1", "Scripts", "SecondShift.CoreUI.dll"));
if (File.Exists(secondShiftDll))
    Check("installed Second Shift replacement panel has all three patch contracts", () =>
    {
        using var stream = File.OpenRead(secondShiftDll);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        int split = PortraitPanelContracts.SecondShiftPanel.LastIndexOf('.');
        var type = FindType(metadata, PortraitPanelContracts.SecondShiftPanel[..split], PortraitPanelContracts.SecondShiftPanel[(split + 1)..]);
        Equal(1, FindMethod(metadata, type, PortraitPanelContracts.CreateViews).GetParameters().Count);
        Equal(0, FindMethod(metadata, type, PortraitPanelContracts.UpdateViews).GetParameters().Count);
        Equal(0, FindMethod(metadata, type, PortraitPanelContracts.UpdateFragment).GetParameters().Count);
        var fields = type.GetFields().Select(h => metadata.GetString(metadata.GetFieldDefinition(h).Name)).ToHashSet();
        foreach (string field in new[] { "_workplace", "_views", "_workplaceUsers" })
            Equal(true, fields.Contains(field));
    });
else
    Console.WriteLine("SKIP optional Second Shift binary preflight: not found in the game's Steam library (not required). Mock compatibility checks still run.");
Check("production assembly does not depend on Second Shift", () =>
{
    using var stream = File.OpenRead(Path.Combine(repo, "src", "HaulingPostPlus", "bin", "Release", "netstandard2.1", "HaulingPostPlus.dll"));
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var references = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name));
    Equal(false, references.Any(name => name.StartsWith("SecondShift.", StringComparison.Ordinal)));
});
Check("game public count adjustment and serialization contracts exist", () =>
{
    using var stream = File.OpenRead(Path.Combine(managed, "Timberborn.WorkSystem.dll"));
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var type = FindType(metadata, "Timberborn.WorkSystem", "Workplace");
    foreach (string method in new[] { "IncreaseDesiredWorkers", "DecreaseDesiredWorkers", "get_MaxWorkers", "get_DesiredWorkers", "Save", "Load" })
        Equal(MethodAttributes.Public, FindMethod(metadata, type, method).Attributes & MethodAttributes.MemberAccessMask);
});
Check("native UI templates contain required named controls", () =>
{
    using var zip = ZipFile.OpenRead(Path.Combine(game, "Timberborn_Data", "StreamingAssets", "Modding", "UI.zip"));
    Equal(true, zip.GetEntry("Views/Game/EntityPanel/WorkplaceFragment.uxml") != null);
    using var reader = new StreamReader(zip.GetEntry("Views/Common/IntegerSlider.uxml")!.Open());
    string content = reader.ReadToEnd();
    foreach (string name in new[] { "Slider", "Value", "MaxValue" })
        Equal(true, content.Contains($"name=\"{name}\"", StringComparison.Ordinal));
});
Console.WriteLine($"{passed} checks passed. Unity UI, Harmony runtime and save/load still require in-game validation.");

static TypeDefinition FindType(MetadataReader reader, string ns, string name) =>
    reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(t => reader.GetString(t.Namespace) == ns && reader.GetString(t.Name) == name);
static MethodDefinition FindMethod(MetadataReader reader, TypeDefinition type, string name) =>
    type.GetMethods().Select(reader.GetMethodDefinition).Single(m => reader.GetString(m.Name) == name);

internal sealed class FakeWorkplace(int initial, int maximum) : IWorkerCount
{
    public int DesiredWorkers { get; private set; } = initial;
    public int MaxWorkers => maximum;
    public int Calls { get; private set; }
    public int Step { get; init; } = 1;
    public void Increase() { ++Calls; DesiredWorkers += Step; }
    public void Decrease() { ++Calls; DesiredWorkers -= Step; }
}
