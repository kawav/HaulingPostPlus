using System.Runtime.CompilerServices;
using HaulingPostPlus.Core;

// These are deliberately minimal test doubles, NOT decompiled game code.
namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
    }
}

namespace UnityEngine.UIElements
{
    public enum DisplayStyle { Flex, None }
    public sealed class MockStyle { public DisplayStyle display { get; set; } }
    public sealed class VisualElement
    {
        public MockStyle style { get; } = new();
        public int ChildCount { get; set; }
        public void Clear() => ChildCount = 0;
    }
}

namespace Timberborn.WorkSystem
{
    public sealed class Workplace(string templateName, int maximum)
    {
        public string TemplateName { get; } = templateName;
        public int MaxWorkers { get; } = maximum;
        public int DesiredWorkers { get; set; } = 5;
        public bool Enabled { get; set; } = true;
    }
    public sealed class WorkplaceWorkerType { }
}

namespace HaulingPostPlus
{
    internal static class HaulingPosts
    {
        public static bool IsSupported(Timberborn.WorkSystem.Workplace? workplace) =>
            workplace != null && SupportedTemplates.Contains(workplace.TemplateName);
    }
}

namespace HaulingPostPlus.Patching.Tests
{
    public abstract class MockPanel
    {
        // Same field contract used by the production Harmony prefixes/postfix.
        protected Timberborn.WorkSystem.Workplace _workplace = null!;
        protected readonly UnityEngine.UIElements.VisualElement _workplaceUsers = new();
        protected readonly List<object> _views = new();
        public int Allocations { get; private set; }
        public int OriginalCreateCalls { get; private set; }
        public int OriginalUpdateCalls { get; private set; }
        public int ViewCount => _views.Count;
        public int ChildCount => _workplaceUsers.ChildCount;
        public UnityEngine.UIElements.DisplayStyle Display => _workplaceUsers.style.display;
        public string CountText { get; private set; } = "";

        public abstract void Select(Timberborn.WorkSystem.Workplace workplace);
        public abstract void UpdateFragment();

        protected void SimulateCreate()
        {
            ++OriginalCreateCalls;
            _views.Clear();
            for (int i = 0; i < _workplace.MaxWorkers; ++i)
                _views.Add(new object());
            Allocations += _views.Count;
            _workplaceUsers.ChildCount = _views.Count;
        }

        protected void SimulateUpdate()
        {
            ++OriginalUpdateCalls;
            // Like the real updater, this requires a fully allocated list.
            for (int i = 0; i < _workplace.DesiredWorkers; ++i)
                _ = _views[i];
        }

        protected void SimulateFrameDisplay()
        {
            CountText = $"0 / {_workplace.DesiredWorkers}";
            _workplaceUsers.style.display = _workplace.Enabled
                ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
        }
    }
}

namespace Timberborn.WorkSystemUI
{
    public sealed class WorkplaceFragment : HaulingPostPlus.Patching.Tests.MockPanel
    {
        public override void Select(WorkSystem.Workplace workplace)
        {
            _workplace = workplace;
            AddEmptyViews(new WorkSystem.WorkplaceWorkerType());
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddEmptyViews(WorkSystem.WorkplaceWorkerType _) => SimulateCreate();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateViews() => SimulateUpdate();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void UpdateFragment()
        {
            if (_workplace.Enabled) UpdateViews();
            SimulateFrameDisplay();
        }
    }
}

#if SECOND_SHIFT_STUB
namespace SecondShift.CoreUI
{
    // A separate panel type is essential: the bug came from patching only the
    // native panel while a replacement panel had its own allocation/update path.
    public sealed class TwoShiftsWorkplaceFragment : HaulingPostPlus.Patching.Tests.MockPanel
    {
        public override void Select(Timberborn.WorkSystem.Workplace workplace)
        {
            _workplace = workplace;
            AddEmptyViews(new Timberborn.WorkSystem.WorkplaceWorkerType());
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddEmptyViews(Timberborn.WorkSystem.WorkplaceWorkerType _) => SimulateCreate();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateViews() => SimulateUpdate();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void UpdateFragment()
        {
            if (_workplace.Enabled) UpdateViews();
            SimulateFrameDisplay();
        }
    }
}
#endif
