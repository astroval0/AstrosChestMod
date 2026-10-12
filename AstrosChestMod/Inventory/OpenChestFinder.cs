using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AstrosChestMod.Inventory;

public class OpenChestFinder
{
    private readonly ICoreClientAPI capi;

    /// <summary>
    /// creates a finder for the local player
    /// </summary>
    /// <param name="capi">the client api</param>
    public OpenChestFinder(ICoreClientAPI capi)
    {
        this.capi = capi;
    }

    /// <summary>
    /// finds the open chest inventories the player can put items into as well as take items out of. other block inventories like barrels and firepits are not included.
    /// </summary>
    /// <returns>the open chests in the order they were opened, empty if none are open ofc</returns>
    public List<InventoryBase> FindOpenChests()
    {
        List<InventoryBase> chests = [];
        IClientPlayer? player = capi.World.Player;

        if (player == null)
        {
            return chests;
        }

        foreach (IInventory inventory in player.InventoryManager.OpenedInventories)
        {
            if (inventory is not InventoryBase chest || chest.Pos == null)
            {
                continue;
            }

            if (chest.PutLocked || chest.TakeLocked)
            {
                continue;
            }

            if (capi.World.BlockAccessor.GetBlockEntity(chest.Pos) is not BlockEntityGenericTypedContainer)
            {
                continue;
            }

            chests.Add(chest);
        }

        return chests;
    }
}