using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using Epic.OnlineServices;
using JetBrains.Annotations;

// For future programs: change namespace to encompass the entire script like here

namespace MoarMarshmallows;

public class MoarMarshmallows : ModBehaviour
{
    public static MoarMarshmallows Instance;

    //This bool is necessary for making sure the player actually suffocates, rather than just falls over
    public bool shouldSuffocatePlayer = false;

    public bool rouletteOn = false;
    
    //This bool determines whether eating a burnt marshmallow kills you
    public bool alwaysPunish = false;

    //This bool determines whether burning a marshmallow kills you
    public bool instantPunish = false;

    public bool marshmallowLimit = false;
    //This int is the mallow count in settings
    public int marshmallowLimitCount = 10;
    //Instant marshmallow count is the number of marshmallows left in inventory should you turn on the limit
    public int instantMarshmallowCount = 5;


    public int marshmallowChance = 6;

    public bool marshmallowDisabled = false;

    public bool theyAteIt = false;

    public void Awake()
    {
        Instance = this;
        // You won't be able to access OWML's mod helper in Awake.
        // So you probably don't want to do anything here.
        // Use Start() instead.
    }

    public void Start()
    {
        // Starting here, you'll have access to OWML's mod helper.
        ModHelper.Console.WriteLine($"My mod {nameof(MoarMarshmallows)} is loaded!", MessageType.Success);

        new Harmony("ColumbidaeCafe.MoarMarshmallows").PatchAll(Assembly.GetExecutingAssembly());

        // Example of accessing game code.
        OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }

    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene != OWScene.SolarSystem) return;
        shouldSuffocatePlayer = false;
        // above line ensures that player will not suffocate after respawning
        ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);

        // sets the marshmallow count for this loop to the one in settings
        var marshmallowLimitTotal = ModHelper.Config.GetSettingsValue<int>("marshmallowCount");
        //ModHelper.Console.WriteLine($"You changed marshmallow limit to: {marshmallowLimitTotal}!");
        //MoarMarshmallows.Instance.marshmallowLimitCount = marshmallowLimitTotal;
        //marshmallowLimitTotal--;

        MoarMarshmallows.Instance.instantMarshmallowCount = marshmallowLimitTotal;
        //ModHelper.Console.WriteLine($"instantMarshmallowCount: {MoarMarshmallows.Instance.instantMarshmallowCount}!");

        //Resets bool that leads to divide by zero death message
        theyAteIt = false;
    }

    public override void Configure(IModConfig config)
    {
        // Put config code here
        var allergyOn = config.GetSettingsValue<bool>("marshmallowRoulette");
        // ModHelper.Console.WriteLine($"You changed allergy state to: {allergyOn}!");
        MoarMarshmallows.Instance.rouletteOn = allergyOn;

        var marshmallowDeathChance = ModHelper.Config.GetSettingsValue<int>("rouletteChance");
        MoarMarshmallows.Instance.marshmallowChance = marshmallowDeathChance;
        // set the public integer marshmallowChance = marshmallowDeathChance, which is the value in the settings.

        var burningPunish = ModHelper.Config.GetSettingsValue<bool>("alwaysPunish");
        MoarMarshmallows.Instance.alwaysPunish = burningPunish;

        var instantPunishment = ModHelper.Config.GetSettingsValue<bool>("instantPunish");
        MoarMarshmallows.Instance.instantPunish = instantPunishment;

        var marshmallowLimitToggle = ModHelper.Config.GetSettingsValue<bool>("marshmallowLimitEnable");
        MoarMarshmallows.Instance.marshmallowLimit = marshmallowLimitToggle;
    }
}

[HarmonyPatch]
public static class Patches
{

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerResources), nameof(PlayerResources.IsOxygenPresent))]
    // Suffocate player even with oxygen
    public static void ForceSuffocate(ref bool __result)
    {
        if (MoarMarshmallows.Instance.shouldSuffocatePlayer == true)
        {
            __result = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Marshmallow), nameof(Marshmallow.Eat))]

    public static void Consumption(Marshmallow __instance)
    {
        int rand = Random.Range(1, MoarMarshmallows.Instance.marshmallowChance + 1);
        if (MoarMarshmallows.Instance.rouletteOn == true)
        {

            if (MoarMarshmallows.Instance.marshmallowChance < 0)
            {
                //death by explosion
                Locator.GetDeathManager().KillPlayer(DeathType.Energy);
            }

            if (MoarMarshmallows.Instance.marshmallowChance == 0)
            {
                //death by breaking spacetime
                Locator.GetTimelineObliterationController().BeginTimelineObliteration(TimelineObliterationController.ObliterationType.PARADOX_DEATH, null);
                //This variable changes whether the divide by zero death message appears from breaking spacetime
                MoarMarshmallows.Instance.theyAteIt = true;
            }

            if (rand == 1)
            {
                Locator.GetPlayerBody().GetComponent<PlayerResources>()._currentOxygen = 0;
                MoarMarshmallows.Instance.shouldSuffocatePlayer = true;
            }
        }

        if (MoarMarshmallows.Instance.alwaysPunish == true)
        {
            if (__instance.IsBurned())
            {
                Locator.GetPlayerBody().GetComponent<PlayerResources>()._currentOxygen = 0;
                MoarMarshmallows.Instance.shouldSuffocatePlayer = true;
            }
        }
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(Marshmallow), nameof(Marshmallow.Burn))]

    public static void InstantDeath(Marshmallow __instance)
    {

        if (MoarMarshmallows.Instance.instantPunish == true)
        {
            if (__instance.IsBurned())
            {
                Locator.GetDeathManager().KillPlayer(DeathType.Default);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Marshmallow), nameof(Marshmallow.RemoveMallow))]

    public static void SetLimit(Marshmallow __instance)
    {
        if (MoarMarshmallows.Instance.marshmallowLimit == true)
        {
            if (__instance._mallowState == Marshmallow.MallowState.Gone)
            {
                MoarMarshmallows.Instance.instantMarshmallowCount--;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Marshmallow), nameof(Marshmallow.SpawnMallow))]
    
    public static bool DisableMarshmallows() { 
    

        if (MoarMarshmallows.Instance.instantMarshmallowCount <= 0)
        {
            return false;
        }
        else 
        {
            return true;
        }
    }



    [HarmonyPostfix]
    [HarmonyPatch(typeof(Campfire), nameof(Campfire.CheckStickIntersection))]

    public static void RemoveMarshmallowPrompt(Campfire __instance)
    {
        if (MoarMarshmallows.Instance.instantMarshmallowCount <= 0)
        {
            if (__instance._isPlayerRoasting == true)
            {
                Locator.GetPromptManager().SetPromptsVisible(false);
            }
            else
            {
                Locator.GetPromptManager().SetPromptsVisible(true);
            }

        }

    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameOverController), nameof(GameOverController.OnTriggerDeathOfReality))]

    public static bool ZeroError(GameOverController __instance)
    {
        if (MoarMarshmallows.Instance.marshmallowChance == 0)
        {
            if (MoarMarshmallows.Instance.theyAteIt == true)
            {
                //Achievements.Earn(Achievements.Type.TERRIBLE_FATE);
                __instance._deathText.text = "YOU TRIED TO DIVIDE BY ZERO";
                __instance.SetupGameOverScreen(4f);

                return false;
            }
            else
            {
                return true;
            }
        }
        else
        {
            return true;
        }
    }
}