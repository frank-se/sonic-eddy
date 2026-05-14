namespace Fr.Wireplumber.Helper;

internal static class TagHelper
{
    internal static string GenerateTag() => Guid.NewGuid().ToString("N")[^12..];
}