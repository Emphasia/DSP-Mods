using System;
using System.Collections;
using UnityEngine;
using BepInEx;
using HarmonyLib;

namespace TelePotter
{
    [BepInPlugin("emphasia.mod.dsp.TelePotter", "TelePotter", "1.0.0")]
    public class TelePotter : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Logger;

        private void Start()
        {
            // Harmony.CreateAndPatchAll(typeof(TelePotter));
            new Harmony("emphasia.mod.dsp.TelePotter").PatchAll();
            Logger = base.Logger;
            Logger.LogInfo("INIT.");
        }

        private void Update()
        {
            if (ready && target != null)
            {
                Logger.LogInfo("READY.");
                UIStarmap starmap = UIRoot.instance.uiGame.starmap;
                if (target is UIStarmapPlanet)
                {
                    starmap.focusPlanet = (UIStarmapPlanet)target;
                    target = ((UIStarmapPlanet)target).planet;
                }
                else if (target is UIStarmapStar)
                {
                    starmap.focusStar = (UIStarmapStar)target;
                    target = ((UIStarmapStar)target).star;
                }
                Teleport(target);
                // GameMain.mainPlayer.movementState = EMovementState.Sail;
                starmap.OnCursorFunction2Click(0);  // re_focus
                Logger.LogInfo("ARRIVED.");
                ready = false;
            }
        }

        private void Teleport(object target)
        {
            if (target is int)
            {
                if ((int)target % 100 == 0)
                {
                    target = GameMain.galaxy.StarById((int)target / 100);
                    Logger.LogInfo("Teleport.StarById.");
                }
                else
                {
                    target = GameMain.galaxy.PlanetById((int)target);
                    Logger.LogInfo("Teleport.PlanetById.");
                }
            }
            if (target is PlanetData)
            {
                GameMain.data.ArriveStar(((PlanetData)target).star);
                Logger.LogInfo("Teleport.ArriveStar.PlanetData.");
            }
            else
            {
                if (target is StarData)
                {
                    GameMain.data.ArriveStar((StarData)target);
                    Logger.LogInfo("Teleport.ArriveStar.StarData.");
                }
            }
            base.StartCoroutine(SendPlayer(target));
        }

        private IEnumerator SendPlayer(object target)
        {
            yield return new WaitForEndOfFrame();
            if (target is PlanetData)
            {
                GameMain.mainPlayer.uPosition = ((PlanetData)target).uPosition + VectorLF3.unit_z * (double)((PlanetData)target).realRadius;
            }
            else
            {
                if (target is StarData)
                {
                    GameMain.mainPlayer.uPosition = ((StarData)target).uPosition + VectorLF3.unit_z * (double)((StarData)target).physicsRadius;
                }
                else
                {
                    if (target is VectorLF3)
                    {
                        GameMain.mainPlayer.uPosition = (VectorLF3)target;
                    }
                    else
                    {
                        if (target is string && (string)target == "resize")
                        {
                            GameMain.mainPlayer.transform.localScale = Vector3.one;
                        }
                    }
                }
            }
            if (!(target is string) || (string)target != "resize")
            {
                base.StartCoroutine(SendPlayer("resize"));
            }
            yield break;
        }

        [HarmonyPatch(typeof(UIStarmap), "OnCursorFunction3Click")]
        private class UIStarmap_OnCursorFunction3Click
        {
            private static bool Prefix(UIStarmap __instance, int obj)
            {
                if (__instance.focusPlanet != null)
                {
                    target = __instance.focusPlanet;
                }
                else if (__instance.focusStar != null)
                {
                    target = __instance.focusStar;
                }
                ready = true;
                return false;
            }
        }

        private static object target = null;
        private static bool ready = false;
    }
}
