using Vintagestory.API.Client;

namespace AstrosChestMod.Sorting;

public class ChestSortFeature
{
    private const string HotkeyCode = "astroschestmod-sort";

    private readonly ICoreClientAPI capi;
    
    /// <summary>
    ///  Registers the sort hotkey
    /// </summary>
    /// <param name="capi"> the client api </param>
    public ChestSortFeature(ICoreClientAPI capi)
    {
        this.capi = capi;
        
        capi.Input.RegisterHotKey(HotkeyCode, "Sort Chests", GlKeys.R, HotkeyType.InventoryHotkeys);
        capi.Input.SetHotKeyHandler(HotkeyCode, OnSortHotkey);
    }

    /// <summary>
    /// sorts the chests when the hotkey is pressed
    /// </summary>
    /// <param name="keyCombination">the pressed keys</param>
    /// <returns>true</returns>
    private bool OnSortHotkey(KeyCombination keyCombination)
    {
        capi.ShowChatMessage("sort pressed");
        return true;
    }
}