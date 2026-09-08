using System;
using System.Collections.Generic;
using HaulingPostPlus.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using Timberborn.WorkSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace HaulingPostPlus;

internal sealed class HaulingPostFragment : IEntityPanelFragment
{
    private readonly VisualElementLoader _loader;
    private readonly VisualElementInitializer _initializer;
    private readonly ILoc _loc;
    private readonly SelectionDraft<Workplace> _selection = new SelectionDraft<Workplace>();
    private readonly List<(int Count, Button Button)> _presets = new List<(int, Button)>();
    private VisualElement _root = null!;
    private VisualElement _controls = null!;
    private Slider _slider = null!;
    private IntegerField _number = null!;
    private Label _status = null!;
    private Label _preview = null!;
    private Label _maximum = null!;
    private Label _warning = null!;
    private bool _resetting;
    private bool _numberFocused;
    private bool _ignoreSliderChanges;
    private int _activePointerId = -1;
    private long _dragToken;
    private int _lastAssigned = -1;
    private int _lastDesired = -1;
    private int _lastMaximum = -1;

    public HaulingPostFragment(VisualElementLoader loader, VisualElementInitializer initializer, ILoc loc)
    {
        _loader = loader;
        _initializer = initializer;
        _loc = loc;
    }

    public VisualElement InitializeFragment()
    {
        // Retain the game's frame and style sheets; no copied game assets or AssetBundle.
        _root = _loader.LoadVisualElement("Game/EntityPanel/WorkplaceFragment");
        _root.Clear();
        _root.name = "HaulingPostPlusFragment";
        _root.Add(MakeLabel("Title", "entity-panel__heading-text"));
        _status = MakeLabel("", "entity-panel__text");
        _status.style.marginTop = 4;
        _root.Add(_status);
        _controls = new VisualElement();
        _controls.style.marginTop = 6;
        _root.Add(_controls);
        _controls.Add(MakeLabel("Desired", "entity-panel__text"));

        var integerSlider = _loader.LoadVisualElement("Common/IntegerSlider");
        _slider = integerSlider.Q<Slider>("Slider") ?? throw new MissingMemberException("Native Slider missing.");
        _number = integerSlider.Q<IntegerField>("Value") ?? throw new MissingMemberException("Native Value field missing.");
        _maximum = integerSlider.Q<Label>("MaxValue");
        _slider.lowValue = 1;
        _slider.highValue = WorkerCounts.Capacity;
        _slider.pageSize = 0;
        _slider.style.minWidth = 80;
        _slider.style.flexGrow = 1;
        _slider.style.flexShrink = 1;
        _number.isDelayed = true;
        _number.Q<TextElement>().style.width = 38;
        integerSlider.style.marginTop = 3;
        integerSlider.style.marginBottom = 3;
        _controls.Add(integerSlider);

        _slider.RegisterCallback<PointerDownEvent>(OnSliderPointerDown, TrickleDown.TrickleDown);
        _slider.RegisterValueChangedCallback(OnSliderChanged);
        _slider.RegisterCallback<PointerUpEvent>(_ => ScheduleDragCommit(), TrickleDown.TrickleDown);
        _slider.RegisterCallback<PointerCaptureOutEvent>(_ => ScheduleDragCommit());
        _slider.RegisterCallback<PointerCancelEvent>(_ => CancelDraft());
        _slider.RegisterCallback<KeyDownEvent>(OnSliderKeyDown, TrickleDown.TrickleDown);
        _number.RegisterCallback<FocusInEvent>(_ => { _numberFocused = true; CancelDraft(); });
        _number.RegisterCallback<FocusOutEvent>(_ => _numberFocused = false);
        _number.RegisterValueChangedCallback(evt =>
        {
            if (!_resetting)
                Commit(_selection.Selected, evt.newValue);
        });

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.marginTop = 5;
        foreach (int count in WorkerCounts.Presets)
        {
            int preset = count;
            var button = new Button(() => Commit(_selection.Selected, preset)) { text = count.ToString() };
            button.AddToClassList("game-text-normal");
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.minWidth = 0;
            button.style.height = 27;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.color = new Color(0.92f, 0.90f, 0.74f);
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 3;
            _initializer.InitializeVisualElement(button);
            _presets.Add((count, button));
            buttons.Add(button);
        }
        _controls.Add(buttons);
        _preview = MakeLabel("", "entity-panel__sub-text");
        _preview.style.marginTop = 4;
        _root.Add(_preview);
        var hint = MakeLabel("Hint", "entity-panel__sub-text");
        hint.style.marginTop = 4;
        _root.Add(hint);
        var staffing = MakeLabel("StaffingHint", "entity-panel__sub-text");
        staffing.style.marginTop = 4;
        _root.Add(staffing);
        _warning = MakeLabel("CapacityWarning", "entity-panel__sub-text");
        _warning.style.color = new Color(1, 0.75f, 0.3f);
        _root.Add(_warning);
        _root.style.display = DisplayStyle.None;
        return _root;
    }

    public void ShowFragment(BaseComponent entity)
    {
        // ShowFragment can run without ClearFragment when switching directly.
        ClearFragment();
        Workplace? workplace = entity.GetComponent<Workplace>();
        if (!HaulingPosts.IsSupported(workplace))
            return;
        _selection.Select(workplace);
        _root.style.display = DisplayStyle.Flex;
        UpdateFragment();
    }

    public void ClearFragment()
    {
        _resetting = true;
        _selection.Select(null);
        AbortPointerGesture();
        // Ensure the game's native text-input blocker receives FocusOut.
        if (_numberFocused)
            (_number.panel?.focusController?.focusedElement as VisualElement)?.Blur();
        _numberFocused = false;
        _lastAssigned = _lastDesired = _lastMaximum = -1;
        _root.style.display = DisplayStyle.None;
        _resetting = false;
    }

