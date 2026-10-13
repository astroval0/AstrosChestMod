namespace AstrosChestMod.Merging;

public static class WindowSpotFinder
{
    /// <summary>
    /// finds the spot closest to where the window wants to go that doesnt cover any of the taken spots and stays in the
    /// area. a free spot always lines up with something thats in the way or the edge of the area, so those are the only
    /// spots it tries
    /// </summary>
    /// <param name="wanted">where the window wants to go, with its size</param>
    /// <param name="taken">the spots of the windows it shouldnt cover</param>
    /// <param name="area">the part of the screen the window has to stay in</param>
    /// <param name="gap">the space to leave between two windows</param>
    /// <returns>the free spot, or the wanted spot when there is no free one</returns>
    public static WindowRect FindFreeSpot(WindowRect wanted, List<WindowRect> taken, WindowRect area, double gap)
    {
        List<double> xs = [wanted.X, area.X, area.Right - wanted.Width];
        List<double> ys = [wanted.Y, area.Y, area.Bottom - wanted.Height];
        foreach (WindowRect other in taken)
        {
            xs.Add(other.X - gap - wanted.Width);
            xs.Add(other.Right + gap);
            ys.Add(other.Y - gap - wanted.Height);
            ys.Add(other.Bottom + gap);
        }

        return xs.SelectMany(x => ys.Select(y => wanted with { X = x, Y = y }))
            .Where(spot => spot.FitsIn(area) && !taken.Any(spot.Overlaps))
            .DefaultIfEmpty(wanted)
            .MinBy(spot => Math.Pow(spot.X - wanted.X, 2) + Math.Pow(spot.Y - wanted.Y, 2));
    }
}