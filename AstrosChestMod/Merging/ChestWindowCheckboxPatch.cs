using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstrosChestMod.Merging;

[HarmonyPatch(typeof(GuiDialogBlockEntityInventory), MethodType.Constructor, typeof(string), typeof(InventoryBase),
    typeof(BlockPos), typeof(int), typeof(int), typeof(ICoreClientAPI))]
public static class ChestWindowCheckboxPatch
{
    /// <summary>
    /// gives every new chest window the merge checkbox
    /// </summary>
    /// <param name="__instance">the chest window that just got built</param>
    /// <param name="dialogTitle">its title</param>
    public static void Postfix(GuiDialogBlockEntityInventory __instance, string dialogTitle)
    {
        ChestMergeFeature.Active?.AddMergeCheckbox(__instance, dialogTitle);
    }
}