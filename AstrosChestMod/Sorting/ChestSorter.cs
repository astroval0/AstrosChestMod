using System.Data.Common;
using AstrosChestMod.Inventory;
using Vintagestory.API.Common;

namespace AstrosChestMod.Sorting;

public class ChestSorter
{
    private readonly SlotMover slotMover;
    
    /// <summary>
    /// creates a sorter that moves items with the given mover
    /// </summary>
    /// <param name="slotMover">does the movingg</param>
    public ChestSorter(SlotMover slotMover)
    {
        this.slotMover = slotMover;
    }

    /// <summary>
    /// merges duplicate stacks across all the chests
    /// </summary>
    /// <param name="chests">the chest to sort to</param>
    /// <returns>how many stacks got merged</returns>
    public int Sort(List<InventoryBase> chests)
    {
        List<ItemSlot> slots = chests.SelectMany(chest => chest).ToList();
        return MergeStacks(slots);
    }

    /// <summary>
    /// merges every stack into the first stack of the same item that still has room
    /// </summary>
    /// <param name="slots"></param>
    /// <returns>how many merges were sent</returns>
    private int MergeStacks(List<ItemSlot> slots)
    {
        int merged = 0;

        for (int sinkIndex = 0; sinkIndex < slots.Count; ++sinkIndex)
        {
            ItemSlot sink = slots[sinkIndex];
            if (sink.Empty)
            {
                continue;
            }

            for (int sourceIndex = sinkIndex + 1; sourceIndex < slots.Count; ++sourceIndex)
            {
                ItemSlot source = slots[sourceIndex];

                if (source.Empty)
                {
                    continue;
                }

                if (sink.Itemstack.Collectible.GetMergableQuantity(sink.Itemstack, source.Itemstack, EnumMergePriority.AutoMerge) <= 0)
                {
                    continue;
                }

                if (slotMover.TryMerge(source, sink))
                {
                    ++merged;
                }
            }
        }
        
        return merged;
    }
}