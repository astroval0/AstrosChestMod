using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstrosChestMod.Inventory;

public class ChestOrder
{
    private readonly List<BlockPos> knownChests = [];

    /// <summary>
    /// puts the chests in the order they were first sorted in, chests that were never sorted before go last in the order they were opened and keep that spot from then on
    /// </summary>
    /// <param name="chests">the open chests, in the order they were opened</param>
    /// <returns>the same chests in their remembered order</returns>
    public List<InventoryBase> Arrange(List<InventoryBase> chests)
    {
        foreach (InventoryBase chest in chests)
        {
            if (!knownChests.Contains(chest.Pos))
            {
                knownChests.Add(chest.Pos.Copy());
            }
        }

        return chests.OrderBy(chest => knownChests.IndexOf(chest.Pos)).ToList();
    }
}