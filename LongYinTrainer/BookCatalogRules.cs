using System;

internal static class BookCatalogRules
{
    // hide controls native discovery, not whether a definition can back a book.
    internal static bool IsEligible(int id, int type, string? name) =>
        id >= 0 && type >= 0 && type <= 8 && !string.IsNullOrWhiteSpace(name);

    internal static bool Matches(int type, int rarity, int typeFilter, int rarityFilter) =>
        (typeFilter < 0 || type == typeFilter) && (rarityFilter < 0 || rarity == rarityFilter);

    internal static int Compare(int typeA, int rarityA, int typeB, int rarityB)
    {
        var typeOrder = typeA.CompareTo(typeB);
        return typeOrder != 0 ? typeOrder : rarityB.CompareTo(rarityA);
    }
}
