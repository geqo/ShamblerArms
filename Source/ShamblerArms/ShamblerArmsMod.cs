using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ShamblerArms
{
    public class ShamblerArmsMod : Mod
    {
        public const string NonUnoPinataId = "avilmask.NonUnoPinata";

        public static ShamblerArmsSettings Settings { get; private set; }

        /// <summary>
        /// Our own "don't drop" logic runs only when Non Uno Pinata is not active;
        /// otherwise NUP owns item handling and we stay out of its way.
        /// </summary>
        public static bool OwnKeepGearActive { get; private set; }

        public ShamblerArmsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ShamblerArmsSettings>();
            OwnKeepGearActive = !ModsConfig.IsActive(NonUnoPinataId);

            new Harmony("geqo.ShamblerArms").PatchAll(Assembly.GetExecutingAssembly());

            Log.Message(OwnKeepGearActive
                ? "[Shambler Arms] loaded."
                : "[Shambler Arms] loaded; Non Uno Pinata detected, its item handling is used.");
        }

        public override string SettingsCategory() => "Shambler Arms";

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
    }
}
