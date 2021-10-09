﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using CardChoiceSpawnUniqueCardPatch.CustomCategories;
using UnboundLib;
using UnboundLib.Cards;
using HarmonyLib;
using FFC.Cards;
using ModdingUtils.Extensions;
using Photon.Pun;
using UnboundLib.GameModes;
using UnboundLib.Utils;
using UnboundLib.Utils.UI;
using UnityEngine;
using TMPro;
using UnboundLib.Networking;

namespace FFC {
    [BepInDependency("com.willis.rounds.unbound")]
    [BepInDependency("pykess.rounds.plugins.moddingutils")]
    [BepInDependency("pykess.rounds.plugins.cardchoicespawnuniquecardpatch")]
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class FFC : BaseUnityPlugin {
        public const string AbbrModName = "FFC";
        
        private const string ModId = "fluxxfield.rounds.plugins.fluxxfieldscards";
        private const string ModName = "FluxxField's Cards (FFC)";
        private const string Version = "1.0.1";

        public static CardCategory DefaultCategory;
        public static CardCategory MainClassesCategory;
        public static CardCategory MarksmanClassUpgradesCategory;
        public static CardCategory LightGunnerClassUpgradesCategory;
        public static CardCategory AssaultRifleUpgradeCategory;
        public static CardCategory DMRUpgradeCategory;
        public static CardCategory LMGUpgradeCategory;

        private static ConfigEntry<bool> UseClassFirstRoundtConfig;
        internal static bool UseClassesFirstRound;

        private void Awake() {
            UseClassFirstRoundtConfig = Config.Bind("FFC", "Enabled", false, "Enable classes only first round");
            new Harmony(ModId).PatchAll();
        }


        private void Start() {
            UseClassesFirstRound = UseClassFirstRoundtConfig.Value;

            // Gotta give CustomCardCategories a sec to setup
            if (CustomCardCategories.instance != null) {
                DefaultCategory = CustomCardCategories.instance.CardCategory("Default");
                MainClassesCategory = CustomCardCategories.instance.CardCategory("MainClasses");
                MarksmanClassUpgradesCategory = CustomCardCategories.instance.CardCategory("MarksmanUpgrades");
                LightGunnerClassUpgradesCategory = CustomCardCategories.instance.CardCategory("LightGunnerUpgrades");
                AssaultRifleUpgradeCategory = CustomCardCategories.instance.CardCategory("AssaultRifle");
                DMRUpgradeCategory = CustomCardCategories.instance.CardCategory("DMR");
                LMGUpgradeCategory = CustomCardCategories.instance.CardCategory("LMG");
            }

            UnityEngine.Debug.Log($"[{AbbrModName}] Building cards");
            // Marksman Class
            CustomCard.BuildCard<MarksmanClass>();
            CustomCard.BuildCard<SniperRifleExtendedMag>();
            CustomCard.BuildCard<Barret50Cal>();
            CustomCard.BuildCard<ArmorPiercingRounds>();
            // Light Gunner Class
            CustomCard.BuildCard<LightGunnerClass>();
            CustomCard.BuildCard<AssaultRifle>();
            CustomCard.BuildCard<DMR>();
            CustomCard.BuildCard<LMG>();
            // Default
            CustomCard.BuildCard<FastMags>();
            CustomCard.BuildCard<Conditioning>();
            CustomCard.BuildCard<BattleExperience>();
            UnityEngine.Debug.Log($"[{AbbrModName}] Done building cards");

            this.ExecuteAfterSeconds(0.4f, HandleBuildDefaultCategory);
            
            Unbound.RegisterMenu(ModName, () => { }, NewGUI, null, false);
            
            Unbound.RegisterHandshake(ModId, OnHandShakeCompleted);
            
            Unbound.RegisterCredits(ModName,
                new[] {"FluxxField"},
                new[] {"github"},
                new[] {"https://github.com/FluxxField/FFC"});

            GameModeManager.AddHook(GameModeHooks.HookGameStart, gm => HandlePlayersBlacklistedCategories());
            GameModeManager.AddHook(GameModeHooks.HookRoundStart, gm => HandleBarret50CalAmmo());
        }

        private void NewGUI(GameObject menu) {
            MenuHandler.CreateText($"{ModName} Options", menu, out TextMeshProUGUI _);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);
            MenuHandler.CreateToggle(false, "Enable Force classes first round", menu, useClassesFirstRound => {
                UseClassesFirstRound = useClassesFirstRound;
                OnHandShakeCompleted();
            });
        }

        private void OnHandShakeCompleted() {
            if (PhotonNetwork.IsMasterClient) {
                NetworkingManager.RPC_Others(typeof(FFC), nameof(SyncSettings), new object[] { UseClassesFirstRound });
            }
        }

        [UnboundRPC]
        private static void SyncSettings(bool hostUseClassesStart) {
            UseClassesFirstRound = hostUseClassesStart;
        }

        private void HandleBuildDefaultCategory() {
            UnityEngine.Debug.Log($"[{AbbrModName}] Building Default categories");
            foreach (Card card in CardManager.cards.Values.ToList()) {
                List<CardCategory> categories = card.cardInfo.categories.ToList();

                if (categories.Count == 0 || card.category != "FFC") {
                    categories.Add(DefaultCategory);
                    card.cardInfo.categories = categories.ToArray();
                }
            }
        }

        private IEnumerator HandlePlayersBlacklistedCategories() {
            if (UseClassesFirstRound) {
                UnityEngine.Debug.Log($"[{AbbrModName}] Setting up players blacklisted categories");
                Player[] players = PlayerManager.instance.players.ToArray();
        
                foreach (Player player in players) {
                    CharacterStatModifiersExtension.GetAdditionalData(player.data.stats).blacklistedCategories.AddRange(
                        new[] {
                            DefaultCategory,
                            MarksmanClassUpgradesCategory,
                            LightGunnerClassUpgradesCategory
                        }
                    );
                }
            }

            yield break;
        }

        private IEnumerator HandleBarret50CalAmmo() {
            UnityEngine.Debug.Log($"[{AbbrModName}] Setting up player categories");
            Player[] players = PlayerManager.instance.players.ToArray();

            foreach (Player player in players) {
                List<CardInfo> cards = player.data.currentCards;
                int ammoCount = 0;
                bool has50Cal = false;

                foreach (CardInfo card in cards) {
                    switch (card.cardName.ToUpper()) {
                        case "BARRET .50 CAL": {
                            has50Cal = true;
                            goto case "SNIPER RIFLE EXTENDED MAG";
                        }
                        case "SNIPER RIFLE EXTENDED MAG": {
                            ammoCount += 1;
                            break;
                        }
                    }
                }

                if (has50Cal) {
                    player
                        .GetComponent<Holding>()
                        .holdable.GetComponent<Gun>()
                        .GetComponentInChildren<GunAmmo>()
                        .maxAmmo = ammoCount;
                }
            }

            yield break;
        }
    }
}