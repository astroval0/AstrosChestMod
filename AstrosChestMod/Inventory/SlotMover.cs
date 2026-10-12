using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstrosChestMod.Inventory;

public class SlotMover
{
    private readonly ICoreClientAPI capi;
    
    /// <summary>
    /// creates a mover for the local player
    /// </summary>
    /// <param name="capi">the client api</param>
    public SlotMover(ICoreClientAPI capi)
    {
        this.capi = capi;
    }

    /// <summary>
    /// moves as much of the source stack into the sink as fits
    /// </summary>
    /// <param name="source">the slot to take from</param>
    /// <param name="sink">the slot to put into</param>
    /// <returns>true if anything was moved</returns>
    public bool TryMerge(ItemSlot source, ItemSlot sink)
    {
        ItemStackMoveOperation operation = new(capi.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, source.StackSize)
        {
                ActingPlayer = capi.World.Player
        };

        object? packet = capi.World.Player.InventoryManager.TryTransferTo(source, sink, ref operation);
        if (packet == null || operation.MovedQuantity <= 0)
        {
            return false;
        }
        
        capi.Network.SendPacketClient(packet);

        return true;
    }

    /// <summary>
    /// swaps the stacks of two slots
    /// </summary>
    /// <param name="source">one of the slots</param>
    /// <param name="target">the other slot, its inventory does the swap</param>
    /// <returns>true if the stacks were swapped</returns>
    public bool TrySwap(ItemSlot source, ItemSlot target)
    {
        InventoryBase targetInventory = target.Inventory;

        object? packet = targetInventory.TryFlipItems(targetInventory.GetSlotId(target), source);
        if (packet == null)
        {
            return false;
        }
        
        capi.Network.SendPacketClient(packet);
        return true;
    }
}