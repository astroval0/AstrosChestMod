using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AstrosChestMod.Sorting;

public static class ItemClassifier
{
    private static readonly HashSet<EnumTool> WeaponTools =
    [
        EnumTool.Sword, EnumTool.Spear, EnumTool.Bow, EnumTool.Sling, EnumTool.Firearm, EnumTool.Crossbow, EnumTool.Javelin,
        EnumTool.Pike, EnumTool.Shield, EnumTool.Club, EnumTool.Mace, EnumTool.Warhammer, EnumTool.Poleaxe, EnumTool.Halberd, EnumTool.Polearm, EnumTool.Staff
    ];

    private static readonly string[] ArmourWords = ["armor", "helmet", "gambeson", "chainmail", "brigandine"];
    
    /// <summary>
    /// gets the category a stack gets sorted into
    /// </summary>
    /// <param name="stack">the stack to check</param>
    /// <param name="world">needed to look at food and meal content</param>
    /// <returns>the category, crafting materials if nothing else fits</returns>
    public static ItemCategory GetCategory(ItemStack stack, IWorldAccessor world)
    {
        CollectibleObject collectible = stack.Collectible;
        string family = GetFamily(stack);

        if (collectible.Tool is { } tool)
        {
            return WeaponTools.Contains(tool) ? ItemCategory.WeaponsAndArmour : ItemCategory.Tools;
        }

        if (IsFood(stack, world))
        {
            return ItemCategory.Food;
        }

        if (IsWearable(collectible))
        {
            return IsArmourFamily(family) ? ItemCategory.WeaponsAndArmour : ItemCategory.ClothingAndBags;
        }

        if (stack.Class == EnumItemClass.Block)
        {
            return ItemCategory.Building;
        }

        return ItemCategory.CraftingMaterials;
    }

    /// <summary>
    /// gets the family of the stack, which is the first part of its code, for example: ingot-copper and ingot-iron are both ingot
    /// </summary>
    /// <param name="stack">the stack to check</param>
    /// <returns>the family</returns>
    public static string GetFamily(ItemStack stack)
    {
        string path = stack.Collectible.Code.Path;
        int dash = path.IndexOf('-');
        return dash < 0 ? path : path[..dash];
    }

    /// <summary>
    /// checks if a stack is food, things like pots and bowls will only count if theres actual food in them
    /// </summary>
    private static bool IsFood(ItemStack stack, IWorldAccessor world)
    {
        CollectibleObject collectible = stack.Collectible;
        if (collectible is IBlockMealContainer mealContainer)
        {
            return mealContainer.GetNonEmptyContents(world, stack).Length > 0;
        }
        
        return collectible.GetNutritionProperties(world, stack, null) != null;
    }

    /// <summary>
    /// checks if an item can be worn
    /// </summary>
    private static bool IsWearable(CollectibleObject collectibleObject)
    {
        return collectibleObject.HasBehavior<CollectibleBehaviorWearable>(withInheritance: true);
    }

    /// <summary>
    /// checks if a wearable family is armor and not clothing
    /// </summary>
    private static bool IsArmourFamily(string family)
    {
        return ArmourWords.Any(family.Contains);
    }
} 