    public void UpdateFragment()
    {
        var workplace = _selection.Selected;
        if (!workplace)
        {
            if (_root.style.display != DisplayStyle.None)
                ClearFragment();
            return;
        }
        int desired = workplace!.DesiredWorkers;
        int maximum = workplace.MaxWorkers;
        int assigned = workplace.NumberOfAssignedWorkers;
        if (_lastAssigned != assigned || _lastDesired != desired || _lastMaximum != maximum)
        {
            _status.text = _loc.T("HaulingPostPlus.Status", assigned, desired, maximum);
            _lastAssigned = assigned;
            _lastDesired = desired;
            _lastMaximum = maximum;
        }
        _controls.SetEnabled(maximum >= 1);
        _warning.style.display = maximum == WorkerCounts.Capacity ? DisplayStyle.None : DisplayStyle.Flex;
        _maximum.text = WorkerCounts.Limit(maximum).ToString();
        _slider.highValue = WorkerCounts.Limit(maximum);
        if (!_selection.HasDraft && !_numberFocused)
            SynchronizeInputs(desired, maximum);
        foreach (var preset in _presets)
        {
            preset.Button.SetEnabled(preset.Count <= maximum);
            preset.Button.style.backgroundColor = desired == preset.Count
                ? new Color(0.34f, 0.47f, 0.26f) : new Color(0.15f, 0.27f, 0.22f);
        }
        UpdatePreview();
    }

    private Label MakeLabel(string key, string styleClass)
    {
        var label = new Label(key.Length == 0 ? "" : _loc.T("HaulingPostPlus." + key));
        label.AddToClassList(styleClass);
        label.style.whiteSpace = WhiteSpace.Normal;
        _initializer.InitializeVisualElement(label);
        return label;
    }

    private void OnSliderPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || !_selection.Selected)
            return;
        var workplace = _selection.Selected!;
        _ignoreSliderChanges = false;
        _activePointerId = evt.pointerId;
        _dragToken = _selection.Begin(workplace.DesiredWorkers, workplace.MaxWorkers);
    }

    private void OnSliderChanged(ChangeEvent<float> evt)
    {
        var workplace = _selection.Selected;
        if (_resetting || _ignoreSliderChanges || !workplace)
            return;
        int value = WorkerCounts.Clamp(Mathf.RoundToInt(evt.newValue), workplace!.MaxWorkers);
        if (_selection.HasDraft)
        {
            _selection.Preview(value, workplace.MaxWorkers);
            _number.SetValueWithoutNotify(value);
            UpdatePreview();
        }
        else
            Commit(workplace, value); // Keyboard and controller navigation.
    }

    private void ScheduleDragCommit()
    {
        if (!_selection.HasDraft)
            return;
        long token = _dragToken;
        // Execute after the native slider's final pointer processing.
        _root.schedule.Execute(() =>
        {
            if (_selection.TryTake(token, out Workplace? workplace, out int value))
            {
                _activePointerId = -1;
                Commit(workplace, value);
            }
        });
    }

    private void OnSliderKeyDown(KeyDownEvent evt)
    {
        _ignoreSliderChanges = false;
        if (evt.keyCode == KeyCode.Escape)
        {
            CancelDraft();
            evt.StopPropagation();
        }
    }

    private void CancelDraft()
    {
        _selection.Cancel();
        AbortPointerGesture();
        if (_selection.Selected)
            SynchronizeInputs(_selection.Selected!.DesiredWorkers, _selection.Selected.MaxWorkers);
        if (_preview != null)
            UpdatePreview();
    }

    private void AbortPointerGesture()
    {
        _ignoreSliderChanges = true;
        if (_activePointerId >= 0 && _slider?.panel != null)
        {
            var capturer = _slider.panel.GetCapturingElement(_activePointerId) as VisualElement;
            if (capturer != null && (ReferenceEquals(capturer, _slider) || _slider.Contains(capturer)))
                capturer.ReleasePointer(_activePointerId);
        }
        _activePointerId = -1;
    }

    private void Commit(Workplace? workplace, int requested)
    {
        if (_resetting || !workplace || !ReferenceEquals(workplace, _selection.Selected))
            return;
        _selection.Cancel();
        int before = workplace!.DesiredWorkers;
        int result = WorkerCounts.Apply(new WorkplaceCountAdapter(workplace), requested);
        int expected = WorkerCounts.Clamp(requested, workplace.MaxWorkers);
        if (result != expected)
            Debug.LogWarning($"[HaulingPostPlus] Desired-worker update stopped at {result}; requested {expected}. Check conflicting mods.");
        else if (before != result)
            Debug.Log($"[HaulingPostPlus] {workplace.GetComponent<Timberborn.TemplateSystem.TemplateSpec>().TemplateName}: desired workers {before} -> {result}.");
        SynchronizeInputs(result, workplace.MaxWorkers);
        UpdateFragment();
    }

    private void SynchronizeInputs(int value, int maximum)
    {
        _resetting = true;
        int clamped = WorkerCounts.Clamp(value, maximum);
        _slider.SetValueWithoutNotify(clamped);
        _number.SetValueWithoutNotify(clamped);
        _resetting = false;
    }

    private void UpdatePreview()
    {
        _preview.style.display = _selection.HasDraft ? DisplayStyle.Flex : DisplayStyle.None;
        if (_selection.HasDraft)
            _preview.text = _loc.T("HaulingPostPlus.Preview", _selection.Value);
    }
}
