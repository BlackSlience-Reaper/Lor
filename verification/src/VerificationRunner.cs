using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.features.temporarymaps;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 验证程序集入口。每个套件仍按自己的 <c>--lor-verify-*</c> 参数决定是否启动；
/// 这里只保留一个主菜单补丁按原来的顺序依次询问各套件。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class VerificationRunner
{
    private const string VerifyArgPrefix = "lor-verify-";
    private const string SecondAscensionUiCheckArg = "lor-second-ascension-ui-check";

    private static readonly Action[] Suites =
    [
        AllyModelRulesVerificationPatch.Start,
        AllyTurnProviderVerificationPatch.Start,
        ArtFloorLiberationVerificationPatch.Start,
        BigBadWolfTargetingVerificationPatch.Start,
        BlueStarStrongVerificationPatch.Start,
        CodeHealthVerificationPatch.Start,
        EgoCardPreviewVerificationPatch.Start,
        EnemyCardIntentVerificationPatch.Start,
        FairyMassCareVerificationPatch.Start,
        GalaxyDoomVerificationPatch.Start,
        JudgementBirdVerificationPatch.Start,
        KaliSpecialGuestContractVerificationPatch.Start,
        KingOfGreedPageStunVerificationPatch.Start,
        KingOfGreedSummonVerificationPatch.Start,
        LanguageFloorLiberationPhaseFiveVerificationPatch.Start,
        LanguageFloorLiberationPhaseFourVerificationPatch.Start,
        LanguageFloorLiberationPhaseThreeVerificationPatch.Start,
        LanguageFloorLiberationVerificationPatch.Start,
        LiberationBossMapIconVerificationPatch.Start,
        LiberationCombatEndGuardVerificationPatch.Start,
        LiberationEncounterStateVerificationPatch.Start,
        LiberationPhaseBossTransitionVerificationPatch.Start,
        LiteratureFloorLiberationPhaseFiveVerificationPatch.Start,
        LiteratureFloorLiberationPhaseFourVerificationPatch.Start,
        LiteratureFloorLiberationPhaseOneVerificationPatch.Start,
        LiteratureFloorLiberationPhaseThreeVerificationPatch.Start,
        LiteratureFloorLiberationPhaseTwoVerificationPatch.Start,
        LiteratureFloorLiberationSettlementVerificationPatch.Start,
        MatchMarkMultiplayerVerificationPatch.Start,
        NosferatuDoomLockVerificationPatch.Start,
        PageRelicPipelineVerificationPatch.Start,
        PhilosophyFloorLiberationVerificationPatch.Start,
        PowerIconVerificationPatch.Start,
        QueenOfHatredPageMultiplayerVerificationPatch.Start,
        RelicRunHistoryDescriptionVerificationPatch.Start,
        SceneBackedAbnormalityAnimationVerificationPatch.Start,
        SmilingBodiesAnimationVerificationPatch.Start,
        SmilingBodiesDeathVerificationPatch.Start,
        SmilingBodiesPageMultiplayerVerificationPatch.Start,
        SocialFloorLiberationVerificationPatch.Start,
        SpecialGuestXiaoContractVerificationPatch.Start,
        TargetedIntentLineVerificationPatch.Start,
        TechnologyFloorLiberationSettlementVerificationPatch.Start,
        XiaoIntentAnchorVerificationPatch.Start,
    ];

    public static void Initialize()
    {
        if (Environment.GetCommandLineArgs().Any(static arg =>
                arg.TrimStart('-').StartsWith(VerifyArgPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            TemporaryMapSessionManager.PreserveSessionsOnRunStarted = true;
        }

        new Harmony("LibraryOfRuina.Verification").CreateClassProcessor(typeof(MainMenuReadyPatch)).Patch();
        Log.Info("[LibraryOfRuina.Verification] Loaded " + Suites.Length + " suites.");
    }

    [HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
    private static class MainMenuReadyPatch
    {
        private static void Postfix(NMainMenu __instance)
        {
            OpenCharacterSelectForSecondAscensionUiCheck(__instance);
            foreach (Action start in Suites)
            {
                start();
            }
        }
    }

    private static void OpenCharacterSelectForSecondAscensionUiCheck(NMainMenu mainMenu)
    {
        if (!CommandLineHelper.HasArg(SecondAscensionUiCheckArg))
        {
            return;
        }

        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(mainMenu))
            {
                return;
            }

            var screen = mainMenu.SubmenuStack.GetSubmenuType<MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen>();
            screen.InitializeSingleplayer();
            mainMenu.SubmenuStack.Push(screen);
            Log.Info("[LibrarySecondAscension] UI check opened standard character select.");
        }).CallDeferred();
    }
}
