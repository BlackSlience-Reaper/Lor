using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
public static class HarborMistDodgePatch
{
    private static readonly object Sync = new();
    private static readonly Dictionary<CardModel, int> PendingDodges = new();
    private static readonly Dictionary<CardModel, int> ResolvedDodges = new();

    private static readonly MethodInfo? OnPlayMethod =
        AccessTools.Method(typeof(CardModel), "OnPlay", new[] { typeof(PlayerChoiceContext), typeof(CardPlay) });

    private static readonly MethodInfo InvokeOnPlayOrSkipMethod =
        AccessTools.Method(typeof(HarborMistDodgePatch), nameof(InvokeOnPlayOrSkip))!;

    public static void MarkCardPlayForDodge(CardModel card)
    {
        lock (Sync)
        {
            PendingDodges.TryGetValue(card, out int pending);
            PendingDodges[card] = pending + 1;
        }
    }

    public static bool IsCardPlayMarkedForDodge(CardModel card)
    {
        lock (Sync)
        {
            return PendingDodges.TryGetValue(card, out int pending) && pending > 0;
        }
    }

    public static bool TryConsumeResolvedDodge(CardModel card)
    {
        lock (Sync)
        {
            if (!ResolvedDodges.TryGetValue(card, out int resolved) || resolved <= 0)
            {
                return false;
            }

            if (resolved == 1)
            {
                ResolvedDodges.Remove(card);
            }
            else
            {
                ResolvedDodges[card] = resolved - 1;
            }

            return true;
        }
    }

    public static void ClearCardMarks(CardModel card)
    {
        lock (Sync)
        {
            PendingDodges.Remove(card);
            ResolvedDodges.Remove(card);
        }
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (OnPlayMethod == null)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
            }

            yield break;
        }

        foreach (CodeInstruction instruction in instructions)
        {
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo method
                && method == OnPlayMethod)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = InvokeOnPlayOrSkipMethod;
            }

            yield return instruction;
        }
    }

    public static Task InvokeOnPlayOrSkip(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ConsumeCardPlayMark(card))
        {
            MarkResolvedDodge(card);
            return Task.CompletedTask;
        }

        if (OnPlayMethod == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            object? result = OnPlayMethod.Invoke(card, new object?[] { choiceContext, cardPlay });
            return result as Task ?? Task.CompletedTask;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void MarkResolvedDodge(CardModel card)
    {
        lock (Sync)
        {
            ResolvedDodges.TryGetValue(card, out int resolved);
            ResolvedDodges[card] = resolved + 1;
        }
    }

    private static bool ConsumeCardPlayMark(CardModel card)
    {
        lock (Sync)
        {
            if (!PendingDodges.TryGetValue(card, out int pending) || pending <= 0)
            {
                return false;
            }

            if (pending == 1)
            {
                PendingDodges.Remove(card);
            }
            else
            {
                PendingDodges[card] = pending - 1;
            }

            return true;
        }
    }
}
