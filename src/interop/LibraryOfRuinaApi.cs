using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop.catalog;
using LibraryOfRuina.interop.descriptors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.interop;

public static class LibraryOfRuinaApi
{
    public const string ModId = "LibraryOfRuina";
    public const string ApiVersion = "1.1.0";

    public static LibraryOfRuinaModelCatalog Models => LibraryOfRuinaInteropRegistry.Models;

    public static LibraryOfRuinaIntentCatalog Intents => LibraryOfRuinaInteropRegistry.Intents;

    public static IReadOnlyList<LibraryOfRuinaInteropDescriptor> AllDescriptors =>
        LibraryOfRuinaInteropRegistry.AllDescriptors;

    public static bool TryGetDescriptor(string publicId, out LibraryOfRuinaInteropDescriptor descriptor)
    {
        return LibraryOfRuinaInteropRegistry.TryGetDescriptor(publicId, out descriptor!);
    }

    public static bool TryGetRawModelId(string publicId, out ModelId modelId)
    {
        if (LibraryOfRuinaInteropRegistry.TryGetModelDescriptor(publicId, out LibraryOfRuinaInteropDescriptor descriptor) &&
            descriptor.RawModelId != null)
        {
            modelId = descriptor.RawModelId;
            return true;
        }

        modelId = ModelId.none;
        return false;
    }

    public static bool TryGetCanonicalModel(string publicId, out AbstractModel model)
    {
        model = null!;
        if (!TryGetRawModelId(publicId, out ModelId modelId))
        {
            return false;
        }

        AbstractModel? resolved = ModelDb.GetByIdOrNull<AbstractModel>(modelId);
        if (resolved == null)
        {
            return false;
        }

        model = resolved;
        return true;
    }

    public static bool TryCreateMutableModel(string publicId, out AbstractModel model)
    {
        model = null!;
        if (!TryGetCanonicalModel(publicId, out AbstractModel canonicalModel))
        {
            return false;
        }

        // 各类模型的 ToMutable 还会设置原型引用等状态，通用浅克隆无法替代这些初始化。
        model = canonicalModel switch
        {
            AfflictionModel affliction => affliction.ToMutable(),
            CardModel card => card.ToMutable(),
            EnchantmentModel enchantment => enchantment.ToMutable(),
            EncounterModel encounter => encounter.ToMutable(),
            EventModel eventModel => eventModel.ToMutable(),
            MonsterModel monster => monster.ToMutable(),
            PowerModel power => power.ToMutable(),
            RelicModel relic => relic.ToMutable(),
            _ => null!
        };
        return model != null;
    }

    public static bool TryGetAffliction(string publicId, out AfflictionModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Afflictions, out model!);
    }

    public static bool TryGetCard(string publicId, out CardModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Cards, out model!);
    }

    public static bool TryGetEnchantment(string publicId, out EnchantmentModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Enchantments, out model!);
    }

    public static bool TryGetEncounter(string publicId, out EncounterModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Encounters, out model!);
    }

    public static bool TryGetEvent(string publicId, out EventModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Events, out model!);
    }

    public static bool TryGetAncient(string publicId, out AncientEventModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Ancients, out model!);
    }

    public static bool TryGetAncientEvent(string publicId, out AncientEventModel model)
    {
        return TryGetAncient(publicId, out model!);
    }

    public static bool TryGetMonster(string publicId, out MonsterModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Monsters, out model!);
    }

    public static bool TryGetPower(string publicId, out PowerModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Powers, out model!);
    }

    public static bool TryGetRelic(string publicId, out RelicModel model)
    {
        return TryGetTypedModel(publicId, LibraryOfRuinaInteropCategory.Relics, out model!);
    }

    public static bool TryApplyPower(
        string publicId,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? source,
        bool silent = false)
    {
        if (target == null)
        {
            return false;
        }

        if (!TryGetPower(publicId, out _))
        {
            return false;
        }

        _ = TaskHelper.RunSafely(ApplyPowerAndLogFailure(
            publicId,
            target,
            amount,
            applier,
            source,
            silent));
        return true;
    }

    public static async Task<bool> TryApplyPowerAsync(
        string publicId,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? source,
        bool silent = false)
    {
        if (target == null
            || !TryGetPower(publicId, out PowerModel canonicalPower))
        {
            return false;
        }

        try
        {
            await PowerCmdCompat.Apply(
                canonicalPower.ToMutable(),
                target,
                amount,
                applier,
                source,
                silent);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[LibraryOfRuina.Interop] Failed to apply power "
                + publicId
                + ": "
                + exception);
            return false;
        }
    }

    public static bool TryApplyEnchantment(string publicId, CardModel card, decimal amount)
    {
        if (card == null)
        {
            return false;
        }

        if (!TryGetEnchantment(publicId, out EnchantmentModel canonicalEnchantment))
        {
            return false;
        }

        try
        {
            CardCmd.Enchant(canonicalEnchantment.ToMutable(), card, amount);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[LibraryOfRuina.Interop] Failed to apply enchantment "
                + publicId
                + ": "
                + exception);
            return false;
        }
    }

    private static async Task ApplyPowerAndLogFailure(
        string publicId,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? source,
        bool silent)
    {
        await TryApplyPowerAsync(
            publicId,
            target,
            amount,
            applier,
            source,
            silent);
    }

    private static bool TryGetTypedModel<TModel>(
        string publicId,
        LibraryOfRuinaInteropCategory expectedCategory,
        out TModel model)
        where TModel : AbstractModel
    {
        model = null!;
        if (!LibraryOfRuinaInteropRegistry.TryGetModelDescriptor(publicId, out LibraryOfRuinaInteropDescriptor descriptor))
        {
            return false;
        }

        if (descriptor.Category != expectedCategory || descriptor.RawModelId == null)
        {
            return false;
        }

        TModel? resolved = ModelDb.GetByIdOrNull<TModel>(descriptor.RawModelId);
        if (resolved == null)
        {
            return false;
        }

        model = resolved;
        return true;
    }
}
