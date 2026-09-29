using System.Collections.Generic;
using LibraryLib.Combat;
using LibraryLib.Commands;
using LibraryLib.Utils.Resistance;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.content.specialguests.Iori;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace LibraryOfRuina.interop;

/// <summary>
/// 本模组接入 LibraryOfRuinaLib 公开扩展点的地方（伤害类型改判、攻击可选目标过滤）。
/// 注册只在初始化时做一次；两端加载同一份代码，注册顺序相同。
/// </summary>
internal static class LibraryExtensionPoints
{
    internal static void Register()
    {
        LibraryDamageTypes.RegisterModifier(DamageTypes.Instance);
        LibraryAttackTargets.RegisterFilter(AttackTargets.Instance);
    }

    private sealed class DamageTypes : ILibraryDamageTypeModifier
    {
        internal static readonly DamageTypes Instance = new();

        public LibraryDamageType ModifyDamageType(in LibraryDamageTypeContext context, LibraryDamageType current)
        {
            // 伊织的攻击类型由她同步过的行动计划决定。
            if (context.VanillaCommand?.Attacker?.Monster is IoriMonsterBase iori)
            {
                return iori.ResolveActiveOrPreviewDamageType();
            }

            // 绞杀藤附魔：带它的卡改判为穿刺，预览与实战一致。
            return context.Card?.Enchantment is StranglingVineEnchantment ? LibraryDamageType.Pierce : current;
        }
    }

    private sealed class AttackTargets : ILibraryAttackTargetFilter
    {
        internal static readonly AttackTargets Instance = new();

        // 与原来 TargetingDispatch 里这一段的调用顺序相同。
        public IReadOnlyList<Creature> FilterTargets(LibraryAttackCommand command, IReadOnlyList<Creature> targets)
        {
            FriendlyAllyLibraryAttackTargetsPatch.FilterLibraryAttackTargets(command, ref targets);
            MagicBulletShooterLibraryAttackTargetsPatch.FilterLibraryAttackTargets(command, ref targets);
            return targets;
        }
    }
}
