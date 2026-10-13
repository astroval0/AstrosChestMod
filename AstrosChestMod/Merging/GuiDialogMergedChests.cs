using AstrosChestMod.Sorting;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AstrosChestMod.Merging;

public class GuiDialogMergedChests : GuiDialogBlockEntity
{
    private const string Title = "Chests";
    private const string InventoryToggleCode = "inventorydialog";
    private const double CheckboxSize = 18;
    private const double TitleHeight = 20;
    private const double CategoriesHeight = 20;
    private const double HeaderHeight = TitleHeight + CategoriesHeight;
    private const double SectionGap = 14;
    private const double InsetPadding = 6;
    private const double TitleBarSpace = 20;
    private const double ScrollbarSpace = 20;
    private const double InventoryGap = 6;
    private const double ScreenEdgeSpace = 100;
    private const double CreativeHotbarSpace = 105;
    private const double MinSectionSpace = 100;

    private readonly Action<MergedChest> onChestUnmerged;
    private readonly Action onWindowClosed;
    private List<MergedChest> chests = [];
    private string[] shownCategories = [];
    private ElementBounds? contentBounds;
    private (int Width, int Height, float Scale) composedScreen;

    public override double DrawOrder => 0.2;

    public IReadOnlyList<MergedChest> Chests => chests;

    /// <summary>
    /// creates the merged chest window, it stays hidden until it gets opened. its a block entity window so other mods
    /// treat it like a chest window, it gets moved onto one of its chests every time chests get shown
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="onChestUnmerged">called when the checkbox of a chest gets unticked</param>
    /// <param name="onWindowClosed">called when the window gets closed</param>
    public GuiDialogMergedChests(ICoreClientAPI capi, Action<MergedChest> onChestUnmerged, Action onWindowClosed)
        : base(Title, new BlockPos(0), capi)
    {
        this.onChestUnmerged = onChestUnmerged;
        this.onWindowClosed = onWindowClosed;
    }

    /// <summary>
    /// shows these chests in the window, one section per chest in the order of the list
    /// </summary>
    /// <param name="newChests">the chests to show</param>
    public void ShowChests(List<MergedChest> newChests)
    {
        chests = newChests;
        MoveOntoChest();
        ComposeWindow();
    }

    /// <summary>
    /// forgets the chests without touching them, for when they already got closed or moved somewhere else
    /// </summary>
    public void ClearChests()
    {
        chests = [];
    }

    /// <summary>
    /// keeps the window up to date. the categories follow what is in the chests, a new screen size gets a new layout and
    /// the inventory gets moved back under the window when it ends up on top of it
    /// </summary>
    public void Refresh()
    {
        if (SingleComposer == null)
        {
            return;
        }

        if (composedScreen != GetScreen())
        {
            ComposeWindow();
            return;
        }

        for (int i = 0; i < chests.Count; i++)
        {
            string categories = GetCategories(chests[i]);
            if (categories != shownCategories[i])
            {
                ShowCategories(i, categories);
            }
        }

        if (IsInventoryInTheWay())
        {
            PlaceAboveInventory();
        }
    }

    /// <summary>
    /// lets the feature close the chests once the window is gone, the base would only close the chest the window sits on
    /// </summary>
    public override void OnGuiClosed()
    {
        onWindowClosed();
    }

    /// <summary>
    /// does what every window does at the end of a frame but skips the reach check of the base, that one closes the whole
    /// window when the chest it sits on is out of reach. the feature checks every chest on its own
    /// </summary>
    /// <param name="dt">seconds since the last frame</param>
    public override void OnFinalizeFrame(float dt)
    {
        foreach (GuiComposer composer in Composers.Values)
        {
            composer.PostRender(dt);
        }
    }

