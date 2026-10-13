using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstrosChestMod.Merging;

public class GuiElementSearchSlotGrid : GuiElementItemSlotGrid
{
    private const float DimDepth = 250;
    private const double DimAlpha = 0.6;

    private readonly ItemSearch search;
    private LoadedTexture dimTexture;

    /// <summary>
    /// creates a slot grid that dims every slot the search doesnt find
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="inventory">the inventory the slots are in</param>
    /// <param name="sendPacket">sends the packets for clicks on the slots</param>
    /// <param name="columns">how many columns the grid has</param>
    /// <param name="bounds">the bounds of the grid</param>
    /// <param name="search">the search of the window the grid is in</param>
    public GuiElementSearchSlotGrid(ICoreClientAPI capi, IInventory inventory, Action<object> sendPacket, int columns,
        ElementBounds bounds, ItemSearch search)
        : base(capi, inventory, sendPacket, columns, null, bounds)
    {
        this.search = search;
        dimTexture = new LoadedTexture(capi);
    }

    /// <summary>
    /// builds the slots and the dark square that goes over the slots the search doesnt find, it has the same shape as a
    /// slot
    /// </summary>
    /// <param name="ctx">the context of the window background</param>
    /// <param name="surface">the window background</param>
    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        base.ComposeElements(ctx, surface);

        int size = (int)scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
        using ImageSurface dimSurface = new(Format.Argb32, size, size);
        using Context dimContext = genContext(dimSurface);
        dimContext.SetSourceRGBA(0, 0, 0, DimAlpha);
        RoundRectangle(dimContext, 0, 0, size, size, GuiStyle.ElementBGRadius);
        dimContext.Fill();
        generateTexture(dimSurface, ref dimTexture);
    }

    /// <summary>
    /// draws the slots and puts the dark square over the ones the search doesnt find while something is searched. the
    /// square goes in front of the items and their stack size but behind hover texts and the item on the mouse, the same
    /// depth the game draws a slot that cant be used at
    /// </summary>
    /// <param name="deltaTime">seconds since the last frame</param>
    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);
        if (search.IsEmpty)
        {
            return;
        }

        int index = 0;
        foreach ((int _, ItemSlot slot) in renderedSlots)
        {
            ElementBounds slotBounds = SlotBounds[index++];
            if (search.Finds(slot) || !slotBounds.PartiallyInside(Bounds.ParentBounds))
            {
                continue;
            }

            api.Render.Render2DTexturePremultipliedAlpha(dimTexture.TextureId, slotBounds, DimDepth);
        }
    }

    /// <summary>
    /// gets rid of the textures of the slots and the dark square
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        dimTexture.Dispose();
    }
}