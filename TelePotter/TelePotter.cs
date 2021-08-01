using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using HarmonyLib;

namespace TelePotter
{
    [BepInPlugin("emphasia.mod.dsp.TelePotter", "TelePotter", "1.1.0")]
    public class TelePotter : BaseUnityPlugin
    {
        static TelePotter self;  // this
        internal static BepInEx.Logging.ManualLogSource Logger;
        public enum TelePotterState { idle, tasking, loading, ready, crossing, arrived, failed };
        public static TelePotterState state = TelePotterState.idle;
        public static int target = 0;
        private int num = 0;
        public Text teleportTip;

        private void Start()
        {
            // Harmony.CreateAndPatchAll(typeof(TelePotter));
            new Harmony("emphasia.mod.dsp.TelePotter").PatchAll();
            self = this;
            Logger = base.Logger;
            state = TelePotterState.idle;
            string text_ = "天体方位指示";
            if (Localization.strings.Exist(text_))
            {
                StringProto text = Localization.strings[text_];
                text.ENUS = "Teleport";
                text.ZHCN = "传送";
                Dictionary<string, int> nameIndices = Traverse.Create(Localization.strings).Field("nameIndices").GetValue() as Dictionary<string, int>;
                Localization.strings.dataArray[nameIndices[text_]] = text;
            }
            teleportTip = Instantiate(UIRoot.instance.uiGame.generalTips.mechaMoveTip.flyTip, UIRoot.instance.uiGame.generalTips.mechaMoveTip.flyTip.transform.parent);
            teleportTip.name = "tp-tip";
            teleportTip.text = (Localization.language == Language.zhCN) ? "跨恒星传送请稍候" : "Please wait while the star is loading...";
            Destroy(teleportTip.gameObject.GetComponent<Localizer>());
            teleportTip.gameObject.SetActive(false);
            Logger.LogInfo("INIT.");
        }

        private void LateUpdate()
        {
            if (target <= 0 && state != TelePotterState.idle)
            {
                Logger.LogInfo("State : IDLE  [target !> 0]");
                state = TelePotterState.idle;
            }
            if (state == TelePotterState.tasking)
            {
                Logger.LogDebug("State : TASKING");
                UIRoot.instance.uiGame.buildMenu.SetCurrentCategory(0);
                GameMain.mainPlayer.movementState = EMovementState.Fly;
                OpenPortal(target);
                num = 5000;
                teleportTip.gameObject.SetActive(true);
                state = TelePotterState.loading;
            }
            else if (state == TelePotterState.loading)
            {
                if (num%50==0)
                    Logger.LogDebug("State : LOADING" + " --- " + (5000-num));
                bool? loaded = DeterminePortal(target);
                if (loaded == null)
                {
                    Logger.LogInfo("State : FAILED");
                    state = TelePotterState.ready;
                }
                else if ((bool)loaded)
                {
                    state = TelePotterState.ready;
                }
            }
            if (state == TelePotterState.ready)
            {
                Logger.LogDebug("State : READY");
                teleportTip.gameObject.SetActive(false);
                base.StartCoroutine(TeleportPlayer(target));
                num = 5;
                state = TelePotterState.crossing;
            }
            else if (state == TelePotterState.crossing)
            {
                Logger.LogDebug("State : CROSSING");
                bool? arrived = DetermineArrival(target);
                if (arrived == null)
                {
                    state = TelePotterState.failed;
                }
                else if ((bool)arrived)
                {
                    state = TelePotterState.arrived;
                }
            }
            if (state == TelePotterState.arrived)
            {
                Logger.LogInfo("State : ARRIVED");
                //GameMain.mainPlayer.navigation.Arrive();
                //GameMain.mainPlayer.movementState = EMovementState.Fly;
                OnArrive(target);
                teleportTip.gameObject.SetActive(false);
                state = TelePotterState.idle;
                Logger.LogDebug($"ARRIVED: TARGET={target}");
            }
            else if (state == TelePotterState.failed)
            {
                Logger.LogInfo("State : FAILED");
                teleportTip.gameObject.SetActive(false);
                state = TelePotterState.idle;
            }
        }

        private object StarOrPlanetById(int target)
        {
            if (target % 100 == 0)
            {
                return GameMain.galaxy.StarById(target / 100);
            }
            else
            {
                return GameMain.galaxy.PlanetById(target);
            }
        }

        private void OpenPortal(object target)
        {
            if (target is int)
            {
                target = StarOrPlanetById((int)target);
            }
            if (target is PlanetData)
            {
                Logger.LogInfo("Target.type ----- Planet");
                GameMain.data.ArriveStar(((PlanetData)target).star);
            }
            else if (target is StarData)
            {
                Logger.LogInfo("Target.type ----- Star");
                GameMain.data.ArriveStar((StarData)target);
            }
        }