    /// <summary>
    /// moves the block position of the window onto one of its chests. a chest with chest in its code gets picked when
    /// there is one, mods like xrb only count a window as a chest window if the block under it has chest in its code
    /// and trunks dont
    /// </summary>
    private void MoveOntoChest()
    {
        MergedChest? chest =
            chests.FirstOrDefault(merged => capi.World.BlockAccessor.GetBlock(merged.Pos).Code.Path.Contains("chest"))
            ?? chests.FirstOrDefault();

        if (chest != null)
        {
            BlockEntityPosition.Set(chest.Pos, chest.Pos.dimension);
        }
    }

    /// <summary>
    /// builds the window with a section for every chest, spread out so the window fits the screen. it scrolls when the
    /// sections still dont fit
    /// </summary>
    private void ComposeWindow()
    {
        composedScreen = GetScreen();
        double padding = GuiStyle.ElementToDialogPadding;
        (double frameWidth, double frameHeight) = GetFrameSize();
        (double maxWidth, double maxHeight) = GetSpaceForSections();
        SectionGrid grid = SectionGridPlanner.Plan(chests.Select(GetSectionSize).ToList(), maxWidth, maxHeight,
            frameWidth / frameHeight, SectionGap);

        double visibleHeight = Math.Min(grid.Height, maxHeight);
        bool scrolls = grid.Height > visibleHeight;

        ElementBounds visibleBounds = ElementBounds.Fixed(0, 0, grid.Width, visibleHeight);
        ElementBounds insetBounds =
            visibleBounds.ForkBoundingParent(InsetPadding, InsetPadding, InsetPadding, InsetPadding);
        ElementBounds dialogBounds = insetBounds
            .ForkBoundingParent(padding, padding + TitleBarSpace, padding + (scrolls ? ScrollbarSpace : 0), padding)
            .WithAlignment(EnumDialogArea.CenterMiddle);
        ElementBounds clipBounds = visibleBounds.CopyOffsetedSibling();
        contentBounds = ElementBounds.Fixed(0, 0, grid.Width, grid.Height);

        SingleComposer?.Dispose();
        GuiComposer composer = capi.Gui.CreateCompo("astroschestmod-mergedchests", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill)
            .AddDialogTitleBar(Title, OnTitleBarClose)
            .AddInset(insetBounds);

        if (scrolls)
        {
            composer.AddVerticalScrollbar(OnNewScrollbarValue,
                ElementStdBounds.VerticalScrollbar(insetBounds).WithParent(dialogBounds), "scrollbar");
        }

        composer.BeginClip(clipBounds).BeginChildElements(contentBounds);
        for (int i = 0; i < chests.Count; i++)
        {
            AddSection(composer, i, grid.Positions[i].X, grid.Positions[i].Y);
        }

        composer.EndChildElements().EndClip();
        SingleComposer = composer.Compose();

        shownCategories = new string[chests.Count];
        for (int i = 0; i < chests.Count; i++)
        {
            SingleComposer.GetSwitch("merged" + i).SetValue(true);
            ShowCategories(i, GetCategories(chests[i]));
        }

        if (scrolls)
        {
            SingleComposer.GetScrollbar("scrollbar").SetHeights((float)visibleHeight, (float)grid.Height);
        }

        PlaceAboveInventory();
    }

    /// <summary>
    /// adds the section of one chest, the checkbox and title go on top, the categories under them and the slots under that
    /// </summary>
    /// <param name="composer">the composer the window is being built with</param>
    /// <param name="index">which chest it is</param>
    /// <param name="x">where the section goes from the left</param>
    /// <param name="y">where the section goes from the top</param>
    private void AddSection(GuiComposer composer, int index, double x, double y)
    {
        MergedChest chest = chests[index];
        ElementBounds slotBounds = GetSlotGridBounds(chest, x, y + HeaderHeight);
        ElementBounds checkboxBounds = ElementBounds.Fixed(x, y + 1, CheckboxSize, CheckboxSize);
        ElementBounds titleBounds = ElementBounds.Fixed(x + CheckboxSize + 6, y,
            slotBounds.fixedWidth - CheckboxSize - 6, TitleHeight);
        ElementBounds categoriesBounds =
            ElementBounds.Fixed(x, y + TitleHeight, slotBounds.fixedWidth, CategoriesHeight);

        composer.AddSwitch(merge => OnMergeToggled(chest, merge), checkboxBounds, "merged" + index, CheckboxSize, 2)
            .AddHoverText("unmerge this chest", CairoFont.WhiteSmallText(), 200, checkboxBounds.FlatCopy())
            .AddStaticText(FitText(chest.Title, CairoFont.WhiteSmallText(), titleBounds.fixedWidth),
                CairoFont.WhiteSmallText(), titleBounds)
            .AddDynamicText(" ", CairoFont.WhiteDetailText(), categoriesBounds, "categories" + index)
            .AddHoverText(" ", CairoFont.WhiteSmallText(), 300, categoriesBounds.FlatCopy(), "categorieshover" + index)
            .AddItemSlotGrid(chest.Inventory, packet => SendChestPacket(chest, packet), chest.Columns, slotBounds,
                "slots" + index);
    }

