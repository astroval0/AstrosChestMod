using AstrosChestMod.Config;
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
    private const double MinContentSpace = 100;

    private readonly ConfigFile configFile;
    private readonly Action<List<MergedChest>> onChestsUnmerged;
    private readonly Action onWindowClosed;
    private readonly List<(ElementBounds Bounds, double Y)> slotGrids = [];
    private List<MergedChest> chests = [];
    private MergedChestInventory? oneChest;
    private List<InventoryBase> categoryInventories = [];
    private string[] shownCategories = [];
    private ElementBounds? contentBounds;
    private (int Width, int Height, float Scale) composedScreen;
    private bool composedAsOneChest;

    public override double DrawOrder => 0.2;

    public IReadOnlyList<MergedChest> Chests => chests;

    public MergedChestInventory? OneChest => oneChest;

    /// <summary>
    /// creates the merged chest window, it stays hidden until it gets opened. its a block entity window so other mods
    /// treat it like a chest window, it gets moved onto one of its chests every time chests get shown
    /// </summary>
    /// <param name="capi">the client api</param>
    /// <param name="configFile">the mod config, it decides if the chests get merged into one big chest</param>
    /// <param name="onChestsUnmerged">called with the chests whose checkbox got unticked</param>
    /// <param name="onWindowClosed">called when the window gets closed</param>
    public GuiDialogMergedChests(ICoreClientAPI capi, ConfigFile configFile,
        Action<List<MergedChest>> onChestsUnmerged, Action onWindowClosed)
        : base(Title, new BlockPos(0), capi)
    {
        this.configFile = configFile;
        this.onChestsUnmerged = onChestsUnmerged;
        this.onWindowClosed = onWindowClosed;
    }

    /// <summary>
    /// shows these chests in the window in the order of the list, one section per chest or all of them as one big chest
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
        oneChest?.Dispose();
        oneChest = null;
    }

    /// <summary>
    /// keeps the window up to date. the categories follow what is in the chests, a new screen size or switching to or
    /// from one big chest in the settings gets a new layout and the inventory gets moved back under the window when it
    /// ends up on top of it
    /// </summary>
    public void Refresh()
    {
        if (SingleComposer == null)
        {
            return;
        }

        if (composedScreen != GetScreen() || composedAsOneChest != configFile.Current.MergeIntoOneChest)
        {
            ComposeWindow();
            return;
        }

        for (int i = 0; i < categoryInventories.Count; i++)
        {
            string categories = GetCategories(categoryInventories[i]);
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
    /// gets rid of the window and stops the one big chest from following the chests
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        oneChest?.Dispose();
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
    /// builds the window again, as one big chest or with a section for every chest depending on the settings
    /// </summary>
    private void ComposeWindow()
    {
        composedScreen = GetScreen();
        composedAsOneChest = configFile.Current.MergeIntoOneChest;
        oneChest?.Dispose();
        oneChest = null;
        slotGrids.Clear();

        if (composedAsOneChest)
        {
            ComposeOneChest();
        }
        else
        {
            ComposeSections();
        }

        PlaceAboveInventory();
    }

    /// <summary>
    /// builds the window with a section for every chest, spread out so the window fits the screen
    /// </summary>
    private void ComposeSections()
    {
        (double maxWidth, double maxHeight) = GetSpaceForContent();
        List<SectionSize> sizes = chests.Select(GetSectionSize).ToList();
        SectionGrid grid = SectionGridPlanner.Plan(sizes, maxWidth, maxHeight, GetScreenAspectRatio(), SectionGap);

        GuiComposer composer = StartWindow(grid.Width, grid.Height, maxHeight);
        for (int i = 0; i < chests.Count; i++)
        {
            AddSectionHeader(composer, i, grid.Positions[i].X, grid.Positions[i].Y);
        }

        composer.EndChildElements();
        for (int i = 0; i < chests.Count; i++)
        {
            AddSectionSlots(composer, i, grid.Positions[i].X, grid.Positions[i].Y + HeaderHeight);
        }

        SingleComposer = composer.EndClip().Compose();
        SetUpScrollbar(grid.Height, maxHeight);

        for (int i = 0; i < chests.Count; i++)
        {
            SingleComposer.GetSwitch("merged" + i).SetValue(true);
        }

        categoryInventories = chests.Select(chest => chest.Inventory).ToList();
        ShowAllCategories();
    }

    /// <summary>
    /// builds the window as one big chest, the slots of every chest go in one grid with the categories of all of them on
    /// top and one checkbox in the title bar. clicks on a slot go to its chest through the one big chest, the grid only
    /// sends the packets for spreading a stack by dragging and those already say which chests they are for
    /// </summary>
    private void ComposeOneChest()
    {
        (double maxWidth, double maxHeight) = GetSpaceForContent();
        int slotCount = chests.Sum(chest => chest.Inventory.Count);
        int columns = PickOneChestColumns(slotCount, maxWidth, maxHeight);
        oneChest = new MergedChestInventory(capi, chests, columns, SendChestPacket);
        ElementBounds slotBounds = GetSlotGridBounds(slotCount, columns, 0, CategoriesHeight);
        double width = slotBounds.fixedWidth;
        double height = CategoriesHeight + slotBounds.fixedHeight;
        ElementBounds categoriesBounds = ElementBounds.Fixed(0, 0, width, CategoriesHeight);
        double titleWidth = GetTextWidth(Title, CairoFont.WhiteSmallText());
        ElementBounds checkboxBounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding + titleWidth + 8,
            (GuiStyle.TitleBarHeight - CheckboxSize) / 2, CheckboxSize, CheckboxSize);

        GuiComposer composer = StartWindow(width, height, maxHeight)
            .AddDynamicText(" ", CairoFont.WhiteDetailText(), categoriesBounds, "categories0")
            .AddHoverText(" ", CairoFont.WhiteSmallText(), 300, categoriesBounds.FlatCopy(), "categorieshover0")
            .EndChildElements();

        slotGrids.Add((slotBounds, CategoriesHeight));
        SingleComposer = composer.AddItemSlotGrid(oneChest, capi.Network.SendPacketClient, columns, slotBounds, "slots")
            .EndClip()
            .AddSwitch(OnOneChestMergeToggled, checkboxBounds, "mergedall", CheckboxSize, 2)
            .AddHoverText("unmerge these chests", CairoFont.WhiteSmallText(), 200, checkboxBounds.FlatCopy())
            .Compose();
        SetUpScrollbar(height, maxHeight);

        SingleComposer.GetSwitch("mergedall").SetValue(true);
        categoryInventories = [oneChest];
        ShowAllCategories();
    }

    /// <summary>
    /// starts building the window around content of this size, with the background, the title bar, the inset and a
    /// scrollbar when the content is taller than it can get. it ends inside the scrolling content, the caller adds the
    /// rest and closes the content and the clip
    /// </summary>
    /// <param name="width">how wide the content is</param>
    /// <param name="height">how tall the content is</param>
    /// <param name="maxHeight">how tall the content can get before it has to scroll</param>
    /// <returns>the composer</returns>
    private GuiComposer StartWindow(double width, double height, double maxHeight)
    {
        double padding = GuiStyle.ElementToDialogPadding;
        double visibleHeight = Math.Min(height, maxHeight);
        bool scrolls = height > visibleHeight;

        ElementBounds visibleBounds = ElementBounds.Fixed(0, 0, width, visibleHeight);
        ElementBounds insetBounds =
            visibleBounds.ForkBoundingParent(InsetPadding, InsetPadding, InsetPadding, InsetPadding);
        ElementBounds dialogBounds = insetBounds
            .ForkBoundingParent(padding, padding + TitleBarSpace, padding + (scrolls ? ScrollbarSpace : 0), padding)
            .WithAlignment(EnumDialogArea.CenterMiddle);
        ElementBounds clipBounds = visibleBounds.CopyOffsetedSibling();
        contentBounds = ElementBounds.Fixed(0, 0, width, height);

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

        return composer.BeginClip(clipBounds).BeginChildElements(contentBounds);
    }

    /// <summary>
    /// tells the scrollbar how much of the content fits, when the window has one
    /// </summary>
    /// <param name="height">how tall the content is</param>
    /// <param name="maxHeight">how tall the content can get before it has to scroll</param>
    private void SetUpScrollbar(double height, double maxHeight)
    {
        if (height > maxHeight)
        {
            SingleComposer.GetScrollbar("scrollbar").SetHeights((float)maxHeight, (float)height);
        }
    }

    /// <summary>
    /// picks how many columns the one big chest gets, the count that gets closest to the shape of the screen while still
    /// fitting in the space the window has
    /// </summary>
    /// <param name="slotCount">how many slots the chests have together</param>
    /// <param name="maxWidth">how wide the content can get</param>
    /// <param name="maxHeight">how tall the content can get before it has to scroll</param>
    /// <returns>the column count</returns>
    private int PickOneChestColumns(int slotCount, double maxWidth, double maxHeight)
    {
        List<SectionGrid> grids = [];
        for (int columns = 1; columns <= slotCount; columns++)
        {
            ElementBounds slotBounds = GetSlotGridBounds(slotCount, columns, 0, 0);
            grids.Add(new SectionGrid([(0, 0)], slotBounds.fixedWidth, CategoriesHeight + slotBounds.fixedHeight));
        }

        SectionGrid? best = SectionGridPlanner.PickBest(grids, maxWidth, maxHeight, GetScreenAspectRatio());
        return best == null ? 1 : grids.IndexOf(best) + 1;
    }

    /// <summary>
    /// adds the top of the section of one chest, the checkbox and title go on top and the categories under them. the
    /// title is dynamic text because static text gets drawn into the window once and wouldnt move when it scrolls
    /// </summary>
    /// <param name="composer">the composer the window is being built with</param>
    /// <param name="index">which chest it is</param>
    /// <param name="x">where the section goes from the left</param>
    /// <param name="y">where the section goes from the top</param>
    private void AddSectionHeader(GuiComposer composer, int index, double x, double y)
    {
        MergedChest chest = chests[index];
        double width = GetSlotGridBounds(chest.Inventory.Count, chest.Columns, 0, 0).fixedWidth;
        ElementBounds checkboxBounds = ElementBounds.Fixed(x, y + 1, CheckboxSize, CheckboxSize);
        ElementBounds titleBounds = ElementBounds.Fixed(x + CheckboxSize + 6, y, width - CheckboxSize - 6, TitleHeight);
        ElementBounds categoriesBounds = ElementBounds.Fixed(x, y + TitleHeight, width, CategoriesHeight);

        composer.AddSwitch(merge => OnMergeToggled(chest, merge), checkboxBounds, "merged" + index, CheckboxSize, 2)
            .AddHoverText("unmerge this chest", CairoFont.WhiteSmallText(), 200, checkboxBounds.FlatCopy())
            .AddDynamicText(FitText(chest.Title, CairoFont.WhiteSmallText(), titleBounds.fixedWidth),
                CairoFont.WhiteSmallText(), titleBounds)
            .AddDynamicText(" ", CairoFont.WhiteDetailText(), categoriesBounds, "categories" + index)
            .AddHoverText(" ", CairoFont.WhiteSmallText(), 300, categoriesBounds.FlatCopy(), "categorieshover" + index);
    }

    /// <summary>
    /// adds the slots of one chest. they go straight into the clip and get scrolled on their own instead of going in the
    /// scrolling content, a slot grid only checks its parent to see if the mouse is over it so it would show tooltips
    /// for slots that are scrolled out of view
    /// </summary>
    /// <param name="composer">the composer the window is being built with</param>
    /// <param name="index">which chest it is</param>
    /// <param name="x">where the slots go from the left</param>
    /// <param name="y">where the slots go from the top when nothing is scrolled</param>
    private void AddSectionSlots(GuiComposer composer, int index, double x, double y)
    {
        MergedChest chest = chests[index];
        ElementBounds slotBounds = GetSlotGridBounds(chest.Inventory.Count, chest.Columns, x, y);
        slotGrids.Add((slotBounds, y));
        composer.AddItemSlotGrid(chest.Inventory, packet => SendChestPacket(chest, packet), chest.Columns, slotBounds,
            "slots" + index);
    }

    /// <summary>
    /// gets the bounds of a slot grid with this many slots and columns
    /// </summary>
    /// <param name="slotCount">how many slots it has</param>
    /// <param name="columns">how many columns it has</param>
    /// <param name="x">where the grid goes from the left</param>
    /// <param name="y">where the grid goes from the top</param>
    /// <returns>the bounds of the grid</returns>
    private static ElementBounds GetSlotGridBounds(int slotCount, int columns, double x, double y)
    {
        int rows = (int)Math.Ceiling(slotCount / (double)columns);
        return ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, columns, rows);
    }

    /// <summary>
    /// gets how much room the section of a chest takes up
    /// </summary>
    /// <param name="chest">the chest</param>
    /// <returns>the size of its slots with the header on top</returns>
    private static SectionSize GetSectionSize(MergedChest chest)
    {
        ElementBounds slotBounds = GetSlotGridBounds(chest.Inventory.Count, chest.Columns, 0, 0);
        return new SectionSize(slotBounds.fixedWidth, HeaderHeight + slotBounds.fixedHeight);
    }

    /// <summary>
    /// works out how big the content of the window can get. the window has to stay off the edges of the screen and fit
    /// above the players inventory, with room left at the top and bottom of the screen for the sort button and the hotbar
    /// </summary>
    /// <returns>the most width and height the content can take up</returns>
    private (double Width, double Height) GetSpaceForContent()
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
        return (Math.Max(width, MinContentSpace), Math.Max(height, MinContentSpace));
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
    /// gets the width to height ratio of the screen, the window aims for the same shape
    /// </summary>
    /// <returns>the ratio</returns>
    private double GetScreenAspectRatio()
    {
        (double frameWidth, double frameHeight) = GetFrameSize();
        return frameWidth / frameHeight;
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
    /// shows the categories on every categories line, for a window that just got built
    /// </summary>
    private void ShowAllCategories()
    {
        shownCategories = new string[categoryInventories.Count];
        for (int i = 0; i < categoryInventories.Count; i++)
        {
            ShowCategories(i, GetCategories(categoryInventories[i]));
        }
    }

    /// <summary>
    /// lists the categories of everything in an inventory, in the order they get sorted in
    /// </summary>
    /// <param name="inventory">a chest or the one big chest</param>
    /// <returns>the category names, or that its empty</returns>
    private string GetCategories(InventoryBase inventory)
    {
        List<string> names = inventory
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
    /// writes the categories on a categories line, cut off when they dont fit. hovering over them shows all of them
    /// </summary>
    /// <param name="index">which categories line it is</param>
    /// <param name="categories">the categories to show</param>
    private void ShowCategories(int index, string categories)
    {
        shownCategories[index] = categories;
        GuiElementDynamicText text = SingleComposer.GetDynamicText("categories" + index);
        string fitted = FitText(categories, CairoFont.WhiteDetailText(), text.Bounds.fixedWidth);
        text.SetNewText(fitted);

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
            onChestsUnmerged([chest]);
        }
    }

    /// <summary>
    /// unmerges every chest when the checkbox of the one big chest gets unticked
    /// </summary>
    /// <param name="merge">false if the checkbox got unticked</param>
    private void OnOneChestMergeToggled(bool merge)
    {
        if (!merge)
        {
            onChestsUnmerged([.. chests]);
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
    /// moves the content when the scrollbar moves, the slot grids get moved on their own
    /// </summary>
    /// <param name="value">how far down the content is scrolled</param>
    private void OnNewScrollbarValue(float value)
    {
        if (contentBounds == null)
        {
            return;
        }

        contentBounds.fixedY = -value;
        contentBounds.CalcWorldBounds();

        foreach ((ElementBounds bounds, double y) in slotGrids)
        {
            bounds.fixedY = y - value;
            bounds.CalcWorldBounds();
        }
    }

    /// <summary>
    /// closes the window from the x in the title bar
    /// </summary>
    private void OnTitleBarClose()
    {
        TryClose();
    }
}