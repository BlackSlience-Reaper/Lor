using LibraryOfRuina.interop.descriptors;

namespace LibraryOfRuina.interop.catalog;

public sealed class LibraryOfRuinaModelCatalog
{
    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Afflictions { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Cards { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Enchantments { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Encounters { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Events { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Ancients { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Monsters { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Powers { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Relics { get; }

    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> All { get; }

    internal LibraryOfRuinaModelCatalog(
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> afflictions,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> cards,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> enchantments,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> encounters,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> events,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> ancients,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> monsters,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> powers,
        IReadOnlyList<LibraryOfRuinaInteropDescriptor> relics)
    {
        Afflictions = afflictions;
        Cards = cards;
        Enchantments = enchantments;
        Encounters = encounters;
        Events = events;
        Ancients = ancients;
        Monsters = monsters;
        Powers = powers;
        Relics = relics;

        All =
        [
            .. Afflictions,
            .. Cards,
            .. Enchantments,
            .. Encounters,
            .. Events,
            .. Ancients,
            .. Monsters,
            .. Powers,
            .. Relics
        ];
    }
}

public sealed class LibraryOfRuinaIntentCatalog
{
    public IReadOnlyList<LibraryOfRuinaInteropDescriptor> Entries { get; }

    internal LibraryOfRuinaIntentCatalog(IReadOnlyList<LibraryOfRuinaInteropDescriptor> entries)
    {
        Entries = entries;
    }
}