    /// <summary>
    /// gets the slot grid of a chest, its as wide as the chest has columns
    /// </summary>
    /// <param name="chest">the chest</param>
    /// <param name="x">where the grid goes from the left</param>
    /// <param name="y">where the grid goes from the top</param>
    /// <returns>the bounds of the grid</returns>
    private static ElementBounds GetSlotGridBounds(MergedChest chest, double x, double y)
    {
        int rows = (int)Math.Ceiling(chest.Inventory.Count / (double)chest.Columns);
        return ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, chest.Columns, rows);
    }

    /// <summary>
    /// gets how much room the section of a chest takes up
    /// </summary>
    /// <param name="chest">the chest</param>
    /// <returns>the size of its slots with the header on top</returns>
    private static SectionSize GetSectionSize(MergedChest chest)
    {
        ElementBounds slotBounds = GetSlotGridBounds(chest, 0, 0);
        return new SectionSize(slotBounds.fixedWidth, HeaderHeight + slotBounds.fixedHeight);
    }

    /// <summary>
    /// works out how big the sections can get together. the window has to stay off the edges of the screen and fit above
    /// the players inventory, with room left at the top and bottom of the screen for the sort button and the hotbar
    /// </summary>
    /// <returns>the most width and height the sections can take up</returns>
    private (double Width, double Height) GetSpaceForSections()
    {
        double padding = GuiStyle.ElementToDialogPadding;
        double windowWidth = 2 * InsetPadding + 2 * padding + ScrollbarSpace;
        double windowHeight = 2 * InsetPadding + 2 * padding + TitleBarSpace;

        GuiComposer? inventory = FindInventoryComposer();
        double inventoryHeight =
            inventory == null ? 0 : inventory.Bounds.OuterHeight / RuntimeEnv.GUIScale + InventoryGap;

        (double frameWidth, double frameHeight) = GetFrameSize();
        double width = frameWidth - 2 * GuiStyle.DialogToScreenPadding - windowWidth;
        double height = frameHeight - 2 * ScreenEdgeSpace - inventoryHeight - windowHeight;
        return (Math.Max(width, MinSectionSpace), Math.Max(height, MinSectionSpace));
    }

    /// <summary>
    /// puts the window in the middle of the screen with the players inventory right under it, the same way xrb lines up
    /// chest windows so the two dont fight over where things go. the inventory only gets moved while its open because the
    /// game builds it again every time it opens. in creative the inventory stays at the bottom where the game has it
    /// </summary>
    private void PlaceAboveInventory()
    {
        if (SingleComposer == null || capi.Settings.Bool["immersiveMouseMode"])
        {
            return;
        }

        (double frameWidth, double frameHeight) = GetFrameSize();
        ElementBounds window = SingleComposer.Bounds;
        double windowWidth = window.OuterWidth / RuntimeEnv.GUIScale;
        double windowHeight = window.OuterHeight / RuntimeEnv.GUIScale;

        GuiDialog? inventoryWindow = FindInventoryWindow();
        GuiComposer? inventory = inventoryWindow?.Composers["maininventory"];
        if (inventoryWindow == null || inventory == null)
        {
            MoveTo(window, (frameWidth - windowWidth) / 2, (frameHeight - windowHeight) / 2);
            return;
        }

        double inventoryWidth = inventory.Bounds.OuterWidth / RuntimeEnv.GUIScale;
        double inventoryHeight = inventory.Bounds.OuterHeight / RuntimeEnv.GUIScale;
        double inventoryTop = IsCreative()
            ? frameHeight - CreativeHotbarSpace - inventoryHeight
            : (frameHeight + windowHeight + InventoryGap - inventoryHeight) / 2;
        double windowTop = Math.Max(0, inventoryTop - InventoryGap - windowHeight);

        MoveTo(window, (frameWidth - windowWidth) / 2, windowTop);
        if (inventoryWindow.IsOpened())
        {
            MoveTo(inventory.Bounds, (frameWidth - inventoryWidth) / 2, windowTop + windowHeight + InventoryGap);
        }
    }

    /// <summary>
    /// checks if the open player inventory is on top of this window, like when it just got opened and the game put it
    /// back in the middle of the screen
    /// </summary>
    /// <returns>true if they overlap</returns>
    private bool IsInventoryInTheWay()
    {
        GuiDialog? inventoryWindow = FindInventoryWindow();
        GuiComposer? inventory = inventoryWindow?.Composers["maininventory"];
        if (inventoryWindow == null || inventory == null || !inventoryWindow.IsOpened())
        {
            return false;
        }

        ElementBounds window = SingleComposer.Bounds;
        ElementBounds slots = inventory.Bounds;
        bool overlapX = window.absX < slots.absX + slots.OuterWidth && slots.absX < window.absX + window.OuterWidth;
        bool overlapY = window.absY < slots.absY + slots.OuterHeight && slots.absY < window.absY + window.OuterHeight;
        return overlapX && overlapY;
    }

    /// <summary>
    /// moves a window to a spot on the screen
    /// </summary>
    /// <param name="bounds">the bounds of the window</param>
    /// <param name="x">unscaled distance from the left of the screen</param>
    /// <param name="y">unscaled distance from the top of the screen</param>
    private static void MoveTo(ElementBounds bounds, double x, double y)
    {
        bounds.Alignment = EnumDialogArea.None;
        bounds.fixedX = x;
        bounds.fixedY = y;
        bounds.fixedOffsetX = 0;
        bounds.fixedOffsetY = 0;
        bounds.absMarginX = 0;
        bounds.absMarginY = 0;
        bounds.MarkDirtyRecursive();
        bounds.CalcWorldBounds();
    }

    /// <summary>
    /// finds the players inventory window
    /// </summary>
    /// <returns>the window, null if it isnt loaded</returns>
    private GuiDialog? FindInventoryWindow()
    {
        return capi.Gui.LoadedGuis.FirstOrDefault(dialog => dialog.ToggleKeyCombinationCode == InventoryToggleCode);
    }

    /// <summary>
    /// finds the part of the players inventory window with the slots in it, the game swaps it between creative and survival
    /// </summary>
    /// <returns>the composer, null if there is none</returns>
    private GuiComposer? FindInventoryComposer()
    {
        return FindInventoryWindow()?.Composers["maininventory"];
    }

    /// <summary>
    /// checks if the player is in creative, the inventory window looks different there
    /// </summary>
    /// <returns>true in creative</returns>
    private bool IsCreative()
    {
        return capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;
    }

    /// <summary>
    /// gets the size of the screen in the units the gui gets laid out in
    /// </summary>
    /// <returns>the unscaled width and height</returns>
    private (double Width, double Height) GetFrameSize()
    {
        return (capi.Render.FrameWidth / RuntimeEnv.GUIScale, capi.Render.FrameHeight / RuntimeEnv.GUIScale);
    }

    /// <summary>
    /// gets what the layout depends on, so it can be built again when the screen changes
    /// </summary>
    /// <returns>the screen size in pixels and the gui scale</returns>
    private (int Width, int Height, float Scale) GetScreen()
    {
        return (capi.Render.FrameWidth, capi.Render.FrameHeight, RuntimeEnv.GUIScale);
    }

    /// <summary>
    /// lists the categories of everything in a chest, in the order they get sorted in
    /// </summary>
    /// <param name="chest">the chest</param>
    /// <returns>the category names, or that its empty</returns>
    private string GetCategories(MergedChest chest)
    {
        List<string> names = chest.Inventory
            .Select(slot => slot.Itemstack)
            .OfType<ItemStack>()
            .Select(stack => ItemClassifier.GetCategory(stack, capi.World))
            .Distinct()
            .Order()
            .Select(category => Lang.Get("astroschestmod:category-" + category.ToString().ToLowerInvariant()))
            .ToList();

        return names.Count == 0 ? Lang.Get("astroschestmod:category-empty") : string.Join(", ", names);
    }

    /// <summary>
    /// writes the categories under the title of a chest, cut off when they dont fit. hovering over them shows all of them
    /// </summary>
    /// <param name="index">which chest it is</param>
    /// <param name="categories">the categories to show</param>
    private void ShowCategories(int index, string categories)
    {
        shownCategories[index] = categories;
        string fitted = FitText(categories, CairoFont.WhiteDetailText(),
            GetSlotGridBounds(chests[index], 0, 0).fixedWidth);
        SingleComposer.GetDynamicText("categories" + index).SetNewText(fitted);

        GuiElementHoverText hoverText = SingleComposer.GetHoverText("categorieshover" + index);
        hoverText.SetNewText(categories);
        hoverText.SetAutoDisplay(fitted != categories);
    }

    /// <summary>
    /// cuts text off with ... so it fits in a width
    /// </summary>
    /// <param name="text">the text</param>
    /// <param name="font">the font it gets drawn with</param>
    /// <param name="maxWidth">the unscaled width it has to fit in</param>
    /// <returns>the text, shortened if it was too wide</returns>
    private static string FitText(string text, CairoFont font, double maxWidth)
    {
        if (GetTextWidth(text, font) <= maxWidth)
        {
            return text;
        }

        string fitted = text;
        while (fitted.Length > 0 && GetTextWidth(fitted + "...", font) > maxWidth)
        {
            fitted = fitted[..^1];
        }

        return fitted.TrimEnd(' ', ',') + "...";
    }

    /// <summary>
    /// measures how wide text is
    /// </summary>
    /// <param name="text">the text</param>
    /// <param name="font">the font it gets drawn with</param>
    /// <returns>the unscaled width</returns>
    private static double GetTextWidth(string text, CairoFont font)
    {
        return font.GetTextExtents(text).Width / RuntimeEnv.GUIScale;
    }

    /// <summary>
    /// unmerges a chest when its checkbox gets unticked
    /// </summary>
    /// <param name="chest">the chest the checkbox belongs to</param>
    /// <param name="merge">false if the checkbox got unticked</param>
    private void OnMergeToggled(MergedChest chest, bool merge)
    {
        if (!merge)
        {
            onChestUnmerged(chest);
        }
    }

    /// <summary>
    /// sends a slot click to the chest it belongs to, the same way the games own chest window does
    /// </summary>
    /// <param name="chest">the chest the slot is in</param>
    /// <param name="packet">the slot packet</param>
    private void SendChestPacket(MergedChest chest, object packet)
    {
        capi.Network.SendBlockEntityPacket(chest.Pos.X, chest.Pos.InternalY, chest.Pos.Z, packet);
    }

    /// <summary>
    /// moves the chest sections when the scrollbar moves
    /// </summary>
    /// <param name="value">how far down the sections are scrolled</param>
    private void OnNewScrollbarValue(float value)
    {
        if (contentBounds == null)
        {
            return;
        }

        contentBounds.fixedY = -value;
        contentBounds.CalcWorldBounds();
    }

    /// <summary>
    /// closes the window from the x in the title bar
    /// </summary>
    private void OnTitleBarClose()
    {
        TryClose();
    }
}