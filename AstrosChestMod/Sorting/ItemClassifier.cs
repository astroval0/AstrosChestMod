using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AstrosChestMod.Sorting;

public static class ItemClassifier
{
    private static readonly HashSet<EnumTool> WeaponTools =
    [
        EnumTool.Sword, EnumTool.Spear, EnumTool.Bow, EnumTool.Sling, EnumTool.Firearm, EnumTool.Crossbow,
        EnumTool.Javelin,
        EnumTool.Pike, EnumTool.Shield, EnumTool.Club, EnumTool.Mace, EnumTool.Warhammer, EnumTool.Poleaxe,
        EnumTool.Halberd, EnumTool.Polearm, EnumTool.Staff
    ];

    private static readonly string[] ArmourWords = ["armor", "helmet", "gambeson", "chainmail", "brigandine"];
    private static readonly string[] OreGrades = ["poor", "medium", "rich", "bountiful"];
    private static readonly Dictionary<string, FamilyInfo> Families = BuildFamilies();

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

        if (Families.TryGetValue(family, out FamilyInfo info))
        {
            return info.Category;
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
    /// gets where a family goes inside its category
    /// </summary>
    /// <param name="family">the family from <see cref="GetFamily"/></param>
    /// <returns>the position in the family table, int.MaxValue for families that arent in it so they go last</returns>
    public static int GetFamilyRank(string family)
    {
        return Families.TryGetValue(family, out FamilyInfo info) ? info.Rank : int.MaxValue;
    }

    /// <summary>
    /// gets the key that orders stacks inside one family. for example: ores go by ore then grade from poor to bountiful.
    /// </summary>
    /// <param name="stack">the stack to check</param>
    /// <returns>a key meant for ordinal string comparison</returns>
    public static string GetWithinFamilyKey(ItemStack stack)
    {
        CollectibleObject collectible = stack.Collectible;
        string? ore = collectible.Variant["ore"];

        if (ore == null)
        {
            return collectible.Code.Path;
        }

        int grade = Array.IndexOf(OreGrades, collectible.Variant["grade"]);
        return $"{ore}|{(grade < 0 ? OreGrades.Length : grade)}|{collectible.Variant["rock"]}|{collectible.Code.Path}";
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

    private static Dictionary<string, FamilyInfo> BuildFamilies()
    {
        Dictionary<string, FamilyInfo> families = new();

        void Add(ItemCategory category, params string[] codes)
        {
            foreach (string family in codes)
            {
                families.TryAdd(family, new FamilyInfo(category, families.Count));
            }
        }

        Add(ItemCategory.Valuables,
            "gear", "gem", "diamond", "amethyst", "clearquartz", "rosequartz", "smokyquartz", "jonasparts",
            "jonasframes", "rustypart", "eidolongearbox", "lore", "book", "letter", "envelope"
        );

        Add(ItemCategory.Tools,
            "axe", "pickaxe", "shovel", "hoe", "knife", "hammer", "saw", "chisel", "scythe", "shears",
            "prospectingpick",
            "tongs", "tongsmetal", "wrench", "crowbar", "cleaver", "snowshovel", "firestarter", "bugnet", "fishingpole",
            "fishinghook", "wormgrunter", "inkandquill", "padlock", "plumbandsquare", "punchset", "solderingiron",
            "sewingkit", "scrapweaponkit", "locatormap", "oar",
            "axehead", "pickaxehead", "shovelhead", "hoehead", "knifeblade", "hammerhead", "sawblade", "scythehead",
            "prospectingpickhead", "cleaverhead", "helvehammerhead"
        );

        Add(ItemCategory.WeaponsAndArmour,
            "blade", "spear", "bow", "sling", "club", "shield", "beenade",
            "arrow", "bullets", "arrowhead", "spearhead", "bladehead", "bowstave", "armor"
        );

        Add(ItemCategory.ClothingAndBags,
            "clothes", "backpack", "hunterbackpack", "basket", "linensack", "miningbag", "miningbagsturdy", "quiver",
            "glider", "hoovedwearables", "nightvisiondevice"
        );

        Add(ItemCategory.Food,
            "flour", "dough", "spice", "salt", "honeycomb"
        );

        Add(ItemCategory.FarmingAndPlants,
            "seeds", "treeseed", "sapling", "fruitingbushcutting", "compost", "bonemeal", "potash", "fishingbait",
            "flower", "fern", "tallfern", "tallgrass", "tallplant", "herb", "herbbundle", "mushroom", "waterlily",
            "seaweed", "aquatic", "lichen", "hanginglichen", "barrelcactus", "saguarocactus", "silvertorchcactus",
            "leaves", "leavesbranchy", "leavesnarrow", "bambooleaves", "fallenleaves", "wildvine",
            "cattailroot", "cattailtops", "papyrusroot", "papyrustops", "tuleroot", "hay", "skep", "wildbeehive",
            "henbox", "trough", "clayplanter", "flowerpot", "wateringcan"
        );

        Add(ItemCategory.OresAndMetals,
            "ore", "crystalizedore", "looseores", "crushed", "powder", "nugget", "ingot", "ironbloom", "metalplate",
            "metalsheet", "metalbit", "metalchain", "metallamellae", "metalscale", "metalnailsandstrips", "solderbar",
            "metalblock", "ingotpile", "platepile", "metalpartpile", "saltpeter"
        );

        Add(ItemCategory.Fuel,
            "firewood", "agedfirewood", "charcoal", "coal", "coke", "peatbrick", "firewoodpile", "coalpile"
        );

        Add(ItemCategory.CraftingMaterials,
            "stick", "flint", "drygrass", "flaxfibers", "flaxtwine", "rope", "cloth", "hide", "leather", "fat", "bone",
            "antler", "tusk", "feather", "beeswax", "resin", "oakbark", "clay", "lime", "quicklime", "calcined",
            "blastingpowder", "plantdebris", "pulp", "rot"
        );

        Add(ItemCategory.Building,
            "log", "logsection", "debarkedlog", "planks", "plank", "plankslab", "plankstairs", "stone", "rock",
            "cobblestone", "stonebricks", "stonebrick", "rawbrick", "burnedbrick", "claybricks", "refractorybrick",
            "refractorybricks", "claytile", "tile", "shingle", "thatch", "mortar", "daubraw", "bamboostakes",
            "glass", "glasspane", "sand", "gravel", "soil"
        );

        Add(ItemCategory.ContainersAndCookware,
            "bowl", "claypot", "dirtyclaypot", "crock", "jug", "woodbucket", "barrel", "storagevessel", "crucible",
            "ingotmold", "toolmold", "pan", "chest", "trunk", "labeledchest", "crate", "woodencrate",
            "stationarybasket", "baskettrap", "sieve", "lootvessel"
        );

        Add(ItemCategory.Lighting,
            "torch", "lantern", "oillamp", "candle", "bunchocandles", "chandelier", "paperlantern", "torchholder"
        );

        return families;
    }

    private readonly record struct FamilyInfo(ItemCategory Category, int Rank);
}