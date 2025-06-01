using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnboundLib;
using UnboundLib.GameModes;
using UnboundLib.Utils.UI;
using UnityEngine;

namespace LarrysMod
{
    // These are the mods required for our mod to work
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("pykess.rounds.plugins.moddingutils")]
    [BepInDependency("pykess.rounds.plugins.cardchoicespawnuniquecardpatch")]
    [BepInDependency("com.pandapip1.rounds.selectanynumberrounds", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("pykess.rounds.plugins.pickncards", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.willuwontu.rounds.gamemodes", BepInDependency.DependencyFlags.SoftDependency)]
    // Declares our mod to Bepin
    [BepInPlugin(ModId, ModName, Version)]
    // The game our mod is associated with
    [BepInProcess("Rounds.exe")]
    public class LarrysMod : BaseUnityPlugin
    {
        public static LarrysMod instance { get; private set; }

        private const string ModId = "larppaliz.rounds.settingsmod";
        private const string ModName = "Larrys Mod";
        public const string Version = "1.0.0"; // What version are we on (major.minor.patch)?
        public const string ModInitials = "LM";

        public static ConfigEntry<bool> EnableWinnerDrawLessConfig;
        public static ConfigEntry<int> WinnerDrawAmountConfig;

        public static ConfigEntry<int> StartingPicksConfig;

        public static int StartingPicks = 1;

        public static ConfigEntry<int> StartingDrawsConfig;

        public static int StartingDraws = 0;

        public static bool enableWinnerDrawLess = false;
        public static int winnerDrawAmount = 2;

        public GameObject optionsMenu;

        void Awake()
        {
            instance = this;

            // Use this to call any harmony patch files your mod may have
            var harmony = new Harmony(ModId);
            harmony.PatchAll();
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, PickEnd);
            GameModeManager.AddHook(GameModeHooks.HookPickStart, PickStart);
            GameModeManager.AddHook(GameModeHooks.HookRoundEnd, RoundEnd);
            GameModeManager.AddHook(GameModeHooks.HookGameStart, GameStart);

            {
                EnableWinnerDrawLessConfig = Config.Bind(ModInitials, "WinnerDrawToggle", false, "Toggle the winner draw stuff.");
                WinnerDrawAmountConfig = Config.Bind(ModInitials, "WinnerDrawAmount", 2, "Winner Draw Amount");
                StartingPicksConfig = Config.Bind(ModInitials, "StartingPicksAmount", 1, "Starting Picks Amount");
                StartingDrawsConfig = Config.Bind(ModInitials, "StartingDrawsAmount", 0, "Extra Starting Draws Amount");
            }
        }
        void Start()
        {
            Unbound.RegisterHandshake(ModId, OnHandShakeCompleted);
            Unbound.RegisterMenu("Larrys Mod", () => { }, BreadGUI, optionsMenu, false);

            StartingPicks = StartingPicksConfig.Value;
            StartingDraws = StartingDrawsConfig.Value;
            enableWinnerDrawLess = EnableWinnerDrawLessConfig.Value;
            winnerDrawAmount = WinnerDrawAmountConfig.Value;
        }
        public List<int> GetRoundWinners() => new List<int>(GameModeManager.CurrentHandler.GetRoundWinners());
        internal void OnHandShakeCompleted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                for (int i = 0; i < 1; i++)
                {
                    NetworkingManager.RPC_Others(typeof(LarrysMod), nameof(UpdateValues), enableWinnerDrawLess, winnerDrawAmount, StartingPicks, StartingDraws);
                }
            }
        }
        private static void UpdateValues(bool WinnerDraw, int WinnerDrawValue, int Picks, int Draws)
        {
            StartingPicks = Picks;
            StartingDraws = Draws;
            enableWinnerDrawLess = WinnerDraw;
            winnerDrawAmount = WinnerDrawValue;
        }
        private void BreadGUI(GameObject menu)
        {
            if (menu == null)
            {
                Debug.LogError("Menu object is null.");
                return;
            }
            MenuHandler.CreateText("Drawing Cards", menu, out TextMeshProUGUI _);
            MenuHandler.CreateToggle(EnableWinnerDrawLessConfig.Value, "Toggle Winner Draws", menu, value => { EnableWinnerDrawLessConfig.Value = value; enableWinnerDrawLess = value; });
            MenuHandler.CreateSlider("How many cards the winner gets to draw", menu, 20, 1, 20, winnerDrawAmount, value => { WinnerDrawAmountConfig.Value = (int)value; winnerDrawAmount = (int)value; }, out UnityEngine.UI.Slider WinnerAmountSlider, true);
            MenuHandler.CreateSlider("How many cards you pick at the start", menu, 20, 1, 20, StartingPicks, value => { StartingPicksConfig.Value = (int)value; StartingPicks = (int)value; }, out UnityEngine.UI.Slider StartPickAmountSlider, true);
            MenuHandler.CreateSlider("How many extra cards you draw at the start", menu, 20, -5, 20, StartingDraws, value => { StartingDrawsConfig.Value = (int)value; StartingDraws = (int)value; }, out UnityEngine.UI.Slider StartDrawAmountSlider, true);

        }

        public int[] WinnerDraws = new int[50];
        public Player[] PlayersItsDonefor;
        private IEnumerator RoundEnd(IGameModeHandler gm)
        {
            PlayersItsDonefor = new Player[50];
            List<int> winners = GetRoundWinners();
            if (EnableWinnerDrawLessConfig.Value && winners != null && DrawNCards.DrawNCards.NumDrawsConfig != null)
            {
                foreach (Player player in PlayerManager.instance.players)
                {
                    if (winners.Contains(player.teamID))
                    {
                        WinnerDraws[player.playerID] = DrawNCards.DrawNCards.GetPickerDraws(player.playerID);
                        DrawNCards.DrawNCards.RPCA_SetPickerDraws(player.playerID, winnerDrawAmount + (DrawNCards.DrawNCards.GetPickerDraws(player.playerID) - DrawNCards.DrawNCards.NumDraws));
                    }
                }
            }
            yield break;
        }

        public void fixWinnerDrawThing(Player player)
        {
            if (EnableWinnerDrawLessConfig.Value && !PlayersItsDonefor.Contains(player))
            {
                List<int> winners = GetRoundWinners();
                if (winners != null)
                {
                    if (winners.Contains(player.teamID) && WinnerDraws[player.playerID] != DrawNCards.DrawNCards.GetPickerDraws(player.playerID))
                    {
                        PlayersItsDonefor[player.playerID] = player;
                        DrawNCards.DrawNCards.RPCA_SetPickerDraws(player.playerID, WinnerDraws[player.playerID]);
                    }
                }
            }
        }
        private IEnumerator PickEnd(IGameModeHandler gm)
        {
            SelectAnyNumberRounds.Plugin.configPickNumber.Value = 1;

            for (int i = 0; i < PlayerManager.instance.players.Count; i++)
            {
                if (PlayersItsDonefor != null)
                {
                    if (PlayerManager.instance.players != null && PlayersItsDonefor.Count() > 1)
                    {
                        fixWinnerDrawThing(PlayerManager.instance.players[i]);
                    }
                }
            }
            yield break;
        }

        private IEnumerator PickStart(IGameModeHandler gm)
        {
            yield break;
        }

        private IEnumerator GameEnd(IGameModeHandler gm)
        {
            SelectAnyNumberRounds.Plugin.configPickNumber.Value = 2;
            yield break;
        }

        private IEnumerator FirstRoundStart(IGameModeHandler gm)
        {
            foreach (Player player in PlayerManager.instance.players)
            {
                DrawNCards.DrawNCards.RPCA_SetPickerDraws(player.playerID, DrawNCards.DrawNCards.GetPickerDraws(player.playerID) - StartingDraws);
            }
            yield break;
        }
        private IEnumerator GameStart(IGameModeHandler gm)
        {
            for (int i = 0; i < PlayerManager.instance.players.Count; i++)
            {
                Player player = PlayerManager.instance.players[i];

                player.playerID = i;

            }

            SelectAnyNumberRounds.Plugin.enableContinueCard.Value = false;
            SelectAnyNumberRounds.Plugin.configPickNumber.Value = StartingPicks;

            foreach (Player player in PlayerManager.instance.players)
            {
                DrawNCards.DrawNCards.RPCA_SetPickerDraws(player.playerID, DrawNCards.DrawNCards.NumDraws + StartingDraws);
            }



            GameModeManager.AddOnceHook(GameModeHooks.HookRoundStart, FirstRoundStart);

            yield break;
        }

        public int PlayerDrawsIncrease(Player player, int Amount)
        {
            if (GameModeManager.CurrentHandler.GetTeamScore(player.teamID).rounds > 0)
            {
                LarrysMod.instance.fixWinnerDrawThing(player);
            }

            DrawNCards.DrawNCards.RPCA_SetPickerDraws(player.playerID, DrawNCards.DrawNCards.GetPickerDraws(player.playerID) + Amount);

            return DrawNCards.DrawNCards.GetPickerDraws(player.playerID);
        }

    }
}