namespace AstrosChestMod.Merging;

public readonly record struct WindowRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>
    /// checks if this overlaps another rect, rects that only touch dont count
    /// </summary>
    /// <param name="other">the other rect</param>
    /// <returns>true if they overlap</returns>
    public bool Overlaps(WindowRect other)
    {
        return X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
    }

    /// <summary>
    /// checks if this fits completely inside another rect
    /// </summary>
    /// <param name="other">the other rect</param>
    /// <returns>true if it fits</returns>
    public bool FitsIn(WindowRect other)
    {
        return X >= other.X && Right <= other.Right && Y >= other.Y && Bottom <= other.Bottom;
    }
}