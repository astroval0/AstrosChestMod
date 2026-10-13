using AstrosChestMod.Config;
using AstrosChestMod.Inventory;
using AstrosChestMod.Sorting;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstrosChestMod;

public class AstrosChestModSystem : ModSystem
{
    private ChestSortFeature? chestSort;
    private ConfigFile? configFile;
    
    /// <summary>
    /// only loads on the client.
    /// </summary>
    /// <param name="forSide">the side that is loading mods</param>
    /// <returns>true on the client</returns>
    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }

    /// <summary>
    /// starts the features
    /// </summary>
    /// <param name="api">the client api</param>
    public override void StartClientSide(ICoreClientAPI api)
    {
        configFile = new ConfigFile(api, Mod);
        ChestOrder chestOrder = new(api, Mod);
        chestSort = new ChestSortFeature(api, configFile, chestOrder);
    }

    /// <summary>
    /// shutdown teh features
    /// </summary>
    public override void Dispose()
    {
        chestSort?.Dispose();
        configFile?.Dispose();
    }
}