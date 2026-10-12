using Vintagestory.API.Common;

namespace AstrosChestMod.Sorting;

public static class ChestLayoutPlanner
{

    /// <summary>
    /// plans the sorted layout
    /// </summary>
    /// <param name="slots">every slot of every open chest, one chest after the other</param>
    /// <param name="shapes">the shape of each chest, in the same order as the slots</param>
    /// <param name="world">needed to check what counts as food</param>
    /// <returns>the stack each slot should end up with, null for slots that should be empty</returns>
    public static ItemStack?[] Plan(List<ItemSlot> slots, List<ChestShape> shapes, IWorldAccessor world)
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

        ChestCursor cursor = new(shapes, slots.Count);
        int stacksLeft = sorted.Count;

        foreach (var category in sorted.GroupBy(entry => entry.Category))
        {
            cursor.TryLeaveCategoryGap(stacksLeft);
            cursor.TryStartInNextChest(category.Count(), stacksLeft);

            foreach (var family in category.GroupBy(entry => entry.Family))
            {
                cursor.TryStartInNextChest(family.Count(), stacksLeft);

                foreach (var sameItem in family.GroupBy(entry => entry.Stack.Collectible))
                {
                    cursor.TryStartInNextChest(sameItem.Count(), stacksLeft);
                    
                    foreach (SortEntry entry in sameItem)
                    {
                        layout[cursor.NextSlot] = entry.Stack;
                        cursor.Advance();
                
                        --stacksLeft;
                    }   
                }
            }
        }
        
        return layout;
    }

    private readonly record struct SortEntry(ItemStack Stack, ItemCategory Category, string Family);

    private class ChestCursor
    {
        private readonly List<ChestShape> shapes;
        private readonly int totalSlots;
        private int chestIndex;
        private int chestStartSlot;
        
        public int NextSlot { get; private set; }

        private ChestShape CurrentChest => shapes[chestIndex];
        private int ChestEndSlot => chestStartSlot + CurrentChest.SlotCount;
        private bool HasNextChest => chestIndex + 1 < shapes.Count;
        
        /// <summary>
        /// creates a cursor that starts at the first slot of the first chest
        /// </summary>
        /// <param name="shapes">the shape of each chest</param>
        /// <param name="totalSlots">the slot count of all chests together</param>
        public ChestCursor(List<ChestShape> shapes, int totalSlots)
        {
            this.shapes = shapes;
            this.totalSlots = totalSlots;
        }

        /// <summary>
        /// moves to the start of the next row so there is at least one empty slot before the next category
        /// (does nothing at the start of a chest or when the remaining stacks wouldnt fit anymore)
        /// </summary>
        /// <param name="stacksLeft">how many stacks still need a slot</param>
        public void TryLeaveCategoryGap(int stacksLeft)
        {
            int offset = NextSlot - chestStartSlot;

            if (offset == 0)
            {
                return;
            }

            int columns = CurrentChest.Columns > 0 ? CurrentChest.Columns : CurrentChest.SlotCount;
            int gapOffset = (offset + columns) / columns * columns;
            int gapSlot = gapOffset >= CurrentChest.SlotCount ? ChestEndSlot : chestStartSlot + gapOffset;

            if (stacksLeft <= totalSlots - gapSlot)
            {
                MoveTo(gapSlot);
            }
        }
        
        /// <summary>
        /// moves to the start of the next chest when a run of stacks would get split between this chest and the next one
        /// </summary>
        /// <param name="runSize">how many stacks are in the run</param>
        /// <param name="stacksLeft">how many stacks still need a slot</param>
        public void TryStartInNextChest(int runSize, int stacksLeft)
        {
            bool runWouldSplit = NextSlot > chestStartSlot && NextSlot + runSize > ChestEndSlot;
            bool runFitsInNextChest = HasNextChest && runSize <= shapes[chestIndex + 1].SlotCount;
            
            if (runWouldSplit && runFitsInNextChest && stacksLeft <= totalSlots - ChestEndSlot)
            {
                MoveTo(ChestEndSlot);
            }
        }

        /// <summary>
        /// moves to the next slot
        /// </summary>
        public void Advance()
        {
            MoveTo(NextSlot + 1);
        }

        /// <summary>
        /// moves to a slot and steps into the next chest when the slot is past the end of the current one
        /// </summary>
        private void MoveTo(int slot)
        {
            NextSlot = slot;

            while (HasNextChest && NextSlot >= ChestEndSlot)
            {
                chestStartSlot = ChestEndSlot;
                ++chestIndex;
            }
        }
    }
}