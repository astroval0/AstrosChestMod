using AstrosChestMod.Inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstrosChestMod.Sorting;

public class ChestSortFeature : IDisposable
{
    private const string HotkeyCode = "astroschestmod-sort";

    private const int ButtonRefreshMs = 100;
    private const long SortCooldownMs = 1000;

    private readonly ICoreClientAPI capi;
    private readonly OpenChestFinder chestFinder;
    private readonly ChestSorter chestSorter;

    private readonly GuiDialogSortButton sortButton;
    private readonly long buttonRefreshListenerId;

    private long lastSortMs = -SortCooldownMs;
    
    /// <summary>
    ///  Registers the sort hotkey and starts checking for open chests so the sort button can show up
    /// </summary>
    /// <param name="capi"> the client api </param>
    public ChestSortFeature(ICoreClientAPI capi)
    {
        this.capi = capi;

        chestFinder = new OpenChestFinder(capi);
        chestSorter = new ChestSorter(capi, new SlotMover(capi));

        sortButton = new GuiDialogSortButton(capi, OnSortButtonClicked);
        
        capi.Input.RegisterHotKey(HotkeyCode, "Sort Chests", GlKeys.R, HotkeyType.InventoryHotkeys);
        capi.Input.SetHotKeyHandler(HotkeyCode, OnSortHotkey);

        buttonRefreshListenerId = capi.Event.RegisterGameTickListener(RefreshSortButton, ButtonRefreshMs);
    }

    /// <summary>
    /// stops the button check and gets rid of the button
    /// </summary>
    public void Dispose()
    {
        capi.Event.UnregisterGameTickListener(buttonRefreshListenerId);
        sortButton.Dispose();
    }

    /// <summary>
    /// sorts the open chests when the button is clicked
    /// </summary>
    private void OnSortButtonClicked()
    {
        TrySortOpenChests();
    }
    
    /// <summary>
    /// sorts all open chests, unless none are open or the last sort was less than a second ago
    /// </summary>
    /// <returns></returns>
    private bool TrySortOpenChests()
    {
        List<InventoryBase> chests = chestFinder.FindOpenChests();
        if (chests.Count == 0)
        {
            return false;
        }

        long nowMs = capi.World.ElapsedMilliseconds;
        if (nowMs - lastSortMs < SortCooldownMs)
        {
            return true;
        }
        
        lastSortMs = nowMs;
        
        SortResult result = chestSorter.Sort(chests);
        capi.ShowChatMessage($"sorted {chests.Count} chest/s, {result.Merged} merged, {result.Moved} moved");

        return true;
    }

    /// <summary>
    /// shows the sort button while a chest is open and hides it once they are all closed
    /// </summary>
    /// <param name="deltaTime">seconds since the last check</param>
    private void RefreshSortButton(float deltaTime)
    {
        bool showButton = chestFinder.FindOpenChests().Count > 0;
        if (showButton && !sortButton.IsOpened())
        {
            sortButton.TryOpen();
        }
        else if (!showButton && sortButton.IsOpened())
        {
            sortButton.TryClose();
        }
    }

    /// <summary>
    /// sorts the chests when the hotkey is pressed
    /// </summary>
    /// <param name="keyCombination">the pressed keys</param>
    /// <returns>false when no chest is open so we dont override other hotkeys</returns>
    private bool OnSortHotkey(KeyCombination keyCombination)
    {
        return TrySortOpenChests();
    }
}