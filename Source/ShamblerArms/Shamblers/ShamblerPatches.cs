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
            if (primary == null || !ShamblerIntellect.CanWield(pawn, ShamblerIntellect.TierOf(pawn), primary.def))
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
    /// A shambler that loses an arm or hand (amputation, part shot off) drops a weapon it no longer
    /// has hands for. The HediffDef overload of AddHediff delegates to this one.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Pawn_HealthTracker_AddHediff_Patch
    {
        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static void Postfix(Pawn ___pawn, Hediff hediff)
        {
            Pawn pawn = ___pawn;
            if (hediff?.Part == null || !IsArmOrHand(hediff.Part))
                return;
            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary == null || pawn.Dead || !pawn.SpawnedOrAnyParentSpawned || !ShamblerIntellect.IsHumanlikeShambler(pawn))
                return;
            if (ShamblerBody.HasHandsFor(pawn, primary.def))
                return;
            pawn.equipment.TryDropEquipment(primary, out _, pawn.PositionHeld, forbid: false);
        }

        /// <summary>The part or one of its ancestors is a shoulder/arm/hand (fingers don't count).</summary>
        private static bool IsArmOrHand(BodyPartRecord part)
        {
            for (BodyPartRecord p = part; p != null; p = p.parent)
            {
                var tags = p.def.tags;
                if (tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) || tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment))
                    return true;
            }
            return false;
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
    /// Shooting from a firing position happens in Wait_Combat via JobDriver_Wait.CheckForAutoAttack,
    /// which vanilla skips entirely for shamblers (`|| pawn.IsShambler` → return). For Gunner+
    /// with a ranged weapon we run the ranged part ourselves; everyone else stays vanilla.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class JobDriver_Wait_CheckForAutoAttack_Patch
    {
        public static bool Prepare() => ModsConfig.AnomalyActive;

        public static bool Prefix(JobDriver_Wait __instance)
        {
            Pawn pawn = __instance.pawn;
            if (!pawn.IsShambler || ShamblerIntellect.TierOf(pawn) < ShamblerTier.Gunner)
                return true;

            Job job = __instance.job;
            if (job == null || job.def != JobDefOf.Wait_Combat || !job.canUseRangedWeapon)
                return false;
            if (pawn.Downed || pawn.stances.FullBodyBusy || pawn.IsCarryingPawn())
                return false;
            if (pawn.drafter != null && !pawn.drafter.FireAtWill)
                return false;

            Verb verb = pawn.CurrentEffectiveVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack)
                return false;

            // Prefer the target the shambler AI already chose (it respects shambler targeting rules).
            Thing target = pawn.mindState.enemyTarget;
            if (target == null || !target.Spawned || !verb.CanHitTarget(target))
            {
                var flags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
                if (verb.IsIncendiary_Ranged())
                    flags |= TargetScanFlags.NeedNonBurning;
                target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(pawn, flags);
            }

            if (target != null && pawn.TryStartAttack(target))
                __instance.collideWithPawns = true;
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
