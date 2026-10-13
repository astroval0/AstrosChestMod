using System.Reflection;
using AstrosChestMod.Config;
using AstrosChestMod.Inventory;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AstrosChestMod.Merging;

public class ChestMergeFeature : IDisposable
{
    private const string CheckboxKey = "astroschestmod-merge";
    private const double CheckboxSize = 18;
    private const int RefreshMs = 100;
    private const int CloseChestPacket = 1001;
    private const long OpenRequestTimeoutMs = 2000;

    private static readonly AccessTools.FieldRef<BlockEntityOpenableContainer, GuiDialogBlockEntity?> ChestWindowField =
        AccessTools.FieldRefAccess<BlockEntityOpenableContainer, GuiDialogBlockEntity?>("invDialog");

    private static readonly FieldInfo? PlayerInventoriesField =
        AccessTools.Field(AccessTools.TypeByName("Vintagestory.Common.PlayerInventoryManager"), "Inventories");

    private readonly ICoreClientAPI capi;
    private readonly ChestOrder chestOrder;
    private readonly MergedChestGroups chestGroups;
    private readonly ILogger logger;
    private readonly GuiDialogMergedChests mergedWindow;
    private readonly HashSet<GuiDialogBlockEntity> windowsBeingSwapped = [];
    private readonly Dictionary<BlockPos, long> openRequests = [];
    private readonly long refreshListenerId;

    public static ChestMergeFeature? Active { get; private set; }

