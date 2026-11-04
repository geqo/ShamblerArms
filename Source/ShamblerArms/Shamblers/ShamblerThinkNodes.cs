using RimWorld;
using Verse;
using Verse.AI;

namespace ShamblerArms
{
    public class ThinkNode_ConditionalShamblerTier : ThinkNode_Conditional
    {
        public ShamblerTier minTier = ShamblerTier.Tactician;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var copy = (ThinkNode_ConditionalShamblerTier)base.DeepCopy(resolve);
            copy.minTier = minTier;
            return copy;
        }

        protected override bool Satisfied(Pawn pawn) => ShamblerIntellect.TierOf(pawn) >= minTier;
    }

    /// <summary>
    /// Picks up a better weapon lying close by (on the way, not a trip across the map).
    /// Ranged beats melee; a melee weapon only replaces bare hands.
    /// </summary>
    public class JobGiver_ShamblerPickUpWeapon : ThinkNode_JobGiver
    {
        public float searchRadius = 8f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var copy = (JobGiver_ShamblerPickUpWeapon)base.DeepCopy(resolve);
            copy.searchRadius = searchRadius;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.equipment == null || !pawn.Spawned || pawn.Downed || pawn.GetRegion() == null)
                return null;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return null;

            ShamblerTier tier = ShamblerIntellect.TierOf(pawn);
            int currentScore = Score(pawn.equipment.Primary?.def, tier);
            if (currentScore >= 2)
                return null; // already has a gun

            bool playerOwned = pawn.Faction == Faction.OfPlayer;
            Thing weapon = GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Weapon),
                PathEndMode.OnCell, TraverseParms.For(pawn), searchRadius,
                t => Score(t.def, tier) > currentScore
                     && !t.IsBurning()
                     && !t.IsForbidden(pawn)
                     && !(playerOwned && t.IsInAnyStorage()) // don't raid the colony's armory
                     && EquipmentUtility.CanEquip(t, pawn)
                     && pawn.CanReserve(t),
                searchRegionsMax: 15);
            if (weapon == null)
                return null;

            Job job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            job.expiryInterval = 600; // shamblers are slow
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static int Score(ThingDef def, ShamblerTier tier)
        {
            if (def == null || !ShamblerIntellect.CanWield(tier, def))
                return 0;
            return def.IsRangedWeapon ? 2 : 1;
        }
    }
}
