using BepInEx;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using System.Reflection;

namespace DaySleeper
{
    [BepInPlugin(PLUGIN_ID, PLUGIN_NAME, PLUGIN_VERSION)]

    public class DaySleeperPlugin : BaseUnityPlugin
    {

        const string PLUGIN_ID = "com.onemorelvl.xaviorr.daysleeper";
        const string PLUGIN_NAME = "DaySleeper";
        const string PLUGIN_VERSION = "1.2.0";

        private Harmony harmony;
        private static Assembly assembly;

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> sleepAtNight;
        private static ConfigEntry<bool> sleepAtDay;
        private static ConfigEntry<bool> sleepTillSix;
        private static ConfigEntry<bool> sleepWhileWet;
        private static ConfigEntry<bool> sleepWhileExposed;


        public void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod bitch!");
            sleepTillSix = Config.Bind("General", "Sleep until the next 6", true, "Sleep anytime and wake up at the next 6 o'clock");
            sleepAtNight = Config.Bind("General", "Sleep at Night", false, "Sleep at any time and Wake up at 6am");
            sleepAtDay = Config.Bind("General", "Sleep during the day", false, "Sleep at any time and wake up at 6pm");
            sleepWhileWet = Config.Bind("General", "Sleep Wet", false, "Go to sleep while wet");
            sleepWhileExposed = Config.Bind("General", "Sleep Exposed", false, "Sleep on a bed without a roof");
            

            if (!modEnabled.Value)
            {
                return;
            }

            assembly = typeof(DaySleeperPlugin).Assembly;
            harmony = new Harmony(PLUGIN_ID);
            harmony.PatchAll(assembly);
        }

        public void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
        public class Interact_Patch
        {
            public static bool Prefix(Bed __instance, ref bool __result, ref Humanoid human, ref bool repeat)
            {
                if (repeat)
                {
                    __result = false;
                    return false;
                }
                long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
                long owner = __instance.GetOwner();
                Player human2 = human as Player;
                if (owner == 0L)
                {
                    Debug.Log("Has no creator");
                    if (!__instance.CheckExposure(human2))
                    {
                        __result = false;
                        return false;
                    }
                    __instance.SetOwner(playerID, Game.instance.GetPlayerProfile().GetName());
                    Game.instance.GetPlayerProfile().SetCustomSpawnPoint(__instance.GetSpawnPoint());
                    human.Message(MessageHud.MessageType.Center, "$msg_spawnpointset");
                }
                else if (__instance.IsMine())
                {
                    Debug.Log("Is mine");
                    if (__instance.IsCurrent())
                    {
                        Debug.Log("is current spawnpoint");
                        if (!modEnabled.Value && !EnvMan.instance.CanSleep())
                        {
                            human.Message(MessageHud.MessageType.Center, "$msg_cantsleep");
                            __result = false;
                            return false;
                        }
                        if (!__instance.CheckEnemies(human2))
                        {
                            __result = false;
                            return false;
                        }
                        if (!sleepWhileExposed.Value)
                        {
                            if (!__instance.CheckExposure(human2))
                            {
                                __result = false;
                                return false;
                            }
                        }
                        if (!__instance.CheckFire(human2))
                        {
                            __result = false;
                            return false;
                        }
                        if (!sleepWhileWet.Value)
                        {
                            if (!__instance.CheckWet(human2))
                            {
                                __result = false;
                                return false;
                            }
                        }
                        human.AttachStart(__instance.m_spawnPoint, __instance.gameObject, hideWeapons: true, isBed: true, onShip: false, "attach_bed", new Vector3(0f, 0.5f, 0f));
                        __result = false;
                        return false;
                    }
                    Debug.Log("Not current spawn point");
                    if (!__instance.CheckExposure(human2))
                    {
                        __result = false;
                        return false;
                    }
                    Game.instance.GetPlayerProfile().SetCustomSpawnPoint(__instance.GetSpawnPoint());
                    human.Message(MessageHud.MessageType.Center, "$msg_spawnpointset");
                }

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.UpdateSleeping))]
        public class UpdateSleeping_Patch
        {
            public static bool Prefix(Game __instance)
            {

                if (!ZNet.instance.IsServer())
                {
                    return false;
                }
                if (__instance.m_sleeping)
                {
                    if (!EnvMan.instance.IsTimeSkipping())
                    {
                        __instance.m_sleeping = false;
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStop");
                        return false;
                    }
                }
                else if (!EnvMan.instance.IsTimeSkipping())
                {
                    if (!__instance.EverybodyIsTryingToSleep())
                    {
                        return false;
                    }
                    EnvMan.instance.SkipToMorning();
                    __instance.m_sleeping = true;
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStart");
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetMorningStartSec))]
        public class GetMorningStartSec_Patch
        {
            public static bool Prefix(EnvMan __instance, ref double __result, int day)
            {
                if (sleepAtNight.Value)
                {
                    __result = (float)(day * __instance.m_dayLengthSec) + (float)__instance.m_dayLengthSec * 0.85f;
                    return false;

                }

                if(sleepAtDay.Value)
                {
                    __result = (float)(day * __instance.m_dayLengthSec) + (float)__instance.m_dayLengthSec * 0.15f;
                    return false;

                }

                if (sleepTillSix.Value)
                {
                    if (__instance.IsDay())
                    {
                        __result = (float)(day * __instance.m_dayLengthSec) + (float)__instance.m_dayLengthSec * 0.85f;
                        return false;
                    }
                    __result = (float)(day * __instance.m_dayLengthSec) + (float)__instance.m_dayLengthSec * 0.15f;
                    return false;
                }

                return true;
            }

        }
    }


}