    /// <summary>
    /// sets up merging chests into one window, the harmony patches get to it through Active
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="mod">the mod, for the logger and the folder the merged chest groups get saved in</param>
    /// <param name="configFile">the mod config, the merged window reads if it should be one big chest from it</param>
    /// <param name="chestOrder">decides the order of the chests in the merged window</param>
    public ChestMergeFeature(ICoreClientAPI capi, Mod mod, ConfigFile configFile, ChestOrder chestOrder)
    {
        this.capi = capi;
        this.chestOrder = chestOrder;
        logger = mod.Logger;
        chestGroups = new MergedChestGroups(capi, mod);

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
    /// swaps the chests that are merged into one big chest for that one big chest so they get sorted like one chest, it
    /// goes where the first of them was
    /// </summary>
    /// <param name="chests">the open chests in their order</param>
    /// <returns>the chests with the one big chest in place of the chests in it, the same list when there is none</returns>
    public List<InventoryBase> CombineOneChest(List<InventoryBase> chests)
    {
        MergedChestInventory? oneChest = mergedWindow.OneChest;
        List<InventoryBase> merged = mergedWindow.Chests.Select(chest => chest.Inventory).ToList();
        int first = chests.FindIndex(merged.Contains);
        if (oneChest == null || first < 0 || !merged.All(chests.Contains))
        {
            return chests;
        }

        List<InventoryBase> combined = chests.Except(merged).ToList();
        combined.Insert(first, oneChest);
        return combined;
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
    /// moves a chest that just got opened into the merged window when its in a group. when the player opened it the rest
    /// of the group gets opened too, chests the mod opened for the group dont open it again. the game already opened the
    /// chests own window, that one gets swapped out before it ever gets drawn
    /// </summary>
    /// <param name="blockEntity">the chest that just got opened</param>
    public void OnChestOpened(BlockEntityOpenableContainer blockEntity)
    {
        bool requested = IsOpenRequested(blockEntity.Pos, capi.World.ElapsedMilliseconds);
        openRequests.Remove(blockEntity.Pos);

        IReadOnlyList<BlockPos>? group = chestGroups.FindGroup(blockEntity.Pos);
        if (group == null || ChestWindowField(blockEntity) is not GuiDialogBlockEntityInventory window)
        {
            return;
        }

        if (MergeChestWindow(window) && !requested)
        {
            OpenRestOfGroup(group);
        }
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
            capi.Event.EnqueueMainThreadTask(() => MergeIntoGroup(window), "astroschestmod-merge");
        }
    }

    /// <summary>
    /// merges a chest window and puts its chest in one group with the chests in the merged window, so they all open
    /// together from now on
    /// </summary>
    /// <param name="window">the chest window</param>
    private void MergeIntoGroup(GuiDialogBlockEntityInventory window)
    {
        if (MergeChestWindow(window))
        {
            chestGroups.Merge(window.BlockEntityPosition, mergedWindow.Chests.Select(chest => chest.Pos).ToList());
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
    /// <returns>true if the chest got merged</returns>
    private bool MergeChestWindow(GuiDialogBlockEntityInventory window)
    {
        BlockEntity? blockEntity = capi.World.BlockAccessor.GetBlockEntity(window.BlockEntityPosition);
        if (!window.IsOpened() || window.Inventory == null || blockEntity is not BlockEntityGenericTypedContainer chest)
        {
            return false;
        }

        MergedChest merged = new(window.Inventory, window.BlockEntityPosition.Copy(), window.DialogTitle,
            chest.quantityColumns, window.CloseSound);
        CloseWithoutClosingChest(window, chest);
        ShowChests([.. mergedWindow.Chests, merged]);
        return true;
    }

    /// <summary>
    /// opens the other chests of a group that are in reach by right clicking them for the player. the server opens them
    /// like any chest the player right clicks and sends what is in them, the copy of a closed chest the client has can
    /// be old. nothing happens while sneaking, some chests do something else on a sneaking right click
    /// </summary>
    /// <param name="group">the positions of the chests in the group</param>
    private void OpenRestOfGroup(IReadOnlyList<BlockPos> group)
    {
        if (capi.World.Player.Entity.Controls.ShiftKey)
        {
            return;
        }

        long nowMs = capi.World.ElapsedMilliseconds;
        foreach (BlockPos pos in group)
        {
            if (IsOpenRequested(pos, nowMs) || !IsClosedChestInReach(pos))
            {
                continue;
            }

            openRequests[pos.Copy()] = nowMs;
            RightClickChest(pos);
        }
    }

    /// <summary>
    /// checks if the mod right clicked a chest for the group and is still waiting for it to open
    /// </summary>
    /// <param name="pos">where the chest is</param>
    /// <param name="nowMs">the time now in milliseconds</param>
    /// <returns>true if its still waiting, requests the server never answered run out after a bit</returns>
    private bool IsOpenRequested(BlockPos pos, long nowMs)
    {
        return openRequests.TryGetValue(pos, out long requestedMs) && nowMs - requestedMs < OpenRequestTimeoutMs;
    }

    /// <summary>
    /// checks if there is still a closed chest at a spot and the player can reach it. something else could have been
    /// placed there since, right clicking that would use it
    /// </summary>
    /// <param name="pos">where the chest should be</param>
    /// <returns>true if the chest can be opened</returns>
    private bool IsClosedChestInReach(BlockPos pos)
    {
        return capi.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityGenericTypedContainer chest &&
               !capi.World.Player.InventoryManager.OpenedInventories.Contains(chest.Inventory) &&
               mergedWindow.IsInRangeOfBlock(pos);
    }

    /// <summary>
    /// right clicks a chest for the player with the same packets the game sends when the player right clicks a block
    /// </summary>
    /// <param name="pos">where the chest is</param>
    private void RightClickChest(BlockPos pos)
    {
        BlockSelection selection = new()
        {
            Position = pos.Copy(),
            Face = BlockFacing.UP,
            HitPosition = new Vec3d(0.5, 1, 0.5)
        };

        capi.Network.SendHandInteraction((int)EnumMouseButton.Right, selection, null, EnumHandInteract.BlockInteract,
            (int)EnumHandInteractNw.StartBlockUse, false, EnumItemUseCancelReason.ReleasedMouse);
        capi.Network.SendHandInteraction((int)EnumMouseButton.Right, selection, null, EnumHandInteract.BlockInteract,
            (int)EnumHandInteractNw.StopBlockUse, false, EnumItemUseCancelReason.ReleasedMouse);
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
    /// takes chests out of the merged window and gives them their own windows again, without closing them. they leave
    /// their group too so they open on their own from now on
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
            chestGroups.Remove(chest.Pos);
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