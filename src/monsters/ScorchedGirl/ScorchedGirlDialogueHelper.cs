using System;
using System.Linq;
using System.Reflection;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace LibraryOfRuina.monsters.ScorchedGirl;

internal static class ScorchedGirlDialogueHelper
{
    private const double DialogueDurationSeconds = 2.0;
    private static readonly MethodInfo? TalkPlayMethod = ResolveTalkPlayMethod();

    public static void Speak(MonsterModel speaker, string key)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled || speaker.Creature.IsDead)
        {
            return;
        }

        LocString line = MonsterModel.L10NMonsterLookup(key);
        InvokeTalkPlay(line, speaker.Creature);
        MoonTextService.ShowLine(speaker.Creature, line);
    }

    public static void SpeakRandom(MonsterModel speaker, string[] keys)
    {
        if (speaker.Creature.IsDead || keys.Length == 0)
        {
            return;
        }

        int index = speaker.Rng.NextInt(keys.Length);
        Speak(speaker, keys[index]);
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
        if (type == typeof(double))
        {
            return DialogueDurationSeconds;
        }

        if (type == typeof(VfxColor))
        {
            return VfxColor.White;
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
