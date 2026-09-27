using System;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.events.AltarEnchantment;



internal static class AltarEnchantmentHelper
{
    internal readonly record struct PoolEntry(string EnchantmentTypeName, int MinAmount, int MaxAmount);

    private static readonly string EnchantmentCategory = ModelId.SlugifyCategory<EnchantmentModel>();
    private static readonly Dictionary<string, EnchantmentModel?> CanonicalByEntryCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingEntriesLogged = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ResolveFailuresLogged = new(StringComparer.Ordinal);
    private static readonly HashSet<string> CanEnchantFailuresLogged = new(StringComparer.Ordinal);

    internal static readonly PoolEntry[] NormalPool =
    [
        new PoolEntry("Sharp", 1, 3),
        new PoolEntry("Steady", 1, 1),
        new PoolEntry("Swift", 1, 3),
        new PoolEntry("Nimble", 1, 4),
        new PoolEntry("Vigorous", 6, 8),
        new PoolEntry("Goopy", 1, 1),
        new PoolEntry("Imbued", 1, 1),
        new PoolEntry("PerfectFit", 1, 1),
        new PoolEntry("Slither", 1, 1),
        new PoolEntry("Corrupted", 1, 1),
        new PoolEntry("TezcatarasEmber", 1, 1)
    ];

    internal static readonly PoolEntry[] AdvancedPool =
    [
        new PoolEntry("Nimble", 3, 5),
        new PoolEntry("Sharp", 3, 6),
        new PoolEntry("Swift", 2, 4),
        new PoolEntry("Instinct", 1, 1),
        new PoolEntry("Momentum", 4, 7),
        new PoolEntry("Glam", 1, 1),
        new PoolEntry("Favored", 1, 1),
        new PoolEntry("SlumberingEssence", 1, 1),
        new PoolEntry("SoulsPower", 1, 1),
        new PoolEntry("Sown", 2, 3),
        new PoolEntry("Spiral", 1, 1),
        new PoolEntry("RoyallyApproved", 1, 1)
    ];

    internal static bool CanAnyEnchant(IEnumerable<PoolEntry> pool, CardModel card)
    {
        foreach (PoolEntry entry in pool)
        {
            EnchantmentModel? canon = ResolveCanonical(entry);
            if (canon != null && CanEnchantSafely(canon, card))
            {
                return true;
            }
        }

        return false;
    }

    internal static List<CardModel> ListEnchantableDeckCards(Player player, PoolEntry[] pool)
    {
        return PileType.Deck.GetPile(player).Cards
            .Where(c => c.Enchantment == null && CanAnyEnchant(pool, c))
            .ToList();
    }

    internal static int RollAmount(Rng rng, int min, int max)
    {
        if (min >= max)
        {
            return min;
        }

        var values = Enumerable.Range(min, max - min + 1).ToList();
        int? picked = rng.WeightedNextItem(values, v => max - v + 1f);
        return picked ?? min;
    }

    
    
    
    
    internal static void EnchantRandomFromPool(Rng rng, CardModel card, PoolEntry[] pool)
    {
        var workable = pool
            .Select(entry => new { Entry = entry, Canon = ResolveCanonical(entry) })
            .Where(x => x.Canon != null && CanEnchantSafely(x.Canon, card))
            .Select(x => (x.Entry, Canon: x.Canon!))
            .ToList();

        if (workable.Count == 0)
        {
            return;
        }

        
        rng.Shuffle(workable);
        var entry = workable[rng.NextInt(workable.Count)];
        int amount = RollAmount(rng, entry.Entry.MinAmount, entry.Entry.MaxAmount);
        CardCmd.Enchant(entry.Canon.ToMutable(), card, amount);
    }

    private static EnchantmentModel? ResolveCanonical(PoolEntry entry)
    {
        string modelEntry = StringHelper.Slugify(entry.EnchantmentTypeName);
        if (CanonicalByEntryCache.TryGetValue(modelEntry, out EnchantmentModel? cached))
        {
            return cached;
        }

        EnchantmentModel? resolved = null;
        try
        {
            resolved = ModelDb.GetByIdOrNull<EnchantmentModel>(new ModelId(EnchantmentCategory, modelEntry));
            if (resolved == null && MissingEntriesLogged.Add(modelEntry))
            {
                Log.Warn(
                    $"[AncientMagicAltar] Enchantment '{modelEntry}' not found in current game build. " +
                    "This pool entry will be skipped.");
            }
        }
        catch (Exception exception)
        {
            if (ResolveFailuresLogged.Add(modelEntry))
            {
                Log.Warn(
                    $"[AncientMagicAltar] Failed to resolve enchantment '{modelEntry}'. " +
                    $"This pool entry will be skipped. {exception}");
            }
        }

        CanonicalByEntryCache[modelEntry] = resolved;
        return resolved;
    }

    private static bool CanEnchantSafely(EnchantmentModel enchantment, CardModel card)
    {
        try
        {
            return enchantment.CanEnchant(card);
        }
        catch (Exception exception)
        {
            if (CanEnchantFailuresLogged.Add(enchantment.Id.Entry))
            {
                Log.Warn(
                    $"[AncientMagicAltar] CanEnchant failed for '{enchantment.Id.Entry}'. " +
                    $"This enchantment will be skipped for altar rolls. {exception}");
            }

            return false;
        }
    }
}
