using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace AstrosChestMod.Merging;

public class ChestMergeFeature : IDisposable
{
    private const string CheckboxKey = "astroschestmod-merge";
    private const double CheckboxSize = 18;

    private readonly ICoreClientAPI capi;
    private bool mergeWindows;

    public static ChestMergeFeature? Active { get; private set; }

    /// <summary>
    /// sets up merging chest windows, the harmony patches get to it through Active
    /// </summary>
    /// <param name="capi">the client api</param>
    public ChestMergeFeature(ICoreClientAPI capi)
    {
        this.capi = capi;
        Active = this;
    }

    /// <summary>
    /// stops the harmony patches from using this
    /// </summary>
    public void Dispose()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    /// <summary>
    /// adds the merge checkbox to a chest window, right after its title
    /// </summary>
    /// <param name="window">the chest window</param>
    /// <param name="title">the title of the chest window</param>
    public void AddMergeCheckbox(GuiDialogBlockEntityInventory window, string title)
    {
        if (window.IsDuplicate ||
            capi.World.BlockAccessor.GetBlockEntity(window.BlockEntityPosition) is not BlockEntityGenericTypedContainer)
        {
            return;
        }

        double titleWidth = CairoFont.WhiteSmallText().GetTextExtents(title).Width / RuntimeEnv.GUIScale;
        ElementBounds bounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding + titleWidth + 8,
            (GuiStyle.TitleBarHeight - CheckboxSize) / 2, CheckboxSize, CheckboxSize);

        GuiElementSwitch checkbox = new(capi, OnMergeToggled, bounds, CheckboxSize, 2);
        checkbox.SetValue(mergeWindows);

        GuiComposer composer = window.SingleComposer;
        composer.AddInteractiveElement(checkbox, CheckboxKey);
        composer.AddInteractiveElement(new GuiElementHoverText(capi, "merge chest windows", CairoFont.WhiteSmallText(),
            200, bounds.FlatCopy()));
        composer.ReCompose();
    }

    /// <summary>
    /// turns merging on or off when a merge checkbox gets clicked
    /// </summary>
    /// <param name="merge">true if the checkbox got checked</param>
    private void OnMergeToggled(bool merge)
    {
        mergeWindows = merge;
        capi.ShowChatMessage(merge ? "merging chest windows" : "not merging chest windows");
    }
}