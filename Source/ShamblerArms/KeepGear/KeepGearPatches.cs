using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ShamblerArms.KeepGear
{
    public enum DropReason : byte
    {
        None,
        Killed,
        Downed
    }

    /// <summary>
    /// Which pawn is currently being killed / downed. Pawn.Kill and Pawn_HealthTracker.MakeDowned
    /// both end up calling Pawn.DropAndForbidEverything; instead of rewriting their IL we mark
    /// the scope and intercept DropAndForbidEverything only for that pawn.
    /// Other callers (taken prisoner, sold, distress call) keep vanilla behaviour.
    /// </summary>
    public struct DropScope
    {
        public Pawn pawn;
        public DropReason reason;
        public bool vanilla; // e.g. gladiator duel: the ritual relies on the loser's weapon dropping

        [ThreadStatic] private static DropScope current;

        public static DropScope Current => current;

        public static DropScope Enter(Pawn pawn, DropReason reason)
        {
            DropScope prev = current;
            current = new DropScope
            {
                pawn = pawn,
                reason = reason,
                vanilla = InDuel(pawn)
            };
            return prev;
        }

        public static void Exit(DropScope prev) => current = prev;

        private static bool InDuel(Pawn pawn) =>
            ModsConfig.IdeologyActive && pawn.GetLord()?.LordJob is LordJob_Ritual_Duel;
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Pawn_Kill_Patch
    {
        public static bool Prepare() => ShamblerArmsMod.OwnKeepGearActive;

        public static void Prefix(Pawn __instance, out DropScope __state) =>
            __state = DropScope.Enter(__instance, DropReason.Killed);

        public static void Finalizer(DropScope __state) => DropScope.Exit(__state);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Pawn_HealthTracker_MakeDowned_Patch
    {
        public static bool Prepare() => ShamblerArmsMod.OwnKeepGearActive;

        public static void Prefix(Pawn ___pawn, out DropScope __state) =>
            __state = DropScope.Enter(___pawn, DropReason.Downed);

        public static void Finalizer(DropScope __state) => DropScope.Exit(__state);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
    public static class Pawn_DropAndForbidEverything_Patch
    {
        public static bool Prepare() => ShamblerArmsMod.OwnKeepGearActive;

        public static bool Prefix(Pawn __instance, bool keepInventoryAndEquipmentIfInBed, bool rememberPrimary)
        {
            DropScope scope = DropScope.Current;
            if (scope.reason == DropReason.None || scope.pawn != __instance || scope.vanilla)
                return true;

            ShamblerArmsSettings s = ShamblerArmsMod.Settings;
            bool killed = scope.reason == DropReason.Killed;
            bool keepEquipment = killed ? s.keepEquipmentOnDeath : s.keepEquipmentOnDowned;
            bool keepInventory = killed ? s.keepInventoryOnDeath : s.keepInventoryOnDowned;

            if (!keepEquipment && !keepInventory)
                return true;

            // Cases vanilla handles specially: let it.
            if (__instance.kindDef.destroyGearOnDrop || __instance.InContainerEnclosed || !__instance.SpawnedOrAnyParentSpawned)
                return true;

            IntVec3 pos = __instance.PositionHeld;

            // Whatever was carried in hands (a haul, a pawn) always falls.
            if (__instance.carryTracker?.CarriedThing != null)
                __instance.carryTracker.TryDropCarriedThing(pos, ThingPlaceMode.Near, out _);

            if (keepInventoryAndEquipmentIfInBed && __instance.InBed())
                return false;

            if (!keepEquipment)
                __instance.equipment?.DropAllEquipment(pos, forbid: true, rememberPrimary);

            if (!keepInventory && __instance.inventory != null && __instance.inventory.innerContainer.TotalStackCount > 0)
                __instance.inventory.DropAllNearPawn(pos, forbid: true);

            return false;
        }
    }

    /// <summary>
    /// Vanilla drops all equipment when a downed pawn is spawned (loading a map, resurrection
    /// while downed, shambler rising). Its 1.6 body does nothing else.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_PawnSpawned))]
    public static class Pawn_EquipmentTracker_Notify_PawnSpawned_Patch
    {
        public static bool Prepare() => ShamblerArmsMod.OwnKeepGearActive;

        public static bool Prefix(Pawn_EquipmentTracker __instance)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.kindDef.destroyGearOnDrop)
                return true;
            return !ShamblerArmsMod.Settings.keepEquipmentOnDowned;
        }
    }
}
