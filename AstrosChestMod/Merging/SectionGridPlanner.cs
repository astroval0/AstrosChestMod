namespace AstrosChestMod.Merging;

public static class SectionGridPlanner
{
    /// <summary>
    /// spreads the chest sections over rows, it picks the row count that gets closest to the shape of the screen while
    /// still fitting in the space the window has. every row gets laid out on its own so small chests can share a row
    /// above a big one, and for every row count the rows are kept as narrow as they can so they come out even
    /// </summary>
    /// <param name="sections">the size of every section in the order they get shown</param>
    /// <param name="maxWidth">how wide the sections can get together</param>
    /// <param name="maxHeight">how tall the sections can get together before the window has to scroll</param>
    /// <param name="aspectRatio">the width to height ratio to aim for</param>
    /// <param name="gap">the space between two sections</param>
    /// <returns>where every section goes, when nothing fits its the shortest grid that still fits the width</returns>
    public static SectionGrid Plan(List<SectionSize> sections, double maxWidth, double maxHeight, double aspectRatio,
        double gap)
    {
        List<SectionGrid> grids = [];
        int rowCount = 0;
        foreach (double rowWidth in GetRowWidths(sections, gap))
        {
            List<int> rowSizes = FillRows(sections, rowWidth, gap);
            if (rowSizes.Count != rowCount)
            {
                grids.Add(Build(sections, rowSizes, gap));
                rowCount = rowSizes.Count;
            }
        }

        return PickBest(grids, maxWidth, maxHeight, aspectRatio) ?? new SectionGrid([], 0, 0);
    }

    /// <summary>
    /// picks the grid that gets closest to the shape of the screen while still fitting in the space the window has
    /// </summary>
    /// <param name="grids">the grids to pick from</param>
    /// <param name="maxWidth">how wide the grid can get</param>
    /// <param name="maxHeight">how tall the grid can get before the window has to scroll</param>
    /// <param name="aspectRatio">the width to height ratio to aim for</param>
    /// <returns>the grid, when nothing fits its the shortest one that still fits the width. null if there are none</returns>
    private static SectionGrid? PickBest(List<SectionGrid> grids, double maxWidth, double maxHeight, double aspectRatio)
    {
        return grids.Where(grid => grid.Width <= maxWidth && grid.Height <= maxHeight)
                   .MinBy(grid => Math.Abs(Math.Log(grid.Width / grid.Height / aspectRatio)))
               ?? grids.Where(grid => grid.Width <= maxWidth).MinBy(grid => grid.Height)
               ?? grids.MinBy(grid => grid.Width);
    }

    /// <summary>
    /// lists every width a row can end up with, a row is always a run of sections that come right after each other
    /// </summary>
    /// <param name="sections">the size of every section</param>
    /// <param name="gap">the space between two sections</param>
    /// <returns>the widths from narrowest to widest</returns>
    private static List<double> GetRowWidths(List<SectionSize> sections, double gap)
    {
        SortedSet<double> widths = [];
        for (int first = 0; first < sections.Count; first++)
        {
            double width = -gap;
            for (int last = first; last < sections.Count; last++)
            {
                width += gap + sections[last].Width;
                widths.Add(width);
            }
        }

        return widths.ToList();
    }

    /// <summary>
    /// fills the rows from the top, a section goes on the next row when it would make the current one too wide. a section
    /// thats too wide on its own still gets a row
    /// </summary>
    /// <param name="sections">the size of every section</param>
    /// <param name="maxRowWidth">how wide a row can get</param>
    /// <param name="gap">the space between two sections</param>
    /// <returns>how many sections go on each row</returns>
    private static List<int> FillRows(List<SectionSize> sections, double maxRowWidth, double gap)
    {
        List<int> rowSizes = [];
        double rowWidth = 0;
        foreach (SectionSize section in sections)
        {
            if (rowSizes.Count > 0 && rowWidth + gap + section.Width <= maxRowWidth)
            {
                rowSizes[^1]++;
                rowWidth += gap + section.Width;
                continue;
            }

            rowSizes.Add(1);
            rowWidth = section.Width;
        }

        return rowSizes;
    }

    /// <summary>
    /// lays the sections out row by row, every row starts on the left and is as tall as its tallest section
    /// </summary>
    /// <param name="sections">the size of every section</param>
    /// <param name="rowSizes">how many sections go on each row</param>
    /// <param name="gap">the space between two sections</param>
    /// <returns>the grid</returns>
    private static SectionGrid Build(List<SectionSize> sections, List<int> rowSizes, double gap)
    {
        List<(double X, double Y)> positions = [];
        double width = 0;
        double y = 0;
        int first = 0;
        foreach (int rowSize in rowSizes)
        {
            List<SectionSize> row = sections.GetRange(first, rowSize);
            double x = 0;
            foreach (SectionSize section in row)
            {
                positions.Add((x, y));
                x += section.Width + gap;
            }

            width = Math.Max(width, x - gap);
            y += row.Max(section => section.Height) + gap;
            first += rowSize;
        }

        return new SectionGrid(positions, width, y - gap);
    }
}