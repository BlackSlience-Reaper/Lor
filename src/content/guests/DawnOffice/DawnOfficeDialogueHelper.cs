using System;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace LibraryOfRuina.content.guests.DawnOffice;

internal static class DawnOfficeDialogueHelper
{
    private static readonly MethodInfo? TalkPlayMethod = ResolveTalkPlayMethod();

    public static void Speak(MonsterModel speaker, string key)
    {
        if (speaker.Creature.IsDead)
        {
            return;
        }

        LocString line = MonsterModel.L10NMonsterLookup(key);
        InvokeTalkPlay(line, speaker.Creature);
    }

    public static void SpeakNonRepeating(MonsterModel speaker, string[] keys, ref int usedMask)
    {
        if (speaker.Creature.IsDead || keys.Length == 0)
        {
            return;
        }

        int index = PickNonRepeatingIndex(speaker, keys.Length, ref usedMask);
        if (index < 0)
        {
            return;
        }

        Speak(speaker, keys[index]);
    }

    public static bool IsOnlyLivingEnemy(MonsterModel monster)
    {
        if (monster.Creature.IsDead || monster.Creature.CombatState == null)
        {
            return false;
        }

        return monster.CombatState.Enemies.Count(enemy => !enemy.IsDead) == 1;
    }

    public static bool IsTeammateAlive<TMonster>(MonsterModel monster) where TMonster : MonsterModel
    {
        if (monster.Creature.CombatState == null)
        {
            return false;
        }

        return monster.CombatState.Enemies.Any(enemy => enemy.Monster is TMonster && !enemy.IsDead);
    }

    public static bool HasEscapedTeammate<TMonster>(MonsterModel monster) where TMonster : MonsterModel
    {
        if (monster.Creature.CombatState == null)
        {
            return false;
        }

        return monster.CombatState.EscapedCreatures.Any(creature => creature.Monster is TMonster);
    }

    private static int PickNonRepeatingIndex(MonsterModel speaker, int count, ref int usedMask)
    {
        int allUsedMask = (1 << count) - 1;
        usedMask &= allUsedMask;
        if (usedMask == allUsedMask)
        {
            return -1;
        }

        int start = speaker.Rng.NextInt(count);
        for (int offset = 0; offset < count; offset++)
        {
            int candidate = (start + offset) % count;
            int candidateBit = 1 << candidate;
            if ((usedMask & candidateBit) != 0)
            {
                continue;
            }

            usedMask |= candidateBit;
            return candidate;
        }

        return -1;
    }

    private static MethodInfo? ResolveTalkPlayMethod()
    {
        return typeof(TalkCmd)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(
                method =>
                {
                    if (method.Name != nameof(TalkCmd.Play))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length >= 2 &&
                        parameters[0].ParameterType == typeof(LocString) &&
                        parameters[1].ParameterType == typeof(Creature);
                });
    }

    private static void InvokeTalkPlay(LocString line, Creature speaker)
    {
        MethodInfo? method = TalkPlayMethod;
        if (method == null)
        {
            return;
        }

        ParameterInfo[] parameters = method.GetParameters();
        object?[] args = new object?[parameters.Length];
        args[0] = line;
        args[1] = speaker;

        for (int i = 2; i < parameters.Length; i++)
        {
            args[i] = GetParameterValue(parameters[i]);
        }

        method.Invoke(null, args);
    }

    private static object? GetParameterValue(ParameterInfo parameter)
    {
        Type type = parameter.ParameterType;
        if (type == typeof(VfxColor))
        {
            return VfxColor.White;
        }

        if (type == typeof(double))
        {
            return -1.0;
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, 0);
        }

        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
