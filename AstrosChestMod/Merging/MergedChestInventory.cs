using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstrosChestMod.Merging;

public class MergedChestInventory : InventoryBase, IDisposable
{
    private readonly List<(MergedChest Chest, int SlotId)> slots = [];
    private readonly List<(InventoryBase Inventory, Action<int> Listener)> slotListeners = [];
    private readonly Action<MergedChest, object> sendChestPacket;

    public override int Count => slots.Count;

    public override ItemSlot this[int slotId]
    {
        get => slots[slotId].Chest.Inventory[slots[slotId].SlotId];
        set => throw new NotSupportedException("the merged chests keep their own slots");
    }

    /// <summary>
    /// puts the slots of the chests together into one inventory, one chest after the other. the chests keep their own
    /// slots, this only hands them out so one slot grid can show all of them like one big chest
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="chests">the chests in the order their slots go in</param>
    /// <param name="sendChestPacket">sends a slot packet to the chest the slot belongs to</param>
    public MergedChestInventory(ICoreClientAPI capi, List<MergedChest> chests,
        Action<MergedChest, object> sendChestPacket)
        : base("astroschestmod-mergedchests", capi)
    {
        this.sendChestPacket = sendChestPacket;

        foreach (MergedChest chest in chests)
        {
            int firstSlot = slots.Count;
            for (int slotId = 0; slotId < chest.Inventory.Count; slotId++)
            {
                slots.Add((chest, slotId));
            }

            Action<int> listener = slotId => MarkSlotDirty(firstSlot + slotId);
            chest.Inventory.SlotModified += listener;
            slotListeners.Add((chest.Inventory, listener));
        }

        InvNetworkUtil = new MergedChestNetworkUtil(InvNetworkUtil, chests.Select(chest => chest.Inventory).ToList());
    }

    /// <summary>
    /// stops following the chests and lets their updates through again, in case the window got rebuilt while a stack
    /// was being dragged over the slots
    /// </summary>
    public void Dispose()
    {
        foreach ((InventoryBase inventory, Action<int> listener) in slotListeners)
        {
            inventory.SlotModified -= listener;
        }

        InvNetworkUtil.PauseInventoryUpdates = false;
    }

    /// <summary>
    /// clicks the slot in the chest it belongs to and sends the click to that chest, the same way the chest window does
    /// </summary>
    /// <param name="slotId">the slot in the merged inventory</param>
    /// <param name="sourceSlot">the slot the click comes from, mostly the one on the mouse</param>
    /// <param name="op">what the click does</param>
    /// <returns>null, the packet already went to the chest so the slot grid doesnt send it again</returns>
    public override object? ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
    {
        (MergedChest chest, int chestSlotId) = slots[slotId];
        object? packet = chest.Inventory.ActivateSlot(chestSlotId, sourceSlot, ref op);
        if (packet != null)
        {
            sendChestPacket(chest, packet);
        }

        return null;
    }

    /// <summary>
    /// does nothing, the chests load their own slots
    /// </summary>
    /// <param name="tree">not used</param>
    public override void FromTreeAttributes(ITreeAttribute tree)
    {
    }

    /// <summary>
    /// does nothing, the chests save their own slots
    /// </summary>
    /// <param name="tree">not used</param>
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
    }
}