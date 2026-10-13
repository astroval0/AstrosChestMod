using System.Reflection;
using AstrosChestMod.Config;
using AstrosChestMod.Inventory;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace AstrosChestMod.Merging;

public class ChestMergeFeature : IDisposable
{
    private const string CheckboxKey = "astroschestmod-merge";
    private const double CheckboxSize = 18;
    private const int RefreshMs = 100;
    private const int CloseChestPacket = 1001;

    private static readonly AccessTools.FieldRef<BlockEntityOpenableContainer, GuiDialogBlockEntity?> ChestWindowField =
        AccessTools.FieldRefAccess<BlockEntityOpenableContainer, GuiDialogBlockEntity?>("invDialog");

    private static readonly FieldInfo? PlayerInventoriesField =
        AccessTools.Field(AccessTools.TypeByName("Vintagestory.Common.PlayerInventoryManager"), "Inventories");

    private readonly ICoreClientAPI capi;
    private readonly ChestOrder chestOrder;
    private readonly ILogger logger;
    private readonly GuiDialogMergedChests mergedWindow;
    private readonly HashSet<GuiDialogBlockEntity> windowsBeingSwapped = [];
    private readonly long refreshListenerId;

    public static ChestMergeFeature? Active { get; private set; }

    /// <summary>
    /// sets up merging chests into one window, the harmony patches get to it through Active
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="configFile">the mod config, the merged window reads if it should be one big chest from it</param>
    /// <param name="chestOrder">decides the order of the chests in the merged window</param>
    /// <param name="logger">the mod logger</param>
    public ChestMergeFeature(ICoreClientAPI capi, ConfigFile configFile, ChestOrder chestOrder, ILogger logger)
    {
        this.capi = capi;
        this.chestOrder = chestOrder;
        this.logger = logger;

        mergedWindow = new GuiDialogMergedChests(capi, configFile, OnChestsUnmerged, OnMergedWindowClosed);
        refreshListenerId = capi.Event.RegisterGameTickListener(OnRefresh, RefreshMs);
        Active = this;
    }

    /// <summary>
    /// stops the refresh, gets rid of the merged window and stops the harmony patches from using this
    /// </summary>
    public void Dispose()
    {
        capi.Event.UnregisterGameTickListener(refreshListenerId);
        mergedWindow.Dispose();

        if (Active == this)
        {
            Active = null;
        }
    }

    /// <summary>
    /// adds the merge checkbox to a chest window, right after its title
    /// </summary>
    /// <param name="window">the chest window</param>
    /// <param name="title">the title of the chest window</param>
    public void AddMergeCheckbox(GuiDialogBlockEntityInventory window, string title)
    {
        if (window.IsDuplicate ||
            capi.World.BlockAccessor.GetBlockEntity(window.BlockEntityPosition) is not BlockEntityGenericTypedContainer)
        {
            return;
        }

        double titleWidth = CairoFont.WhiteSmallText().GetTextExtents(title).Width / RuntimeEnv.GUIScale;
        ElementBounds bounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding + titleWidth + 8,
            (GuiStyle.TitleBarHeight - CheckboxSize) / 2, CheckboxSize, CheckboxSize);

