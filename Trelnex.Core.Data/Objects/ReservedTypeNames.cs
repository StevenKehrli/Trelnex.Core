namespace Trelnex.Core.Data;

public static class ReservedTypeNames
{
    public static readonly string Event = "event";

    private static readonly string[] _reservedTypeNames = [Event];

    public static bool IsReserved(
        string typeName)
    {
        return _reservedTypeNames.Any(rtn => string.Equals(rtn, typeName));
    }
}
