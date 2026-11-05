using System.Collections.Generic;
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
    /// Picks up the best weapon it can see nearby — not the closest one, so a shambler doesn't
    /// grab a sword and then walk on to the SMG next to it. Ranged beats melee; ties go to the closer.
    /// Radius = setting × sight (no eyes, no pickup). Needs enough working hands for the weapon.
    /// </summary>
    public class JobGiver_ShamblerPickUpWeapon : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.equipment == null || !pawn.Spawned || pawn.Downed)
                return null;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return null;

            ShamblerTier tier = ShamblerIntellect.TierOf(pawn);
            int currentScore = Score(pawn, pawn.equipment.Primary?.def, tier);
            if (currentScore >= 2)
                return null; // already has a usable gun

            float radius = ShamblerArmsMod.Settings.pickupRadius * ShamblerBody.SightFactor(pawn);
            if (radius < 1f)
                return null;
            float radiusSq = radius * radius;

            bool playerOwned = pawn.Faction == Faction.OfPlayer;
            Thing best = null;
            int bestScore = currentScore;
            float bestDistSq = float.MaxValue;

            List<Thing> weapons = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            for (int i = 0; i < weapons.Count; i++)
            {
                Thing t = weapons[i];
                float distSq = (t.Position - pawn.Position).LengthHorizontalSquared;
                if (distSq > radiusSq)
                    continue;
                int score = Score(pawn, t.def, tier);
                if (score <= currentScore || score < bestScore || (score == bestScore && distSq >= bestDistSq))
                    continue;
                if (t.IsBurning()
                    || t.IsForbidden(pawn)
                    || (playerOwned && t.IsInAnyStorage()) // don't raid the colony's armory
                    || !EquipmentUtility.CanEquip(t, pawn)
                    || !pawn.CanReserve(t)
                    || !pawn.CanReach(t, PathEndMode.OnCell, Danger.Deadly))
                    continue;
                best = t;
                bestScore = score;
                bestDistSq = distSq;
            }
            if (best == null)
                return null;

            Job job = JobMaker.MakeJob(JobDefOf.Equip, best);
            job.expiryInterval = 600; // shamblers are slow
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static int Score(Pawn pawn, ThingDef def, ShamblerTier tier)
        {
            if (def == null || !ShamblerIntellect.CanWield(pawn, tier, def))
                return 0;
            return def.IsRangedWeapon ? 2 : 1;
        }
    }
}
