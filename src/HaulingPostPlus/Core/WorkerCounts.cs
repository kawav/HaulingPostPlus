using System;
using System.Collections.Generic;

namespace HaulingPostPlus.Core;

internal interface IWorkerCount
{
    int DesiredWorkers { get; }
    int MaxWorkers { get; }
    void Increase();
    void Decrease();
}

internal static class WorkerCounts
{
    public const int Capacity = 1000;
    public static IReadOnlyList<int> Presets { get; } = Array.AsReadOnly(new[] { 10, 50, 100, 500, 1000 });
    public static int Limit(int maximum) => Math.Max(1, Math.Min(Capacity, maximum));
    public static int Clamp(int value, int maximum) => Math.Max(1, Math.Min(value, Limit(maximum)));

    // Use the public game operations, preserving notifications and unassignment.
    // Progress and iteration guards prevent another mod from causing an endless loop.
    public static int Apply(IWorkerCount workplace, int requested)
    {
        int target = Clamp(requested, workplace.MaxWorkers);
        for (int i = 0; i < Capacity && workplace.DesiredWorkers != target; ++i)
        {
            int before = workplace.DesiredWorkers;
            if (before < target)
                workplace.Increase();
            else
                workplace.Decrease();
            int after = workplace.DesiredWorkers;
            if ((before < target && (after <= before || after > target)) ||
                (before > target && (after >= before || after < target)))
                break;
        }
        return workplace.DesiredWorkers;
    }
}
