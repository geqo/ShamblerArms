# Shambler Arms

A RimWorld **1.6** mod (1.7 support is planned). Requires Harmony.
The shambler part needs the Anomaly DLC. Available in English and Russian.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3834113610

## Features

1. **Dead and downed pawns keep their gear.** Weapons and inventory stay on the pawn or corpse
   (four toggles in the mod settings, all on by default). Whatever the pawn was carrying in
   its hands still falls. Use the vanilla Strip order to take the gear off.
   Vanilla behaviour is kept for gladiator duels (the ritual needs the loser's weapon to drop),
   pawns in bed, `destroyGearOnDrop` pawn kinds, and pawns inside containers or off the map.
   If **Non Uno Pinata** is active, this whole part is switched off (its patches are not applied).
2. **Armed shamblers.** A shambler raised from an armed pawn can keep its weapon.
   No weapons are handed out: shamblers spawned by events arrive unarmed, as in vanilla.
3. **Shambler intellect** is a hediff, `ShamblerArms_Intellect`, shown on the Health tab.
   It is rolled once, when the shambler rises: `level ± spread` from the mod settings.

| Intellect | Tier | What it can do |
|---|---|---|
| < 25% | mindless | vanilla: drops its weapon |
| 25%+ | brawler | keeps melee weapons; +5% consciousness |
| 50%+ | gunner | keeps any weapon, finds a firing position within 12 cells without cover; Shooting skill = the living pawn's skill × intellect; +10% consciousness |
| 75%+ | tactician | uses firing positions at full weapon range and takes cover; picks up the best weapon within the search radius (default 6 cells × sight); +15% consciousness |

Tier thresholds and consciousness bonuses live in `Defs/HediffDefs/ShamblerIntellect.xml`.
A gunner with a firearm and no firing position charges into melee instead of wandering off.
Player-owned shamblers don't take weapons from stockpiles or forbidden ones.

**Weapon pickup.** The shambler takes the best weapon within the radius, not the closest one:
a firearm beats a melee weapon, and between equals the closer one wins. The radius is
multiplied by sight (0..1): damaged or missing eyes shrink it, and a blind shambler picks
nothing up.

**Hands.** Holding a weapon needs a working arm with a hand; two-handed weapons need two.
Without enough hands a shambler drops its weapon when it rises, won't pick one up, and drops
the weapon it holds if it loses an arm or hand in combat. Vanilla and DLC weapons are tagged
in `Patches/WeaponHands.xml`:
- one-handed: pistols, grenades, pila, knives, swords, axes, maces, club;
- two-handed: bows, rifles, shotguns, SMGs, heavy weapons, spear, hammers, staff.

Weapons from other mods count as one-handed if they are ranged up to 1.5 kg or melee up to 2.5 kg.

## Building

```sh
cd Source/ShamblerArms
dotnet build -c Release          # → 1.6/Assemblies/ShamblerArms.dll
# point it at your game: -p:RimWorldManaged=/path/to/RimWorld/RimWorld*_Data/Managed
```

Requires .NET SDK 8+ and NuGet access (the net472 reference assemblies and Lib.Harmony are
compile-time only). `0Harmony.dll` is not shipped: the Harmony mod provides it (2.4.3 on 1.6).

## How it works

| File | Patches |
|---|---|
| `KeepGear/KeepGearPatches.cs` | `Pawn.Kill` and `Pawn_HealthTracker.MakeDowned` mark a scope; a prefix on `Pawn.DropAndForbidEverything` keeps the gear inside that scope; `Pawn_EquipmentTracker.Notify_PawnSpawned` no longer drops a downed pawn's weapon |
| `Shamblers/ShamblerPatches.cs` | `Pawn_MutantTracker.Turn` (roll intellect), `HandleEquipment` (keep the weapon), `Pawn_HealthTracker.AddHediff` (drop the weapon after losing hands), `SkillRecord.GetLevel` (Shooting skill), `JobGiver_ShamblerFight.TryFindShootingPosition` / `TryGiveJob`, `JobDriver_Wait.CheckForAutoAttack` (vanilla never lets shamblers shoot from a firing position) |
| `Shamblers/ShamblerBody.cs` | working hands, hands needed per weapon, sight factor |
| `Shamblers/ShamblerThinkNodes.cs` | tier condition and weapon pickup |
| `Defs/ThinkTreeDefs`, `Patches/ShamblerSwarm.xml` | weapon pickup for lone shamblers (`Shambler_PreMain` insert tag) and for swarms (`ShamblerSwarm` duty) |
| `Languages/English`, `Languages/Russian` | settings text, intellect hediff and tier names |

## In-game test checklist (dev mode)

- [ ] Kill a colonist or a raider with a weapon: weapon and inventory stay on the corpse, Strip removes them.
- [ ] Down a pawn: the weapon stays with it, including after save and load.
- [ ] Gladiator duel (Ideology): the loser's weapon drops.
- [ ] Raise a shambler from an armed corpse (dev: Resurrect as shambler) at 0% / 30% / 60% / 90% intellect: tier and behaviour match the table.
- [ ] A gunner with a rifle shoots, and charges into melee when it has no line of fire.
- [ ] An unarmed tactician picks up the best weapon nearby, not the closest one.
- [ ] A one-armed shambler takes only one-handed weapons; cutting off a hand makes it drop a two-handed one.
- [ ] Death pall / shambler swarm: no errors in the log.
- [ ] With Non Uno Pinata: the log says "Non Uno Pinata detected" and the keep-gear toggles are hidden.

## License

GPL-3.0, see `LICENSE`.
