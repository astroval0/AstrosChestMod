using AstrosChestMod.Inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstrosChestMod.Sorting;

public class ChestSortFeature
{
    private const string HotkeyCode = "astroschestmod-sort";

    private readonly ICoreClientAPI capi;
    private readonly OpenChestFinder chestFinder;
    private readonly ChestSorter chestSorter;
    
    /// <summary>
    ///  Registers the sort hotkey
    /// </summary>
    /// <param name="capi"> the client api </param>
    public ChestSortFeature(ICoreClientAPI capi)
    {
        this.capi = capi;

        chestFinder = new OpenChestFinder(capi);
        chestSorter = new ChestSorter(capi, new SlotMover(capi));
        
        capi.Input.RegisterHotKey(HotkeyCode, "Sort Chests", GlKeys.R, HotkeyType.InventoryHotkeys);
        capi.Input.SetHotKeyHandler(HotkeyCode, OnSortHotkey);
    }

    /// <summary>
    /// sorts the chests when the hotkey is pressed
    /// </summary>
    /// <param name="keyCombination">the pressed keys</param>
    /// <returns>false when no chest is open so we dont override other hotkeys</returns>
    private bool OnSortHotkey(KeyCombination keyCombination)
    {
        List<InventoryBase> chests = chestFinder.FindOpenChests();
        if (chests.Count == 0)
        {
            return false;
        }

        SortResult result = chestSorter.Sort(chests);
        capi.ShowChatMessage($"Sorted {chests.Count} chest/s, {result.Merged} merged, {result.Moved} moved");

        return true;
    }
}