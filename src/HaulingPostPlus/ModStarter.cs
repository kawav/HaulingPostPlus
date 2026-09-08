using Timberborn.ModManagerScene;
using UnityEngine;

namespace HaulingPostPlus;

public sealed class ModStarter : IModStarter
{
    public void StartMod(IModEnvironment modEnvironment)
    {
        WorkplacePortraitPatch.Install();
        Debug.Log($"[HaulingPostPlus] 0.1.3 loaded; capacity 1000; compact UI installed for {WorkplacePortraitPatch.InstalledPanelCount} panel type(s).");
    }
}
