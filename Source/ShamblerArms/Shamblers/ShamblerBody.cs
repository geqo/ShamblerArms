using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ShamblerArms
{
    /// <summary>
    /// How many hands a weapon needs. Vanilla has no such notion; vanilla and DLC weapons get it
    /// from Patches/WeaponHands.xml, modded weapons fall back to a mass heuristic.
    /// </summary>
    public class WeaponHandsExtension : DefModExtension
    {
        public int hands = 2;
    }

    public static class ShamblerBody
    {
        // Fallback for weapons without WeaponHandsExtension.
        private const float MaxOneHandedRangedMass = 1.5f; // pistols
        private const float MaxOneHandedMeleeMass = 2.5f;  // swords, maces, axes

        public static int HandsRequired(ThingDef weapon)
        {
            WeaponHandsExtension ext = weapon.GetModExtension<WeaponHandsExtension>();
            if (ext != null)
                return ext.hands;
            float limit = weapon.IsRangedWeapon ? MaxOneHandedRangedMass : MaxOneHandedMeleeMass;
            return weapon.BaseMass <= limit ? 1 : 2;
        }

        /// <summary>
        /// Counts working hands: an arm (ManipulationLimbCore) counts if the arm and its hand
        /// (child ManipulationLimbSegment) are present and not fully disabled. Fingers don't matter.
        /// </summary>
        public static int WorkingHands(Pawn pawn)
        {
            HediffSet hediffs = pawn.health.hediffSet;
            int count = 0;
            List<BodyPartRecord> arms = pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.ManipulationLimbCore);
            for (int i = 0; i < arms.Count; i++)
            {
                BodyPartRecord arm = arms[i];
                if (!Works(hediffs, arm))
                    continue;

                bool hasHandPart = false;
                bool handWorks = false;
                foreach (BodyPartRecord part in arm.parts)
                {
                    if (!part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment))
                        continue;
                    hasHandPart = true;
                    if (Works(hediffs, part))
                    {
                        handWorks = true;
                        break;
                    }
                }
                // Bodies whose arms have no hand part (some modded races): the arm itself is the hand.
                if (handWorks || !hasHandPart)
                    count++;
            }
            return count;
        }

        private static bool Works(HediffSet hediffs, BodyPartRecord part) =>
            !hediffs.PartIsMissing(part) && PawnCapacityUtility.CalculatePartEfficiency(hediffs, part) > 0f;

        public static bool HasHandsFor(Pawn pawn, ThingDef weapon) => WorkingHands(pawn) >= HandsRequired(weapon);

        /// <summary>0..1 — only eyes count (vanilla Sight ignores consciousness); bionics don't extend range.</summary>
        public static float SightFactor(Pawn pawn) =>
            Mathf.Clamp01(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight));
    }
}