        private bool? DeterminePortal(object target)
        {
            if (num > 0)
            {
                num--;
                if (target is int)
                {
                    target = StarOrPlanetById((int)target);
                }
                if (target is PlanetData)
                {
                    return ((PlanetData)target).star.loaded;
                }
                else if (target is StarData)
                {
                    return ((StarData)target).loaded;
                }
            }
            return null;
        }

        private IEnumerator TeleportPlayer(object target)
        {
            yield return new WaitForEndOfFrame();
            if (target is int)
            {
                target = StarOrPlanetById((int)target);
            }
            if (target is PlanetData)
            {
                Logger.LogDebug($"Target.type ----- {((PlanetData)target).type}.");
                // GameMain.mainPlayer.navigation.Navigate(((PlanetData)target).id, ((PlanetData)target).uPosition);
                if (((PlanetData)target).type != EPlanetType.Gas)
                {
                    GameMain.mainPlayer.uPosition = ((PlanetData)target).uPosition + VectorLF3.unit_z * ((PlanetData)target).realRadius;
                }
                else
                {
                    GameMain.mainPlayer.uPosition = ((PlanetData)target).uPosition + VectorLF3.unit_z * (((PlanetData)target).realRadius + 20f);
                }
                GameMain.data.ArrivePlanet((PlanetData)target);
            }
            else if (target is StarData)
            {
                GameMain.mainPlayer.uPosition = ((StarData)target).uPosition + VectorLF3.unit_z * (((StarData)target).physicsRadius + 80f);
            }
            else if (target is VectorLF3)
            {
                GameMain.mainPlayer.uPosition = (VectorLF3)target;
            }
            GameMain.data.DetermineRelative();
            yield break;
        }

        private bool? DetermineArrival(object target)
        {
            if (num > 0)
            {
                num--;
                if (target is int)
                {
                    target = StarOrPlanetById((int)target);
                }
                if (target is PlanetData)
                {
                    return (GameMain.mainPlayer.movementState < EMovementState.Sail && GameMain.data.localPlanet != null && GameMain.data.localPlanet.id == ((PlanetData)target).id);
                }
                else if (target is StarData)
                {
                    return (GameMain.data.localStar != null && GameMain.data.localStar.id == ((StarData)target).id);
                }
            }
            return null;
        }

        private void OnArrive(object target)
        {
            if (target is int)
            {
                target = StarOrPlanetById((int)target);
            }
            GameMain.mainPlayer.transform.localScale = Vector3.one;
            GameMain.mainPlayer.uVelocity = VectorLF3.zero;
            GameMain.mainPlayer.controller.velocityOnLanding = Vector3.zero;
            if (target is PlanetData)
            {
                if (((PlanetData)target).type != EPlanetType.Gas)
                {
                    GameMain.data.InitLandingPlace();
                }
                GameMain.mainPlayer.controller.movementStateInFrame = EMovementState.Fly;
                GameMain.mainPlayer.controller.actionFly.targetAltitude = 20f;
            }
            else if (target is StarData)
            {
                GameMain.mainPlayer.controller.movementStateInFrame = EMovementState.Sail;
                GameMain.mainPlayer.controller.actionSail.ResetSailState();
                GameCamera.instance.SyncForSailMode();
                GameMain.gameScenario.NotifyOnSailModeEnter();
            }
            Mecha mecha = GameMain.mainPlayer.mecha;
            for (int i = 0; i < mecha.droneCount; i++)
            {
                if (mecha.drones[i].stage != 0)
                {
                    mecha.drones[i].Reset();
                    mecha.drones[i].position = GameMain.mainPlayer.position;
                }
            }
            mecha.droneLogic.ReloadStates();
        }


        [HarmonyPatch(typeof(UIStarmap), "OnCursorFunction3Click")]
        private class UIStarmap_OnCursorFunction3Click
        {
            private static bool Prefix(UIStarmap __instance, int obj)
            {
                if (__instance.focusPlanet != null)
                {
                    target = __instance.focusPlanet.planet.id;
                }
                else if (__instance.focusStar != null)
                {
                    target = __instance.focusStar.star.id * 100;
                }
                if (target > 0 && (GameMain.mainPlayer.planetData == null || (GameMain.mainPlayer.planetData != null && GameMain.mainPlayer.planetData.id != target)))
                {
                    Logger.LogInfo($"TASK: TARGET={target}");
                    state = TelePotterState.tasking;
                    UIRoot.instance.uiGame.starmap.OnCursorFunction2Click(0);
                    GameMain.mainPlayer.navigation.indicatorAstroId = 0;
                    return false;
                }
                Logger.LogInfo($"ABORT: TARGET={target}");
                target = 0;
                state = TelePotterState.idle;
                return true;
            }
        }
    }
}
