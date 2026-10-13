namespace AstrosChestMod.Merging;

public static class SectionGridPlanner
{
    /// <summary>
    /// spreads the chest sections over rows, it picks the row count that gets closest to the shape of the screen while
    /// still fitting in the space the window has. rows get filled evenly and sections in the same column line up
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
        for (int rows = 1; rows <= sections.Count; rows++)
        {
            grids.Add(Build(sections, rows, gap));
        }

        return grids.Where(grid => grid.Width <= maxWidth && grid.Height <= maxHeight)
                   .MinBy(grid => Math.Abs(Math.Log(grid.Width / grid.Height / aspectRatio)))
               ?? grids.Where(grid => grid.Width <= maxWidth).MinBy(grid => grid.Height)
               ?? grids.MinBy(grid => grid.Width)
               ?? new SectionGrid([], 0, 0);
    }

    /// <summary>
    /// lays the sections out over this many rows, the first rows get one more section when they dont split evenly
    /// </summary>
    /// <param name="sections">the size of every section</param>
    /// <param name="rows">how many rows to use</param>
    /// <param name="gap">the space between two sections</param>
    /// <returns>the grid</returns>
    private static SectionGrid Build(List<SectionSize> sections, int rows, double gap)
    {
        List<(int Row, int Column)> cells = [];
        for (int row = 0; row < rows; row++)
        {
            int count = sections.Count / rows + (row < sections.Count % rows ? 1 : 0);
            for (int column = 0; column < count; column++)
            {
                cells.Add((row, column));
            }
        }

        double[] columnWidths = new double[cells.Max(cell => cell.Column) + 1];
        double[] rowHeights = new double[rows];
        for (int i = 0; i < sections.Count; i++)
        {
            (int row, int column) = cells[i];
            columnWidths[column] = Math.Max(columnWidths[column], sections[i].Width);
            rowHeights[row] = Math.Max(rowHeights[row], sections[i].Height);
        }

        List<(double X, double Y)> positions = cells
            .Select(cell => (columnWidths.Take(cell.Column).Sum() + cell.Column * gap,
                rowHeights.Take(cell.Row).Sum() + cell.Row * gap))
            .ToList();

        return new SectionGrid(positions, columnWidths.Sum() + (columnWidths.Length - 1) * gap,
            rowHeights.Sum() + (rows - 1) * gap);
    }
}