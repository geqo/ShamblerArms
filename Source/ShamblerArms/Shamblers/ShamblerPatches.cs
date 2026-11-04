using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ShamblerArms.Shamblers
{
    /// <summary>Roll intellect the moment a pawn turns into a shambler, before its gear is handled.</summary>
    [HarmonyPatch(typeof(Pawn_MutantTracker), nameof(Pawn_MutantTracker.Turn))]
    public static class Pawn_MutantTracker_Turn_Patch
    {
        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static void Prefix(Pawn_MutantTracker __instance, Pawn ___pawn)
        {
            if (__instance.Def == MutantDefOf.Shambler)
                ShamblerIntellect.Ensure(___pawn);
        }
    }

    /// <summary>
    /// Vanilla HandleEquipment always drops the primary weapon. Keep it if the shambler's tier
    /// allows it; the rest (apparel) is the vanilla 1.6 logic.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MutantTracker), "HandleEquipment")]
    public static class Pawn_MutantTracker_HandleEquipment_Patch
    {
        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static bool Prefix(Pawn_MutantTracker __instance, Pawn ___pawn)
        {
            Pawn pawn = ___pawn;
            MutantDef def = __instance.Def;
            if (def != MutantDefOf.Shambler)
                return true;

            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary == null || !ShamblerIntellect.CanWield(ShamblerIntellect.TierOf(pawn), primary.def))
                return true;

            HandleApparel(pawn, def);
            return false;
        }

        private static void HandleApparel(Pawn pawn, MutantDef def)
        {
            if (pawn.apparel == null)
                return;
            if (def.disableApparel)
            {
                if (pawn.MapHeld != null)
                    pawn.apparel.DropAll(pawn.Position);
                else
                    pawn.apparel.DestroyAll();
            }
            if (!def.isConsideredCorpse)
                return;
            foreach (Apparel item in pawn.apparel.WornApparel)
            {
                if (item.def.apparel.careIfWornByCorpse)
                    item.WornByCorpse = true;
            }
        }
    }

    /// <summary>
    /// Shamblers have Shooting disabled, so the skill reads as 0. For Gunner+ return what the
    /// living pawn had, scaled by intellect.
    /// </summary>
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.GetLevel))]
    public static class SkillRecord_GetLevel_Patch
    {
        private static readonly AccessTools.FieldRef<SkillRecord, Pawn> PawnRef =
            AccessTools.FieldRefAccess<SkillRecord, Pawn>("pawn");

        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static void Postfix(SkillRecord __instance, ref int __result)
        {
            if (__result != 0 || __instance.def != SkillDefOf.Shooting || __instance.levelInt <= 0)
                return;
            Pawn pawn = PawnRef(__instance);
            if (ShamblerIntellect.TierOf(pawn, assign: false) < ShamblerTier.Gunner)
                return;
            __result = Mathf.Clamp(Mathf.RoundToInt(__instance.levelInt * ShamblerIntellect.ValueOf(pawn)), 0, 20);
        }
    }

    /// <summary>
    /// Vanilla shamblers never look for a firing position. Gunners get a short-range position
    /// without cover, Tacticians get the regular raider logic.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_ShamblerFight), "TryFindShootingPosition")]
    public static class JobGiver_ShamblerFight_TryFindShootingPosition_Patch
    {
        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static bool Prefix(Pawn pawn, ref IntVec3 dest, Verb verbToUse, ref bool __result)
        {
            ShamblerTier tier = ShamblerIntellect.TierOf(pawn);
            if (tier < ShamblerTier.Gunner)
                return true;

            Thing target = pawn.mindState.enemyTarget;
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(target);
            if (target == null || verb == null || verb.verbProps.IsMeleeAttack)
                return true;

            bool tactician = tier >= ShamblerTier.Tactician;
            var request = new CastPositionRequest
            {
                caster = pawn,
                target = target,
                verb = verb,
                maxRangeFromTarget = tactician ? verb.EffectiveRange : Mathf.Min(verb.EffectiveRange, ShamblerIntellect.GunnerMaxRange),
                wantCoverFromTarget = tactician && verb.EffectiveRange > 5f
            };
            __result = CastPositionFinder.TryFindCastPosition(request, out dest);
            return false;
        }
    }

    /// <summary>
    /// A shambler with a gun that has no firing position would otherwise just wander off.
    /// Make it charge and bash instead, as a shambler should.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_ShamblerFight), "TryGiveJob")]
    public static class JobGiver_ShamblerFight_TryGiveJob_Patch
    {
        private static readonly Func<JobGiver_ShamblerFight, Pawn, Thing, Job> MeleeAttackJob =
            AccessTools.MethodDelegate<Func<JobGiver_ShamblerFight, Pawn, Thing, Job>>(
                AccessTools.Method(typeof(JobGiver_ShamblerFight), "MeleeAttackJob"));

        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static void Postfix(JobGiver_ShamblerFight __instance, Pawn pawn, ref Job __result)
        {
            if (__result != null)
                return;
            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary == null || !primary.def.IsRangedWeapon)
                return;
            Thing target = pawn.mindState.enemyTarget;
            if (target == null || (target is Pawn p && p.IsPsychologicallyInvisible()))
                return;
            __result = MeleeAttackJob(__instance, pawn, target);
        }
    }
}
