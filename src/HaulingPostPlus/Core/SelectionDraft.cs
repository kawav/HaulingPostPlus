namespace HaulingPostPlus.Core;

// A release callback may run after the panel switches buildings. A generation
// token makes stale callbacks harmless, including duplicate capture/up events.
internal sealed class SelectionDraft<T> where T : class
{
    public T? Selected { get; private set; }
    public bool HasDraft { get; private set; }
    public int Value { get; private set; }
    private long _generation;

    public void Select(T? target)
    {
        Cancel();
        Selected = target;
    }

    public long Begin(int value, int maximum)
    {
        Cancel();
        HasDraft = Selected != null;
        Value = WorkerCounts.Clamp(value, maximum);
        return _generation;
    }

    public void Preview(int value, int maximum)
    {
        if (HasDraft)
            Value = WorkerCounts.Clamp(value, maximum);
    }

    public bool TryTake(long generation, out T? target, out int value)
    {
        target = Selected;
        value = Value;
        if (!HasDraft || generation != _generation || target == null)
            return false;
        Cancel();
        return true;
    }

    public void Cancel()
    {
        HasDraft = false;
        ++_generation;
    }
}
