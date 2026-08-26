using System.Collections.Generic;
using System.Linq;

namespace SonicEddy.Services.SoomfonDeck;

/// <summary>
/// Logical-key layout of the 3x6 grid used by the M/E switcher feature -
/// indices 5/11/17 (the display-only strip, confirmed empirically to never
/// generate button events) are deliberately absent from every row here.
/// </summary>
public static class SoomfonDeckLayout
{
    public static readonly int[] ProgramRow = [0, 1, 2, 3, 4];
    public static readonly int[] PreviewRow = [6, 7, 8, 9, 10];
    public static readonly int[] ObjectRow = [12, 13, 14, 15, 16];

    public static IEnumerable<int> AllInteractiveKeys =>
        ProgramRow.Concat(PreviewRow).Concat(ObjectRow);
}
