using Vintagestory.API.Common;

namespace AstrosChestMod.Sorting;

public class ChestLayoutPlanner
{

    /// <summary>
    /// plans the sorted layout
    /// </summary>
    /// <param name="slots">every slot of every open chest, one chest after the other</param>
    /// <returns>the stack each slot should end up with, null for slots that should be empty</returns>
    public static ItemStack?[] Plan(List<ItemSlot> slots)
    {
        List<ItemStack> sorted = slots
            .Select(slot => slot.Itemstack)
            .OfType<ItemStack>()
            .OrderBy(stack => stack.Collectible.Code.Path, StringComparer.Ordinal)
            .ThenByDescending(stack => stack.StackSize)
            .ToList();

        ItemStack?[] layout = new ItemStack?[slots.Count];
        for (int i = 0; i < sorted.Count; ++i)
        {
            layout[i] = sorted[i];
        }

        return layout;
    }
}