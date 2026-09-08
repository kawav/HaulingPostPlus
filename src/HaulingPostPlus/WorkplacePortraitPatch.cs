using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HaulingPostPlus.Core;
using HarmonyLib;
using Timberborn.WorkSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace HaulingPostPlus;

internal static class WorkplacePortraitPatch
{
    private const string HarmonyId = "HaulingPostPlus.WorkplacePortraits";
    private static readonly HashSet<Type> InstalledPanels = new HashSet<Type>();
    private static readonly HashSet<Type> ReportedPanels = new HashSet<Type>();

    public static int InstalledPanelCount => InstalledPanels.Count;

    public static void Install()
    {
        var nativePanel = AccessTools.TypeByName(PortraitPanelContracts.NativePanel)
            ?? throw new MissingMemberException("HaulingPostPlus: WorkplaceFragment is unavailable.");
        InstallForPanel(nativePanel);

        // Second Shift disables the original panel and registers its own one,
        // even when the selected building's Two shifts checkbox is unchecked.
        var secondShiftPanel = AccessTools.TypeByName(PortraitPanelContracts.SecondShiftPanel);
        if (secondShiftPanel != null)
            InstallForPanel(secondShiftPanel);
    }

    private static void InstallForPanel(Type type)
    {
        if (InstalledPanels.Contains(type))
            return;
        var create = AccessTools.DeclaredMethod(type, PortraitPanelContracts.CreateViews, new[] { typeof(WorkplaceWorkerType) })
            ?? throw new MissingMethodException(type.FullName, PortraitPanelContracts.CreateViews);
        var update = AccessTools.DeclaredMethod(type, PortraitPanelContracts.UpdateViews, Type.EmptyTypes)
            ?? throw new MissingMethodException(type.FullName, PortraitPanelContracts.UpdateViews);
        var updateFragment = AccessTools.DeclaredMethod(type, PortraitPanelContracts.UpdateFragment, Type.EmptyTypes)
            ?? throw new MissingMethodException(type.FullName, PortraitPanelContracts.UpdateFragment);
        RequireField(type, "_workplace", typeof(Workplace));
        RequireField(type, "_workplaceUsers", typeof(VisualElement));
        RequireField(type, "_views", typeof(IList));

        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(WorkplacePortraitPatch), nameof(BeforeUpdateViews)));
            // Both panels explicitly show the container on every frame. Hide it
            // AFTER that update as well as preventing portrait allocation.
            harmony.Patch(updateFragment, postfix: new HarmonyMethod(typeof(WorkplacePortraitPatch), nameof(AfterUpdateFragment)));
            harmony.Patch(create, prefix: new HarmonyMethod(typeof(WorkplacePortraitPatch), nameof(BeforeAddEmptyViews)));
            InstalledPanels.Add(type);
            Debug.Log($"[HaulingPostPlus] Compact portrait patches installed: {type.FullName} (3 methods).");
        }
        catch
        {
            // Never leave creation suppressed while the original updater still indexes the list.
            harmony.Unpatch(create, HarmonyPatchType.All, HarmonyId);
            harmony.Unpatch(update, HarmonyPatchType.All, HarmonyId);
            harmony.Unpatch(updateFragment, HarmonyPatchType.All, HarmonyId);
            throw;
        }
    }

    private static void RequireField(Type type, string name, Type assignableType)
    {
        FieldInfo? field = AccessTools.Field(type, name);
        if (field == null || !assignableType.IsAssignableFrom(field.FieldType))
            throw new MissingFieldException(type.FullName, name);
    }

    // Harmony's ___ prefix followed by the native field's leading underscore.
    private static bool BeforeAddEmptyViews(object __instance, Workplace ____workplace,
        VisualElement ____workplaceUsers, IList ____views)
    {
        if (!HaulingPosts.IsSupported(____workplace))
            return true;
        ____workplaceUsers.Clear();
        ____views.Clear();
        ____workplaceUsers.style.display = DisplayStyle.None;
        if (ReportedPanels.Add(__instance.GetType()))
            Debug.Log($"[HaulingPostPlus] Hauling Post portraits suppressed in {__instance.GetType().FullName}; worker counts and workplace controls retained.");
        return false;
    }

    private static bool BeforeUpdateViews(Workplace ____workplace) => !HaulingPosts.IsSupported(____workplace);

    private static void AfterUpdateFragment(Workplace ____workplace, VisualElement ____workplaceUsers)
    {
        if (HaulingPosts.IsSupported(____workplace))
            ____workplaceUsers.style.display = DisplayStyle.None;
        // On other buildings leave the panel's own display decision untouched.
    }
}
