using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstrosChestMod.Inventory;

public class ChestOrder
{
    private readonly ICoreClientAPI capi;
    private readonly ILogger logger;
    private readonly string dataFolder;
    private List<BlockPos>? knownChests;

    /// <summary>
    /// creates the chest order, the saved order gets loaded the first time its needed
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="mod">the mod the order belongs to, its id is used for the save folder</param>
    public ChestOrder(ICoreClientAPI capi, Mod mod)
    {
        this.capi = capi;
        logger = mod.Logger;
        dataFolder = mod.Info.ModID;
    }

    /// <summary>
    /// puts the chests in the order they were first sorted in, chests that were never sorted before go last in the order they were opened and keep that spot from then on
    /// </summary>
    /// <param name="chests">the open chests, in the order they were opened</param>
    /// <returns>the same chests in their remembered order</returns>
    public List<InventoryBase> Arrange(List<InventoryBase> chests)
    {
        List<BlockPos> known = knownChests ??= Load();

        bool addedChest = false;
        foreach (InventoryBase chest in chests)
        {
            if (!known.Contains(chest.Pos))
            {
                known.Add(chest.Pos.Copy());
                addedChest = true;
            }
        }

        if (addedChest)
        {
            Save(known);
        }

        return chests.OrderBy(chest => known.IndexOf(chest.Pos)).ToList();
    }

    /// <summary>
    /// gets the file the order is saved in, theres one per world
    /// </summary>
    private string GetSavePath()
    {
        return Path.Combine(capi.GetOrCreateDataPath(Path.Combine("ModData", dataFolder)), capi.World.SavegameIdentifier + ".json");
    }

    /// <summary>
    /// reads the saved order for this world
    /// </summary>
    /// <returns>the saved chest positions, empty if nothing is saved yet or the file cant be read</returns>
    private List<BlockPos> Load()
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            int[][]? saved = JsonSerializer.Deserialize<int[][]>(File.ReadAllText(path));
            return saved?.Select(pos => new BlockPos(pos[0], pos[1], pos[2], pos[3])).ToList() ?? [];
        }
        catch (Exception e)
        {
            logger.Warning("Could not read the chest order in {0}, starting a new one: {1}", path, e.Message);
            return [];
        }
    }

    /// <summary>
    /// writes the order for this world
    /// </summary>
    /// <param name="known">the chest positions in their order</param>
    private void Save(List<BlockPos> known)
    {
        int[][] saved = known.Select(pos => new[] { pos.X, pos.Y, pos.Z, pos.dimension }).ToArray();

        try
        {
            File.WriteAllText(GetSavePath(), JsonSerializer.Serialize(saved));
        }
        catch (Exception e)
        {
            logger.Warning("Could not save the chest order: {0}", e.Message);
        }
    }
}