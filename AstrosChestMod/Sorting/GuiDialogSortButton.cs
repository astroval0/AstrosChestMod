using Vintagestory.API.Client;

namespace AstrosChestMod.Sorting;

public class GuiDialogSortButton : GuiDialog
{
    private readonly Action onSortClicked;
    
    public override string? ToggleKeyCombinationCode => null;
    
    public override EnumDialogType DialogType => EnumDialogType.HUD;
    
    public override bool PrefersUngrabbedMouse => false;

    /// <summary>
    /// builds the button, it stays hidden until it gets opened
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="onSortClicked">called when the button is clicked</param>
    public GuiDialogSortButton(ICoreClientAPI capi, Action onSortClicked) : base(capi)
    {
        this.onSortClicked = onSortClicked;

        ElementBounds buttonBounds = ElementBounds.Fixed(0, 0, 180, 30);
        ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);
        backgroundBounds.BothSizing = ElementSizing.FitToChildren;
        backgroundBounds.WithChildren(buttonBounds);

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterTop).WithFixedAlignmentOffset(0, GuiStyle.DialogToScreenPadding);
        
        SingleComposer = capi.Gui.CreateCompo("astroschestmod-sortbutton", dialogBounds)
            .AddShadedDialogBG(backgroundBounds, false).AddSmallButton("Sort Chests", OnSortButtonClicked, buttonBounds)
            .Compose();
    }

    /// <summary>
    /// runs the sort callback
    /// </summary>
    /// <returns>true so the click doesnt go through to whatever is under the button</returns>
    private bool OnSortButtonClicked()
    {
        onSortClicked();
        return true;
    }
}