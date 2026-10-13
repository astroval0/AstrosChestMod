using AstrosChestMod.Sorting;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace AstrosChestMod.Merging;

public class ItemSearch
{
    private readonly IWorldAccessor world;
    private readonly Dictionary<ItemSlot, (ItemStack? Stack, bool Found)> results = [];
    private string[] words = [];

    public string Text { get; private set; } = "";

    public bool IsEmpty => words.Length == 0;

    /// <summary>
    /// creates a search that doesnt search for anything yet
    /// </summary>
    /// <param name="world">needed to work out the category of an item</param>
    public ItemSearch(IWorldAccessor world)
    {
        this.world = world;
    }

    /// <summary>
    /// searches for something else. an item gets found when every word of the text is in its name or its category
    /// </summary>
    /// <param name="text">what got typed in the search box</param>
    public void SetText(string text)
    {
        Text = text;
        words = text.ToSearchFriendly().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        results.Clear();
    }

    /// <summary>
    /// checks if the item in a slot gets found. the answer is kept until something else lands in the slot, the slots get
    /// checked every frame
    /// </summary>
    /// <param name="slot">the slot</param>
    /// <returns>true if the item gets found, an empty slot never does</returns>
    public bool Finds(ItemSlot slot)
    {
        ItemStack? stack = slot.Itemstack;
        if (!results.TryGetValue(slot, out (ItemStack? Stack, bool Found) result) || result.Stack != stack)
        {
            result = (stack, stack != null && Matches(stack));
            results[slot] = result;
        }

        return result.Found;
    }

    /// <summary>
    /// checks if every word is in the name or the category of a stack
    /// </summary>
    /// <param name="stack">the stack</param>
    /// <returns>true if they all are</returns>
    private bool Matches(ItemStack stack)
    {
        ItemCategory category = ItemClassifier.GetCategory(stack, world);
        string categoryName = Lang.Get("astroschestmod:category-" + category.ToString().ToLowerInvariant());
        string searched = (stack.GetName() + " " + categoryName).ToSearchFriendly().ToLowerInvariant();
        return words.All(searched.Contains);
    }
}