using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstrosChestMod.Merging;

public class MergedChestGroups
{
    private readonly ICoreClientAPI capi;
    private readonly ILogger logger;
    private readonly string dataFolder;
    private List<List<BlockPos>>? groups;

    /// <summary>
    /// creates the groups of merged chests, the saved groups get loaded the first time theyre needed
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="mod">the mod the groups belong to, its id is used for the save folder</param>
    public MergedChestGroups(ICoreClientAPI capi, Mod mod)
    {
        this.capi = capi;
        logger = mod.Logger;
        dataFolder = mod.Info.ModID;
    }

    /// <summary>
    /// finds the group a chest is in, opening one chest of a group opens the rest of it too
    /// </summary>
    /// <param name="pos">where the chest is</param>
    /// <returns>the positions of every chest in the group, null if the chest isnt in one</returns>
    public IReadOnlyList<BlockPos>? FindGroup(BlockPos pos)
    {
        return GetGroups().FirstOrDefault(group => group.Contains(pos));
    }

    /// <summary>
    /// puts a chest in one group with the chests it gets merged with. groups that had any of them in it become one
    /// group, so a chest is never in two groups
    /// </summary>
    /// <param name="pos">where the chest is</param>
    /// <param name="mergedWith">where the chests it gets merged with are</param>
    public void Merge(BlockPos pos, List<BlockPos> mergedWith)
    {
        List<List<BlockPos>> all = GetGroups();
        List<BlockPos> chests = [pos, .. mergedWith];
        List<List<BlockPos>> joined = all.Where(group => group.Any(chests.Contains)).ToList();

        all.RemoveAll(joined.Contains);
        all.Add(joined.SelectMany(group => group).Concat(chests).Distinct().Select(chest => chest.Copy()).ToList());
        Save(all);
    }

    /// <summary>
    /// takes a chest out of its group so it opens on its own again
    /// </summary>
    /// <param name="pos">where the chest is</param>
    public void Remove(BlockPos pos)
    {
        List<List<BlockPos>> all = GetGroups();
        List<BlockPos>? group = all.FirstOrDefault(group => group.Contains(pos));
        if (group == null)
        {
            return;
        }

        group.Remove(pos);
        if (group.Count == 0)
        {
            all.Remove(group);
        }

        Save(all);
    }

    /// <summary>
    /// gets the groups of this world, they get loaded the first time
    /// </summary>
    /// <returns>the groups</returns>
    private List<List<BlockPos>> GetGroups()
    {
        return groups ??= Load();
    }

    /// <summary>
    /// gets the file the groups are saved in, theres one per world next to the chest order
    /// </summary>
    /// <returns>the path of the file</returns>
    private string GetSavePath()
    {
        string folder = capi.GetOrCreateDataPath(Path.Combine("ModData", dataFolder));
        return Path.Combine(folder, capi.World.SavegameIdentifier + "-merged.json");
    }

    /// <summary>
    /// reads the groups of this world
    /// </summary>
    /// <returns>the saved groups, empty if nothing is saved yet or the file cant be read</returns>
    private List<List<BlockPos>> Load()
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            int[][][]? saved = JsonSerializer.Deserialize<int[][][]>(File.ReadAllText(path));
            return saved?.Select(group => group.Select(pos => new BlockPos(pos[0], pos[1], pos[2], pos[3])).ToList())
                .ToList() ?? [];
        }
        catch (Exception e)
        {
            logger.Warning("Could not read the merged chests in {0}, starting over: {1}", path, e.Message);
            return [];
        }
    }

    /// <summary>
    /// writes the groups of this world
    /// </summary>
    /// <param name="all">the groups</param>
    private void Save(List<List<BlockPos>> all)
    {
        int[][][] saved = all
            .Select(group => group.Select(pos => new[] { pos.X, pos.Y, pos.Z, pos.dimension }).ToArray())
            .ToArray();

        try
        {
            File.WriteAllText(GetSavePath(), JsonSerializer.Serialize(saved));
        }
        catch (Exception e)
        {
            logger.Warning("Could not save the merged chests: {0}", e.Message);
        }
    }
}