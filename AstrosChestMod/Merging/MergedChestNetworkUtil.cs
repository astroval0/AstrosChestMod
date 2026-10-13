using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstrosChestMod.Merging;

public class MergedChestNetworkUtil : IInventoryNetworkUtil
{
    private readonly IInventoryNetworkUtil networkUtil;
    private readonly List<InventoryBase> chests;

    public ICoreAPI Api
    {
        get => networkUtil.Api;
        set => networkUtil.Api = value;
    }

    public bool PauseInventoryUpdates
    {
        get => networkUtil.PauseInventoryUpdates;
        set
        {
            networkUtil.PauseInventoryUpdates = value;
            foreach (InventoryBase chest in chests)
            {
                chest.InvNetworkUtil.PauseInventoryUpdates = value;
            }
        }
    }

    /// <summary>
    /// wraps the network util of the merged inventory so pausing it pauses the chests too. the slot grid pauses updates
    /// while a stack gets spread over slots by dragging the mouse, otherwise the server puts the old counts back in the
    /// slots halfway through
    /// </summary>
    /// <param name="networkUtil">the network util the game made for the merged inventory</param>
    /// <param name="chests">the chests that get paused with it</param>
    public MergedChestNetworkUtil(IInventoryNetworkUtil networkUtil, List<InventoryBase> chests)
    {
        this.networkUtil = networkUtil;
        this.chests = chests;
    }

    /// <summary>
    /// passes it on to the games network util
    /// </summary>
    /// <param name="slotId">the clicked slot</param>
    /// <param name="op">what the click does</param>
    /// <returns>the packet</returns>
    public object GetActivateSlotPacket(int slotId, ItemStackMoveOperation op)
    {
        return networkUtil.GetActivateSlotPacket(slotId, op);
    }

    /// <summary>
    /// passes it on to the games network util
    /// </summary>
    /// <param name="sourceInv">the inventory the other slot is in</param>
    /// <param name="sourceSlotId">the other slot</param>
    /// <param name="targetSlotId">the slot in this inventory</param>
    /// <returns>the packet</returns>
    public object GetFlipSlotsPacket(IInventory sourceInv, int sourceSlotId, int targetSlotId)
    {
        return networkUtil.GetFlipSlotsPacket(sourceInv, sourceSlotId, targetSlotId);
    }

    /// <summary>
    /// passes it on to the games network util
    /// </summary>
    /// <param name="byPlayer">the player that sent it</param>
    /// <param name="packetId">the packet id</param>
    /// <param name="data">the packet</param>
    public void HandleClientPacket(IPlayer byPlayer, int packetId, byte[] data)
    {
        networkUtil.HandleClientPacket(byPlayer, packetId, data);
    }

    /// <summary>
    /// passes it on to the games network util
    /// </summary>
    /// <param name="byPlayer">the player to send it to</param>
    /// <param name="forClientPacketId">the packet id to roll back</param>
    /// <param name="forClientPacketData">the packet to roll back</param>
    public void SendInventoryRollback(IServerPlayer byPlayer, int forClientPacketId,
        ReadOnlySpan<byte> forClientPacketData)
    {
        networkUtil.SendInventoryRollback(byPlayer, forClientPacketId, forClientPacketData);
    }

    /// <summary>
    /// passes it on to the games network util
    /// </summary>
    /// <param name="player">the player that opened it</param>
    /// <returns>the packet</returns>
    public object DidOpen(IPlayer player)
    {
        return networkUtil.DidOpen(player);
    }

    /// <summary>
    /// passes it on to the games network util
    /// </summary>
    /// <param name="player">the player that closed it</param>
    /// <returns>the packet</returns>
    public object DidClose(IPlayer player)
    {
        return networkUtil.DidClose(player);
    }
}