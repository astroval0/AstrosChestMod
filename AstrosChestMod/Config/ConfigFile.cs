using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstrosChestMod.Config;

public class ConfigFile : IDisposable
{
    private readonly ICoreClientAPI capi;
    private readonly ILogger logger;
    private readonly string fileName;
    private readonly EventBusListenerDelegate configLibSavedListener;

    public AstrosChestConfig Current { get; private set; }

    /// <summary>
    /// loads the config file and writes the defaults on first run and starts listening for ConfigLib saves
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="mod">the mod the config belongs to, its id is used for the file name</param>
    public ConfigFile(ICoreClientAPI capi, Mod mod)
    {
        this.capi = capi;
        logger = mod.Logger;
        fileName = mod.Info.ModID + ".json";
        Current = Load();

        configLibSavedListener = OnConfigLibSaved;
        capi.Event.RegisterEventBusListener(configLibSavedListener, 0.5, $"configlib:{mod.Info.ModID}:config-saved");
    }

    /// <summary>
    /// stops listening for configlib saves
    /// </summary>
    public void Dispose()
    {
        capi.Event.UnregisterEventBusListener(configLibSavedListener);
    }

    /// <summary>
    /// writes the config back to the file, for settings that get changed in game
    /// </summary>
    public void Save()
    {
        try
        {
            capi.StoreModConfig(Current, fileName);
        }
        catch (Exception e)
        {
            logger.Warning("Could not save {0}: {1}", fileName, e.Message);
        }
    }

    /// <summary>
    /// reloads the config once configlib has written the new values to the file
    /// </summary>
    private void OnConfigLibSaved(string eventName, ref EnumHandling handling, IAttribute data)
    {
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