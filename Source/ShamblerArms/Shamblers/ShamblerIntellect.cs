using RimWorld;
using UnityEngine;
using Verse;

namespace ShamblerArms
{
    /// <summary>Order must match the stages of ShamblerArms_Intellect.</summary>
    public enum ShamblerTier
    {
        Mindless = 0,  // vanilla: drops its weapon when it turns
        Brawler = 1,   // keeps melee weapons
        Gunner = 2,    // keeps any weapon, shoots from short range
        Tactician = 3  // full ranged AI with cover, picks up weapons on the way
    }

    [DefOf]
    public static class ShamblerArmsDefOf
    {
        public static HediffDef ShamblerArms_Intellect;

        static ShamblerArmsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ShamblerArmsDefOf));
    }

    public static class ShamblerIntellect
    {
        /// <summary>Max distance to target for a Gunner's firing position.</summary>
        public const float GunnerMaxRange = 12f;

        public static bool IsHumanlikeShambler(Pawn pawn) =>
            pawn?.mutant != null
            && pawn.mutant.Def == MutantDefOf.Shambler
            && pawn.RaceProps.Humanlike;

        public static Hediff Get(Pawn pawn) =>
            pawn.health?.hediffSet?.GetFirstHediffOfDef(ShamblerArmsDefOf.ShamblerArms_Intellect);

        /// <summary>Returns the intellect hediff, rolling a new one for a shambler that has none.</summary>
        public static Hediff Ensure(Pawn pawn)
        {
            if (!IsHumanlikeShambler(pawn) || pawn.Dead)
                return null;

            Hediff hediff = Get(pawn);
            if (hediff != null)
                return hediff;

            ShamblerArmsSettings s = ShamblerArmsMod.Settings;
            float value = Mathf.Clamp01(s.intellectLevel + Rand.Range(-s.intellectSpread, s.intellectSpread));

            hediff = HediffMaker.MakeHediff(ShamblerArmsDefOf.ShamblerArms_Intellect, pawn);
            // Severity 0 would remove the hediff; 0.001 still reads as Mindless.
            hediff.Severity = Mathf.Max(value, 0.001f);
            pawn.health.AddHediff(hediff);
            return hediff;
        }

        /// <param name="assign">Roll intellect for shamblers from older saves. Never pass true from stat code.</param>
        public static ShamblerTier TierOf(Pawn pawn, bool assign = true)
        {
            if (!IsHumanlikeShambler(pawn))
                return ShamblerTier.Mindless;
            Hediff hediff = assign ? Ensure(pawn) : Get(pawn);
            return hediff == null ? ShamblerTier.Mindless : (ShamblerTier)hediff.CurStageIndex;
        }

        public static float ValueOf(Pawn pawn) => Get(pawn)?.Severity ?? 0f;

        /// <summary>Intellect allows this kind of weapon (melee for Brawler, anything for Gunner+).</summary>
        public static bool TierAllows(ShamblerTier tier, ThingDef weapon)
        {
            if (weapon == null || !weapon.IsWeapon)
                return false;
            if (tier >= ShamblerTier.Gunner)
                return true;
            return tier >= ShamblerTier.Brawler && weapon.IsMeleeWeapon;
        }

        /// <summary>Intellect allows it and the shambler has enough working hands.</summary>
        public static bool CanWield(Pawn pawn, ShamblerTier tier, ThingDef weapon) =>
            TierAllows(tier, weapon) && ShamblerBody.HasHandsFor(pawn, weapon);
    }
}
