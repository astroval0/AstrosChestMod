using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstrosChestMod.Config;

public class ConfigFile
{
    private readonly ICoreClientAPI capi;
    private readonly ILogger logger;
    private readonly string fileName;

    public AstrosChestConfig Current { get; private set; }
    
    /// <summary>
    /// loads the config file and writes the defaults on first run
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="mod">the mod the config belongs to, its id is used for the file name</param>
    public ConfigFile(ICoreClientAPI capi, Mod mod)
    {
        this.capi = capi;
        logger = mod.Logger;
        fileName = mod.Info.ModID + ".json";
        Current = Load();
    }

    /// <summary>
    /// reads the config file and creates it with the defaults if it doesnt exist yet
    /// </summary>
    /// <returns>the loaded config, or the defaults if the file cant be read</returns>
    private AstrosChestConfig Load()
    {
        try
        {
            AstrosChestConfig? config = capi.LoadModConfig<AstrosChestConfig>(fileName);
            if (config != null)
            {
                return config;
            }
        }
        catch (Exception e)
        {
            logger.Warning("Could not read {0}, using the defaults until it's fixed: {1}", fileName, e.Message);
            return new AstrosChestConfig();
        }

        AstrosChestConfig defaults = new();
        capi.StoreModConfig(defaults, fileName);
        return defaults;
    }
}