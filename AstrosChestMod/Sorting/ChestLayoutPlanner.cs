using Vintagestory.API.Common;

namespace AstrosChestMod.Sorting;

public static class ChestLayoutPlanner
{

    /// <summary>
    /// plans the sorted layout
    /// </summary>
    /// <param name="slots">every slot of every open chest, one chest after the other</param>
    /// <param name="world">needed to check what counts as food</param>
    /// <returns>the stack each slot should end up with, null for slots that should be empty</returns>
    public static ItemStack?[] Plan(List<ItemSlot> slots, IWorldAccessor world)
    {
        List<SortEntry> sorted = slots
            .Select(slot => slot.Itemstack)
            .OfType<ItemStack>()
            .Select(            stack => new SortEntry(stack, ItemClassifier.GetCategory(stack, world), ItemClassifier.GetFamily(stack)))
            .OrderBy(           entry => entry.Category)
            .ThenBy(            entry => ItemClassifier.GetFamilyRank(entry.Family))
            .ThenBy(            entry => entry.Family, StringComparer.Ordinal)
            .ThenBy(            entry => ItemClassifier.GetWithinFamilyKey(entry.Stack), StringComparer.Ordinal)
            .ThenBy(            entry => entry.Stack.Collectible.Code.Domain, StringComparer.Ordinal)
            .ThenBy(            entry => entry.Stack.Class)
            .ThenBy(            entry => entry.Stack.GetName(), StringComparer.Ordinal)
            .ThenByDescending(  entry => entry.Stack.StackSize)
            .ToList();

        ItemStack?[] layout = new ItemStack?[slots.Count];
        for (int i = 0; i < sorted.Count; ++i)
        {
            layout[i] = sorted[i].Stack;
        }

        return layout;
    }

    private readonly record struct SortEntry(ItemStack Stack, ItemCategory Category, string Family);
}