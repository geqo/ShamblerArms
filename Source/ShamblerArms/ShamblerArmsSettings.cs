using UnityEngine;
using Verse;

namespace ShamblerArms
{
    public class ShamblerArmsSettings : ModSettings
    {
        // Item keeping (ignored while Non Uno Pinata is active).
        public bool keepEquipmentOnDeath = true;
        public bool keepInventoryOnDeath = true;
        public bool keepEquipmentOnDowned = true;
        public bool keepInventoryOnDowned = true;

        // Shambler intellect: each new shambler gets level ± spread, clamped to 0..1.
        public float intellectLevel = 0.5f;
        public float intellectSpread = 0.25f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref keepEquipmentOnDeath, "keepEquipmentOnDeath", true);
            Scribe_Values.Look(ref keepInventoryOnDeath, "keepInventoryOnDeath", true);
            Scribe_Values.Look(ref keepEquipmentOnDowned, "keepEquipmentOnDowned", true);
            Scribe_Values.Look(ref keepInventoryOnDowned, "keepInventoryOnDowned", true);
            Scribe_Values.Look(ref intellectLevel, "intellectLevel", 0.5f);
            Scribe_Values.Look(ref intellectSpread, "intellectSpread", 0.25f);
        }

        public void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);

            Text.Font = GameFont.Medium;
            list.Label("ShamblerArms_KeepGearHeader".Translate());
            Text.Font = GameFont.Small;

            if (ShamblerArmsMod.OwnKeepGearActive)
            {
                list.CheckboxLabeled("ShamblerArms_KeepEquipmentOnDeath".Translate(), ref keepEquipmentOnDeath);
                list.CheckboxLabeled("ShamblerArms_KeepInventoryOnDeath".Translate(), ref keepInventoryOnDeath);
                list.CheckboxLabeled("ShamblerArms_KeepEquipmentOnDowned".Translate(), ref keepEquipmentOnDowned);
                list.CheckboxLabeled("ShamblerArms_KeepInventoryOnDowned".Translate(), ref keepInventoryOnDowned);
            }
            else
            {
                list.Label("ShamblerArms_NonUnoPinataActive".Translate());
            }

            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("ShamblerArms_IntellectHeader".Translate());
            Text.Font = GameFont.Small;

            if (!ModsConfig.AnomalyActive)
                list.Label("ShamblerArms_NoAnomaly".Translate());

            intellectLevel = list.SliderLabeled(
                "ShamblerArms_IntellectLevel".Translate(intellectLevel.ToStringPercent()),
                intellectLevel, 0f, 1f, 0.5f, "ShamblerArms_IntellectLevelTip".Translate());
            intellectSpread = list.SliderLabeled(
                "ShamblerArms_IntellectSpread".Translate(intellectSpread.ToStringPercent()),
                intellectSpread, 0f, 0.5f, 0.5f, "ShamblerArms_IntellectSpreadTip".Translate());

            float lo = Mathf.Clamp01(intellectLevel - intellectSpread);
            float hi = Mathf.Clamp01(intellectLevel + intellectSpread);
            list.Label("ShamblerArms_IntellectRange".Translate(lo.ToStringPercent(), hi.ToStringPercent()));
            list.Label("ShamblerArms_TierHelp".Translate());

            list.End();
        }
    }
}
