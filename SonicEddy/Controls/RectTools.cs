using Avalonia;

namespace SonicEddy.Controls;

public static class RectTools
{
    public static bool IsInvalidRect(this Rect rect)
    {
        return rect.Width < 0 || double.IsNaN(rect.Width) ||
               double.IsInfinity(rect.Width) ||
               rect.Height < 0 || double.IsNaN(rect.Height) ||
               double.IsInfinity(rect.Height) ||
               double.IsNaN(rect.X) || double.IsInfinity(rect.X) ||
               double.IsNaN(rect.Y) || double.IsInfinity(rect.Y);
    }
}