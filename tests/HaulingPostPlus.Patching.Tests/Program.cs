using HaulingPostPlus;
using HaulingPostPlus.Patching.Tests;
using Timberborn.WorkSystem;
using UnityEngine.UIElements;

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

var panels = new List<MockPanel> { new Timberborn.WorkSystemUI.WorkplaceFragment() };
#if SECOND_SHIFT_STUB
panels.Add(new SecondShift.CoreUI.TwoShiftsWorkplaceFragment());
#endif
Check("install with optional panel present/absent", () =>
{
    WorkplacePortraitPatch.Install();
    Equal(panels.Count, WorkplacePortraitPatch.InstalledPanelCount);
});
Check("repeat installation is idempotent", () =>
{
    WorkplacePortraitPatch.Install();
    Equal(panels.Count, WorkplacePortraitPatch.InstalledPanelCount);
});
foreach (var panel in panels)
{
    string name = panel.GetType().FullName!;
    foreach (string faction in new[] { "Folktails", "IronTeeth" })
        Check($"{name}: {faction} hides 1000 portraits and retains count", () =>
        {
            var workplace = new Workplace("HaulingPost." + faction, 1000) { DesiredWorkers = 1000 };
            panel.Select(workplace);
            Equal(0, panel.ViewCount);
            Equal(0, panel.ChildCount);
            Equal(0, panel.Allocations);
            Equal(DisplayStyle.None, panel.Display);
            for (int frame = 0; frame < 100; ++frame) panel.UpdateFragment();
            Equal("0 / 1000", panel.CountText);
            Equal(DisplayStyle.None, panel.Display);
            Equal(0, panel.OriginalCreateCalls);
            Equal(0, panel.OriginalUpdateCalls);
            workplace.Enabled = false;
            panel.UpdateFragment();
            Equal(DisplayStyle.None, panel.Display);
        });
    Check($"{name}: normal building restores its portraits", () =>
    {
        panel.Select(new Workplace("FarmHouse.Folktails", 10));
        panel.UpdateFragment();
        Equal(10, panel.ViewCount);
        Equal(10, panel.ChildCount);
        Equal(DisplayStyle.Flex, panel.Display);
        Equal("0 / 5", panel.CountText);
        Equal(1, panel.OriginalCreateCalls);
        Equal(1, panel.OriginalUpdateCalls);
    });
    Check($"{name}: returning to hauling clears old portraits", () =>
    {
        int allocations = panel.Allocations;
        panel.Select(new Workplace("HaulingPost.IronTeeth", 1000) { DesiredWorkers = 500 });
        panel.UpdateFragment();
        Equal(0, panel.ViewCount);
        Equal(0, panel.ChildCount);
        Equal(DisplayStyle.None, panel.Display);
        Equal(allocations, panel.Allocations);
        Equal("0 / 500", panel.CountText);
    });
}
Console.WriteLine($"{passed} Harmony smoke checks passed against mock panels ({panels.Count} panel type(s)). This does NOT replace Unity/Mono in-game testing.");
