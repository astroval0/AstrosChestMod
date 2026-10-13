using HarmonyLib;
using Vintagestory.API.Client;

namespace AstrosChestMod.Merging;

[HarmonyPatch(typeof(GuiDialogBlockEntity), nameof(GuiDialogBlockEntity.OnGuiClosed))]
public static class ChestWindowClosePatch
{
    /// <summary>
    /// keeps the chest open when its window only gets swapped for the merged window
    /// </summary>
    /// <param name="__instance">the window that is closing</param>
    /// <returns>false to skip closing the chest</returns>
    public static bool Prefix(GuiDialogBlockEntity __instance)
    {
        return ChestMergeFeature.Active?.IsBeingSwapped(__instance) != true;
    }
}