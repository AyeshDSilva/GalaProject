namespace GalaReconciler.Core.Domain;

/// <summary>
/// Naming for guests without a name (an extra ticket in Source, a "guest" entry in Processed).
/// Both parsers use this so the two sides produce the same match key.
/// </summary>
public static class UnnamedGuest
{
    public static string DisplayName(string hostDisplayName, int index) =>
        index <= 1 ? $"Unnamed guest of {hostDisplayName}" : $"Unnamed guest {index} of {hostDisplayName}";

    public static string MatchKey(string hostMatchKey, int index) =>
        index <= 1 ? $"unnamed guest of {hostMatchKey}" : $"unnamed guest {index} of {hostMatchKey}";
}
