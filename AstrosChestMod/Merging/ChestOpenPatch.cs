using HarmonyLib;
using Vintagestory.GameContent;

namespace AstrosChestMod.Merging;

[HarmonyPatch(typeof(BlockEntityOpenableContainer), nameof(BlockEntityOpenableContainer.OnReceivedServerPacket))]
public static class ChestOpenPatch
{
    private const int OpenChestPacket = 5000;

    /// <summary>
    /// stops the game from opening a window for a chest that is already in the merged window, the merge feature closes
    /// it instead like a second right click does
    /// </summary>
    /// <param name="__instance">the chest that got the packet</param>
    /// <param name="packetid">the packet id, 5000 is the server telling the client to open the chest</param>
    /// <returns>false to skip the game opening the chest</returns>
    public static bool Prefix(BlockEntityOpenableContainer __instance, int packetid)
    {
        return packetid != OpenChestPacket || ChestMergeFeature.Active?.TryCloseMergedChest(__instance) != true;
    }
}