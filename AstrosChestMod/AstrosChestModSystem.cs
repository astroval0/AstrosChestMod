using Vintagestory.API.Common;

namespace AstrosChestMod;

public class AstrosChestModSystem : ModSystem
{
    /// <summary>
    /// only loads on the client.
    /// </summary>
    /// <param name="forSide">the side that is loading mods</param>
    /// <returns>true on the client</returns>
    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }
}