        GuiComposer composer = window.SingleComposer;
        composer.AddInteractiveElement(
            new GuiElementSwitch(capi, merge => OnMergeTicked(window, merge), bounds, CheckboxSize, 2), CheckboxKey);
        composer.AddInteractiveElement(new GuiElementHoverText(capi, "merge this chest", CairoFont.WhiteSmallText(),
            200, bounds.FlatCopy()));
        composer.ReCompose();
    }

    /// <summary>
    /// tells the close patch if a chest window is only being swapped for the merged window, its chest has to stay open then
    /// </summary>
    /// <param name="window">the window that is closing</param>
    /// <returns>true if the chest has to stay open</returns>
    public bool IsBeingSwapped(GuiDialogBlockEntity window)
    {
        return windowsBeingSwapped.Contains(window);
    }

    /// <summary>
    /// closes a chest that is in the merged window when it gets right clicked again, the same as a chest with its own window.
    /// the game would try to open a second window for it otherwise, that one counts as a duplicate and stays broken
    /// </summary>
    /// <param name="blockEntity">the chest the server wants to open</param>
    /// <returns>true if the chest was in the merged window, the game should leave it alone then</returns>
    public bool TryCloseMergedChest(BlockEntityOpenableContainer blockEntity)
    {
        MergedChest? chest = mergedWindow.Chests.FirstOrDefault(merged => merged.Inventory == blockEntity.Inventory);
        if (chest == null)
        {
            return false;
        }

        RemoveFromMergedWindow([chest]);
        CloseChest(chest);
        capi.Gui.PlaySound(chest.CloseSound);
        return true;
    }

    /// <summary>
    /// merges a chest when its checkbox gets ticked. that only happens next frame because the click is still running
    /// inside the window that gets closed
    /// </summary>
    /// <param name="window">the chest window the checkbox is on</param>
    /// <param name="merge">true if the checkbox got ticked</param>
    private void OnMergeTicked(GuiDialogBlockEntityInventory window, bool merge)
    {
        if (merge)
        {
            capi.Event.EnqueueMainThreadTask(() => MergeChestWindow(window), "astroschestmod-merge");
        }
    }

    /// <summary>
    /// gives chests their own windows back when their checkbox in the merged window gets unticked, next frame for the
    /// same reason as merging
    /// </summary>
    /// <param name="chests">the chests to unmerge</param>
    private void OnChestsUnmerged(List<MergedChest> chests)
    {
        capi.Event.EnqueueMainThreadTask(() => PopOutChests(chests), "astroschestmod-merge");
    }

    /// <summary>
    /// moves a chest from its own window into the merged window, the chest stays open the whole time
    /// </summary>
    /// <param name="window">the chest window</param>
    private void MergeChestWindow(GuiDialogBlockEntityInventory window)
    {
        BlockEntity? blockEntity = capi.World.BlockAccessor.GetBlockEntity(window.BlockEntityPosition);
        if (!window.IsOpened() || window.Inventory == null || blockEntity is not BlockEntityGenericTypedContainer chest)
        {
            return;
        }

        MergedChest merged = new(window.Inventory, window.BlockEntityPosition.Copy(), window.DialogTitle,
            chest.quantityColumns, window.CloseSound);
        CloseWithoutClosingChest(window, chest);
        ShowChests([.. mergedWindow.Chests, merged]);
    }

    /// <summary>
    /// closes a chest window but leaves its chest open so the merged window can take it over. the chest only forgets
    /// its window when the chest itself closes, so that gets cleared here too
    /// </summary>
    /// <param name="window">the chest window</param>
    /// <param name="chest">the chest it belongs to</param>
    private void CloseWithoutClosingChest(GuiDialogBlockEntityInventory window, BlockEntityGenericTypedContainer chest)
    {
        windowsBeingSwapped.Add(window);
        window.TryClose();
        windowsBeingSwapped.Remove(window);

        ChestWindowField(chest) = null;
        window.Dispose();
    }

    /// <summary>
    /// shows the chests in the merged window in their saved order and opens it if it isnt open yet
    /// </summary>
    /// <param name="chests">the chests to show</param>
    private void ShowChests(List<MergedChest> chests)
    {
        List<InventoryBase> order = chestOrder.Arrange(chests.Select(chest => chest.Inventory).ToList());
        mergedWindow.ShowChests(order.Select(inventory => chests.First(chest => chest.Inventory == inventory))
            .ToList());

        if (!mergedWindow.IsOpened())
        {
            mergedWindow.TryOpen();
        }
    }

    /// <summary>
    /// takes chests out of the merged window, the window closes when none are left. the chests stay open
    /// </summary>
    /// <param name="chests">the chests to take out</param>
    private void RemoveFromMergedWindow(List<MergedChest> chests)
    {
        List<MergedChest> rest = mergedWindow.Chests.Where(other => !chests.Contains(other)).ToList();
        if (rest.Count > 0)
        {
            mergedWindow.ShowChests(rest);
            return;
        }

        mergedWindow.ClearChests();
        mergedWindow.TryClose();
    }

    /// <summary>
    /// takes chests out of the merged window and gives them their own windows again, without closing them
    /// </summary>
    /// <param name="chests">the chests</param>
    private void PopOutChests(List<MergedChest> chests)
    {
        List<MergedChest> merged = chests.Where(mergedWindow.Chests.Contains).ToList();
        if (merged.Count == 0)
        {
            return;
        }

        RemoveFromMergedWindow(merged);
        foreach (MergedChest chest in merged)
        {
            OpenChestWindow(chest);
        }
    }

    /// <summary>
    /// opens a normal chest window for a chest that is already open, the same way the game does when the server opens it
    /// </summary>
    /// <param name="chest">the chest</param>
    private void OpenChestWindow(MergedChest chest)
    {
        if (capi.World.BlockAccessor.GetBlockEntity(chest.Pos) is not BlockEntityOpenableContainer blockEntity)
        {
            CloseChest(chest);
            return;
        }

        ForgetOpenedInventory(chest.Inventory);
        GuiDialogBlockEntityInventory window = new(chest.Title, chest.Inventory, chest.Pos, chest.Columns, capi)
        {
            CloseSound = chest.CloseSound
        };

        if (window.IsDuplicate)
        {
            logger.Warning("Could not give the chest at {0} its own window again, closing it instead", chest.Pos);
            window.Dispose();
            CloseChest(chest);
            return;
        }

        ChestWindowField(blockEntity) = window;
        window.TryOpen();
    }

    /// <summary>
    /// takes a chest out of the players open inventories without closing it, a new window for a chest that is still in
    /// there counts as a duplicate and stays empty. the window puts it back when it opens. the api only hands out a copy
    /// of that list so it goes through the games own one
    /// </summary>
    /// <param name="inventory">the chest inventory</param>
    private void ForgetOpenedInventory(InventoryBase inventory)
    {
        IPlayerInventoryManager inventoryManager = capi.World.Player.InventoryManager;
        if (PlayerInventoriesField?.GetValue(inventoryManager) is IDictionary<string, InventoryBase> inventories)
        {
            inventories.Remove(inventory.InventoryID);
        }
    }

    /// <summary>
    /// closes every chest that was in the merged window once the window is gone
    /// </summary>
    private void OnMergedWindowClosed()
    {
        List<MergedChest> chests = [.. mergedWindow.Chests];
        mergedWindow.ClearChests();

        foreach (MergedChest chest in chests)
        {
            CloseChest(chest);
        }

        if (chests.Count > 0)
        {
            capi.Gui.PlaySound(chests[0].CloseSound);
        }
    }

    /// <summary>
    /// closes a chest the same way its own window would
    /// </summary>
    /// <param name="chest">the chest to close</param>
    private void CloseChest(MergedChest chest)
    {
        capi.World.Player.InventoryManager.CloseInventoryAndSync(chest.Inventory);
        capi.Network.SendBlockEntityPacket(chest.Pos, CloseChestPacket);
    }

    /// <summary>
    /// drops chests the server closed, closes chests that got broken or that the player walked away from and keeps the
    /// rest of the window up to date
    /// </summary>
    /// <param name="deltaTime">seconds since the last check</param>
    private void OnRefresh(float deltaTime)
    {
        if (!mergedWindow.IsOpened())
        {
            return;
        }

        List<IInventory> opened = capi.World.Player.InventoryManager.OpenedInventories;
        List<MergedChest> stillOpen = [];
        foreach (MergedChest chest in mergedWindow.Chests)
        {
            if (!opened.Contains(chest.Inventory))
            {
                continue;
            }

            if (!IsChestStillThere(chest) || !mergedWindow.IsInRangeOfBlock(chest.Pos))
            {
                CloseChest(chest);
                capi.Gui.PlaySound(chest.CloseSound);
                continue;
            }

            stillOpen.Add(chest);
        }

        if (stillOpen.Count == mergedWindow.Chests.Count)
        {
            mergedWindow.Refresh();
            return;
        }

        if (stillOpen.Count == 0)
        {
            mergedWindow.ClearChests();
            mergedWindow.TryClose();
            return;
        }

        mergedWindow.ShowChests(stillOpen);
    }

    /// <summary>
    /// checks if the chest block is still there with the same inventory, it can get broken while its merged
    /// </summary>
    /// <param name="chest">the chest</param>
    /// <returns>true if its still there</returns>
    private bool IsChestStillThere(MergedChest chest)
    {
        return capi.World.BlockAccessor.GetBlockEntity(chest.Pos) is BlockEntityOpenableContainer blockEntity &&
               blockEntity.Inventory == chest.Inventory;
    }
}