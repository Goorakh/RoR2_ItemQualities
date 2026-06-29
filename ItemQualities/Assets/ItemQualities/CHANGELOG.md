## <Next Version>

* Reworks:
    * Substandard Duplicator
    * Unstable Transmitter
    * Medkit

* Scrap:
    * Quality Scrap can now be traded for regular Scrap with a special scrapper interactable on the moon.
    * Quality Scrap no longer automatically turns into regular Scrap when entering the moon.

* Regenerating Scrap:
    * Quality Regenerating Scrap is now the highest priority scrap, always being taken first if available.
        * For cauldrons, scrap priority has been modified to take as few quality regenerating scraps as possible, so that you can properly benefit from having several quality regenerating scrap.
            * Priority when taking items is now:
                * 1 of the highest quality Regenerating Scrap available
                * Normal Regenerating Scrap
                * Normal Scrap
                * Any remaining quality regenerating scrap, starting with the lowest quality
                * Random items from inventory
        * Cauldrons now give their item in the quality of the highest input scrap. Before now, they gave the average of all input scrap, which just tended to punish you for having a high and a low quality regenerating scrap by reducing the average quality of the resulting item.

* Added ProperSave support.

* Remote Caffeinator:
    * Now increases max number of activations per quality.

* Disposable Missile Launcher:
    * Fixed missile increase not counting common Dml.

* Bottled Chaos:
    * Fixed no additional equipment effects triggering when having both a quality Bottled Chaos and a regular one.

* Executive Card:
    * Fixed the small chests on Gilded Coast not being targetable.

* Jade Elephant:
    * Fixed barrier gain being applied when using the non-quality version of the equipment.

* Sticky Bomb:
    * Fixed size not changing on client

* Eccentric Vase:
    * Fixed orb teleport not working when at less than 60fps.

* Fixed compatibility issues with mods that add projectiles with no explosion radius resulting in a divide by zero error.

* Fixed Artifact of Sacrifice not being able to drop quality equipments.

## 0.7.6 Changes:

* Symbiotic Scorpion
    * Chance on hit to apply venom for 10 seconds:
        * Uncommon: 2%
        * Rare: 3%
        * Epic: 4%
        * Legendary: 5%
    * Venom deals damage every second for every damage over time effect applied
        * Uncommon: 50% (+50% per stack)
        * Rare: 100% (+100% per stack)
        * Epic: 150% (+150% per stack)
        * Legendary: 200% (+200% per stack)

* Crowbar:
    * Every 10th high-damage hit deals more damage.

* Stun Grenade:
    * Changed to Crowbar's previous effect.
    * Fixed immobilizing forces (Primordial Cube, Void Band, etc) not counting as immobilized.

* Tougher Times
    * No longer counts blocks from i-frames.
    * Invincibility duration per 10% damage blocked:
        * Uncommon: 0.1s (+0.1s per stack) -> 0.2s (+0.2s per stack)
        * Rare: 0.5s (+0.5s per stack) -> 0.5s (+0.5s per stack)
        * Epic: 1.5s (+1.5s per stack) -> 1s (+1s per stack)
        * Legendary: 2.5s (+2.5s per stack) -> 1.5s (+1.5s per stack)
    * Added maximum invincibility time:
        * Uncommon: 3s (+3s per stack)
        * Rare: 6s (+6s per stack)
        * Epic: 9s (+9s per stack)
        * Legendary: 12s (+12s per stack)

* AtG Missile Mk. 1:
    * Base missile chance:
        * Uncommon: 15% -> 12%
        * Rare: 20% -> 14%
        * Epic: 25% -> 16%
        * Legendary: 30% -> 20%
        
* Ben's Raincoat:
    * Reflected debuff count:
        * Uncommon: 2 -> 5
        * Rare: 3 -> 8
        * Epic: 4 -> 12
        * Legendary: 5 -> 15

* Blast Shower:
    * Cleanse duration:
        * Uncommon: 1s -> 2s
        * Rare: 2s -> 3s
        * Epic: 4s -> 5s
        * Legendary: 5s -> 6s

* Sawmerang:
    * Linger duration changed to 1 second for all qualities.

* Infusion:
    * Health gained per boss kill:
        * Uncommon: 5 -> 10
        * Rare: 15 -> 20
        * Epic: 30 -> 35
        * Legendary: 50 -> 55

* Gnarled Woodsprite:
    * Fixed ghost drones being usable in drone combiners and scrappers.
    * Fixed ghost drones not being the same tier as the drone being cloned.

* Bison Steak:
    * Now increases health for every boss or main stage objective completed.
    * Health per boss or stage objective:
        * Uncommon: 35 -> 40
        * Rare: 75 -> 80
        * Epic: 150 -> 130
        * Legendary: 250 -> 200

* Foreign Fruit:
    * Fixed Cautious Slug affecting Foreign Fruit maximum temporary health, and vice versa.

* Armor Piercing Rounds:
    * Marks no longer disappear sometimes when losing and regaining the item.

* Razorwire:
    * Fixed bonus damage being reduced by armor.

* Lysate Cell:
    * Fixed bonus damage not applying to Engineer Turrets.

* Newly Hatched Zoea:
    * Allies now spawn with the appropriate quality tier.

* Eccentric Vase:
    * Added maximum teleport distance (1000m, same as vase placement)

* Fuel Array:
    * Not telling you.

## 0.7.5

* Recycler:
    * Can now reroll quality printers.

* Soulbound Catalyst:
    * Fixed item removing quality status of equipment on kill.

* Rose Buckler:
    * Cooldown now displays time remaining until it ends.

* Eclipse Lite:
    * Fixed overflow barrier not displaying correctly with curse.

* Backup Magazine:
    * Fixed refresh not working for Operator swarm.

## 0.7.4

* Fix changelog.

## 0.7.3

* Crafting:
    * Fixed missing recipes for some items + qualities combinations.
    * Combining a quality item with a common item will now give a common result (previously did not result in anything).
    * For recipes with a result without qualities (such as meal items), any combination of quality ingredients can now be used to craft them.

* Eclipse Lite:
    * Now increases max barrier capacity:
        * Uncommon: +30% (+30% per stack)
        * Rare: +60% (+60% per stack)
        * Epic: +100% (+100% per stack)
        * Legendary: +150% (+150% per stack)
    * Barrier gain on equipment use per second cooldown:
        * Uncommon: 0.5% (+0.5% per stack) -> 1% (+1% per stack)
        * Rare: 1% (+1% per stack) -> 2% (+2% per stack)
        * Epic: 3% (+3% per stack) -> 3% (+3% per stack)
        * Legendary: 5% (+5% per stack) -> 4% (+4% per stack)

* Will-o'-the-wisp:
    * Explosion radius increase:
        * Uncommon: 15% (+15% per stack) -> 20% (+20% per stack)
        * Rare: 25% (+25% per stack) -> 40% (+40% per stack)
        * Epic: 50% (+50% per stack) -> 70% (+70% per stack)
        * Legendary: 70% (+70% per stack) -> 100% (+100% per stack)

* Soulbound Catalyst:
    * Large monster kills no longer get extra cooldown reduction
    * Large monster kills grant temporary equipment charges:
        * Uncommon: 1 (+1 per stack)
        * Rare: 2 (+2 per stack)
        * Epic: 3 (+3 per stack)
        * Legendary: 4 (+4 per stack)

* Foreign Fruit:
    * Added a cap to temporary health amount.
    * Fixed temporary health being counted when determining max health %, causing temporary health to increase exponentially as the equipment was used repeatedly.

* Armor Piercing Rounds:
    * Fixed being able to mark certain non-enemies as targets (scorcher puddles, heat vents, etc).

* Rose Buckler:
    * Fixed dash not applying weaken.

* Backup Magazine:
    * Fixed being able to spam loader grapple to gain all charges back. The refresh now rolls when the grapple hits.
    * Fixed refresh not working on Railgunner.

* Warhorn:
    * Fixed quality effect not stacking.

* Warped Echo:
    * Fixed elite death effects being repeated:
        * Glacial Explosion
        * Malchite Turret
        * Mending healing orb
        * Voidtouched infestor
    * Fixed effects from artifacts being repeated:
        * Soul
        * Spite
        * Vengeance
        * Sacrifice

* Eccentric Vase:
    * Highlighted interactables are now prioritized over teleporting to an orb.

* Squid Polyp:
    * Fixed squids not changing size for non-host players.
    * Fixed squids not increasing duration on kill after upgrading to max.

* Super Massive Leech:
    * Movement speed is now increased by any healing received while leech is active, and not just leeched health.

* Executive Card:
    * Fixed being able to copy Scavenger Bags.
    * Giving the equipment to a drone will now transfer your stored interactable to it.

* Disposable Missile Launcher:
    * Placing the missile launcher in an equipment drone will now transfer the extra missile count to the drone.

* Blast Shower:
    * Fixed Void Fields fog damage increasing rapidly when used inside it.

* Bandolier:
    * Fixed being able to give charges to certain skills that do not use charges to activate.

* Goobo Jr.:
    * Clones are now upgraded to the appropriate quality tier.

* Drifter Salvage & Upgraded Junk Drones can no longer drop quality temporary items.

* Seed of Life:
    * Fixed being able to upgrade scrap (regenerating scrap can still be upgraded).

## 0.7.2

* Goobo Jr.:
    * Goobo clones no longer copy equipment.

* Fixed Volcanic Egg descriptions.

* Fixed Super Massive Leech descriptions

## 0.7.1

* Forgive Me Please:
    * Fixed Sonorous Whispers being able to drop items from the doll.

* Warped Echo:
    * Fixed Sonorous Whispers drops being repeated.

* Rose Buckler:
    * Fixed dash triggering immediately when gaining the item while moving.

* Milky Chrysalis:
    * Removed duration increase when collecting a bug.
    * Bugs gained per pickup: 1 ->
        * Uncommon: 2
        * Rare: 3
        * Epic: 4
        * Legendary: 5

* Volcanic Egg:
    * Fixed quality effect not working when used by non-host players.
    * Explosion damage bonus per enemy hit:
        * Uncommon: +20% -> +300%
        * Rare: +50% -> +450%
        * Epic: +100% -> 650%
        * Legendary: +200% -> +850%

* Molotov:
    * Burn area expansion amount:
        * Uncommon: +70% -> +140%
        * Rare: +140% -> +300%
        * Epic: +300% -> +650%
        * Legendary: +650% -> +1100%

* Gnarled Woodsprite:
    * Fixed quality effect not working properly when used on non-host players.
    * Fixed ghosts being able to revive and becoming permanent.
    * Fixed ghosts' spawned minions being permanent.

* Recycler:
    * Fixed recycle indicator not showing for non-host players.

* Super Massive Leech:
    * Fixed incorrect values in descriptions.

## 0.7.0

* Added quality equipments

* Rose Buckler:
    * Double tap forward to do a short dash
    * Dash into enemies to weaken them:
        * Uncommon: 3 seconds (+3 seconds per stack)
        * Rare: 6 seconds (+6 seconds per stack)
        * Epic: 9 seconds (+9 seconds per stack)
        * Legendary: 12 seconds (+12 seconds per stack)

* Prayer Beads:
    * Monster level reduction:
        * Uncommon: 5 -> 2
        * Rare: 7 -> 4
        * Epic: 10 -> 6
        * Legendary: 15 -> 8

* Bison Steak
    * Health gained per teleporter:
        * Uncommon: 25 (+25 per stack) -> 35 (+35 per stack)
        * Rare: 50 (+50 per stack) -> 75 (+75 per stack)
        * Epic: 100 (+100 per stack) -> 150 (+150 per stack)
        * Legendary: Unchanged

* Pocket I.C.B.M.:
    * Extra missile chance:
        * Uncommon: 40% (+40% per stack) -> 50% (+50% per stack)
        * Rare: 75% (+75% per stack) -> 1 guaranteed (+1 per stack)
        * Epic: 1 guaranteed (+1 per stack) -> 1 guaranteed (+1 per stack) + 50% for additional (+50% per stack)
        * Legendary: 1 guaranteed (+1 per stack) + 50% for additional (+50% per stack) -> 2 guaranteed (+2 per stack) + 50% for additional (+50% per stack)

* Electric Boomerang:
    * Bounce chance:
        * Uncommon: 30% (+30% per stack) -> 75% (+75% per stack)
        * Rare: 50% (+50% per stack) -> 1 guaranteed (+1 per stack) + 50% for additional (+50% per stack)
        * Epic: 80% (+80% per stack) -> 3 guaranteed (+3 per stack)
        * Legendary: 1 guaranteed (+1 per stack) -> 5 guaranteed (+5 per stack)

* Warped Echo:
    * Chance to apply echo on hit:
        * Uncommon: 1% (+1% per stack)
        * Rare: 1.5% (+1.5% per stack)
        * Epic: 2% (+2% per stack)
        * Legendary: 2.5% (+2.5% per stack)
    * Repeat on kill effect per echo stack:
        * Uncommon: 1 time
        * Rare: 2 times
        * Epic: 3 times
        * Legendary: 4 times

* Resonance Disc:
    * Now always targets the last hit enemy when charged.

* AtG Missile Mk. 1
    * Increased big missile explosion radius: 3 -> 10
    * Increased chance for missile proc:
        * Uncommon: 15%
        * Rare: 20%
        * Epic: 25%
        * Legendary: 30%

* Razorwire:
    * Damage returned:
        * Uncommon: 10% (+10% per stack) -> 100% (+100% per stack)
        * Rare: 15% (+15% per stack) -> 200% (+200% per stack)
        * Epic: 20% (+20% per stack) -> 300% (+300% per stack)
        * Legendary: 25% (+25% per stack) -> 500% (+500% per stack)

* Wax Quail:
    * Removed air control increase.
    * Removed maximum angle to continue jump combo.
    * Increased window to continue jump combo slightly: 0.1s -> 0.15s

* Warbanner:
    * Now increases attack speed with time spent in the banner radius
        * Uncommon: +2% attack speed per second, up to 30 (+30 per stack) seconds
        * Rare: +3% attack speed per second, up to 40 (+40 per stack) seconds
        * Epic: +4% attack speed per second, up to 50 (+50 per stack) seconds
        * Legendary: +5% attack speed damage per second, up to 60 (+60 per stack) seconds
    * Temporary banner duration:
        * Uncommon: 3 (+3 per stack) seconds -> 10 (+10 per stack) seconds
        * Rare: 8 (+8 per stack) seconds -> 20 (+20 per stack) seconds
        * Epic: 15 (+15 per stack) seconds -> 30 (+30 per stack) seconds
        * Legendary: 30 (+30 per stack) seconds -> 40 (+40 per stack) seconds

* Armor Piercing Rounds:
    * Damage increase applies to bosses
    * Maximum distance for marked enemies: 250
    * Added new UI
    * Enemy mark frequency: 40 seconds
    * Damage increase per enemy killed:
        * Uncommon: 1% -> 1% (+1% per stack)
        * Rare: 1% -> 1.25% (+1.25% per stack)
        * Epic: 1% -> 1.5% (+1.5% per stack)
        * Legendary: 1% -> 2% (+2% per stack)
    * Enemy mark duration:
        * Uncommon: 10 seconds -> 15 seconds (+5 seconds per stack)
        * Rare: 10 seconds -> 20 seconds (+10 seconds per stack)
        * Epic: 10 seconds -> 25 seconds (+15 seconds per stack)
        * Legendary: 10 seconds -> 30 seconds (+20 seconds per stack)
    * Maximum ticks:
        * Uncommon: 15 (+15 per stack) -> 15
        * Rare: 30 (+30 per stack) -> 30
        * Epic: 45 (+45 per stack) -> 45
        * Legendary: 60 (+60 per stack) -> 60

* Ben's Raincoat:
    * Debuff spread radius:
        * Uncommon: 30m (+5m per stack) -> 15m (+15m per stack)
        * Rare: 35m (+10m per stack) -> 25m (+25m per stack)
        * Epic: 55m (+30m per stack) -> 35m (+35m per stack)
        * Legendary: 75m (+50m per stack) -> 50m (+50m per stack)
    * Spread debuff stacks: 1x ->
        * Uncommon: 2x
        * Rare: 3x
        * Epic: 4x
        * Legendary: 5x

* Faraday Spur:
    * Removed charge speed increase.
    * Charge used per jump: 100% ->
        * Uncommon: 75%
        * Rare: 50%
        * Epic: 25%
        * Legendary: 10%

* Squid Polyp rework:
    * Squids have a chance to upgrade on kill, increasing maximum health and damage.
        * Uncommon: 20%
        * Rare: 30%
        * Epic: 40%
        * Legendary: 50%
    * Squid base damage:
        * Uncommon: 110% (+10% per stack) -> 130% (+30% per stack)
        * Rare: 130% (+30% per stack) -> 140% (+40% per stack)
        * Epic: 160% (+60% per stack) -> 150% (+50% per stack)
        * Legendary: 200% (+100% per stack)
    * Removed base max health and duration increase.

* Rusted Key & Encrusted Key:
    * Fixed temporary quality keys not being used when opening a lockbox.

* Delicate Watch:
    * No longer counts parried damage as a hit
    * Fixed incorrect numbers in description of uncommon watch.

* Roll of Pennies:
    * Fixed bonus gold not spawning when opening a Collector's Barrel.

* Collector's Compulsion:
    * Made barrel pickups always target the player that opened the barrel.

* Ghor's Tome:
    * Fixed money buff not being set on pickup.

## 0.6.1

* Fixed quality item descriptions not showing properly if game was launched in a language other than English.

* Fixed quality item drop sounds playing globally.

## 0.6.0

* Quality items now play a unique sound when dropped.

* Crowbar rework:
    * Store damage dealt to stunned enemies, deal stored damage when enemy regains control.

* Armor Piercing Rounds rework:
    * Periodically marks an enemy for 10 seconds, deal bonus damage to marked enemies for every marked enemy killed.

* Bandolier:
    * Temporary skill charges granted: 1 ->
        * Uncommon: 3 (+3 per stack)
        * Rare: 6 (+6 per stack)
        * Epic: 10 (+10 per stack)
        * Legendary: 15 (+15 per stack)
    * Temporary skill charge chance -> 8% (previously stacked with qualities)

* Ghor's Tome:
    * Fixed quality buffs staying after losing the item.
    * Decreased damage gained per 25 gold (maximum damage increase unchanged):
        * Uncommon: 2% -> 1%
        * Rare: 3% -> 1.5%
        * Epic: 3.5% -> 2%
        * Legendary: 4% -> 3%

* Cautious Slug:
    * Decreased health gained per kill:
        * Uncommon: Unchanged
        * Rare: 6 (+6 per stack) -> 4 (+4 per stack)
        * Epic: 12 (+12 per stack) -> 8 (+8 per stack)
        * Legendary: 20 (+20 per stack) -> 12 (+12 per stack)
    * Decreased maximum health increase:
        * Uncommon: Unchanged
        * Rare: 300 (+300 per stack) -> 200 (+200 per stack)
        * Epic: 600 (+600 per stack) -> 400 (+400 per stack)
        * Legendary: 1000 (+1000 per stack) -> 600 (+600 per stack)

* Rose Buckler:
    * Slightly reduced straight-line strictness.
    * Bonus armor now lasts for 0.5 seconds instead of immediately ending after you stop sprinting along the line.
    * Required duration for straight-line armor:
        * Uncommon: 1s (Unchanged)
        * Rare: 1s -> 0.9s
        * Epic: 1s -> 0.8s
        * Legendary: 1s -> 0.7s

* Warped Echo:
    * Clarified that this item applies to 'on damage taken' effects, not 'on-hit enemy' effects.
    * Fixed incorrect stacking in item descriptions.
    * Increased 'on damage taken' repeat chance:
        * Uncommon: 10% (+10% per stack) -> 30% (+30% per stack)
        * Rare: 30% (+30% per stack) -> 60% (+60% per stack)
        * Epic: 50% (+50% per stack) -> 1 (+1 per stack) guaranteed
        * Legendary: 1 (+1 per stack) guaranteed -> 1 (+1 per stack) guaranteed and 50% (+50% per stack) for an additional repeat

* Ignition Tank:
    * Reduced burn damage:
        * Uncommon: 20% (+20% per stack) -> 10% (+10% per stack)
        * Rare: 50% (+50% per stack) -> 20% (+20% per stack)
        * Epic: 80% (+80% per stack) -> 30% (+30% per stack)
        * Legendary: 100% (+100% per stack) -> 50% (+50% per stack)

* Bustling Fungus:
    * Allied attacks no longer collide with the outside of the shield.
    * Increased shield spawn delay: 0.25s -> 0.6s

* Luminous Shot:
    * Fixed an error causing lightning storms to deal ~5x more damage than they were supposed to (lol).
    * Increased (after effective 5x reduction above) storm TOTAL damage:
        * Uncommon: 1000% (+400% per stack) -> 1200% (+800% per stack)
        * Rare: 1200% (+600% per stack) -> 1600% (+1200% per stack)
        * Epic: 1400% (+800% per stack) -> 1800% (+1400% per stack)
        * Legendary: 1600% (+1000% per stack) -> 2000% (+1600% per stack)
        
* Delicate Watch
    * Increased damage:
        * Uncommon: 5% (+5% per stack) -> 7% (+7% per stack)
        * Rare and up: Unchanged
    * Maximum hits per stage:
        * Uncommon: 10 -> 12
        * Rare and up: Unchanged

* Paul's Goat Hoof:
    * Decreased movement speed gain:
        * Uncommon: 28% (+28% per stack) -> 25% (+25% per stack)
        * Rare: 49% (+49% per stack) -> 40% (+40% per stack)
        * Epic: 70% (+70% per stack) -> 60% (+60% per stack)
        * Legendary: 98% (+98% per stack) -> 75% (+75% per stack)

* Energy Drink:
    * Decreased sprint speed gain:
        * Uncommon: 40% (+40% per stack) (Unchanged)
        * Rare: 70% (+70% per stack) -> 50% (+50% per stack)
        * Epic: 100% (+100% per stack) -> 65% (+65% per stack)
        * Legendary: 150% (+150% per stack) -> 80% (+80% per stack)

* Backup Magazine:
    * Free recharge now triggers 'on skill cooldown' effects (ie. Eclipse Lite).
    * Increased recharge chance:
        * Uncommon: 10% (+10% per stack) -> 15% (+15% per stack)
        * Rare: 20% (+20% per stack) -> 25% (+25% per stack)
        * Epic: 35% (+35% per stack) -> 40% (+40% per stack)
        * Legendary: 60% (+60% per stack) (Unchanged)

* N'kuhana's Opinion:
    * Increased damage:
        * Uncommon: +20% (+20% per stack) -> +40% (+40% per stack)
        * Rare: +40% (+40% per stack) -> +80% (+80% per stack)
        * Epic: +80% (+80% per stack) -> +100% (+100% per stack)
        * Legendary: +100% (+100% per stack) -> +150% (+150% per stack)

* Lens-Maker's Glasses:
    * Decreased crit damage:
        * Uncommon: 20% (+20% per stack) -> 15% (+15% per stack)
        * Rare: 40% (+40% per stack) -> 30% (+30% per stack)
        * Epic: 100% (+100% per stack) -> 80% (+80% per stack)
        * Legendary: 150% (+150% per stack) -> 120% (+120% per stack)

* Networked Suffering:
    * Fixed player allies targetting crystals.
    * Procs can no longer happen from hitting Network Crystals.
    * Increased crystal aoe radius:
        * Uncommon: 15m (+15m per stack) -> 25m (+25m per stack)
        * Rare: 20m (+20m per stack) -> 35m (+35m per stack)
        * Epic: 25m (+25m per stack) -> 50m (+50m per stack)
        * Legendary: 35m (+35m per stack) -> 65m (+65m per stack)

* Chance Doll:
    * Now stops price scaling after successful shrine hits:
        * Uncommon: 2 successful hits
        * Rare and up: 1 successful hit

* War Horn:
    * Added skill cooldown reduction on use:
        * Uncommon: 20% (+10% per stack)
        * Rare: 40% (+30% per stack)
        * Epic: 60% (+50% per stack)
        * Legendary: 100% (+90% per stack)

* Shuriken:
    * Added chance to regain shuriken on kill:
        * Uncommon: 20%
        * Rare: 40%
        * Epic: 70%
        * Legendary: 100%
    * Buffed shuriken size:
        * Uncommon: 10% (+10% per stack) -> 30% (+30% per stack)
        * Rare: 30% (+30% per stack) -> 60% (+60% per stack)
        * Epic: 50% (+50% per stack) -> 100% (+100% per stack)
        * Legendary: 80% (+80% per stack) -> 150% (+150% per stack)

* Pocket I.C.B.M.:
    * Increased extra missile chance:
        * Uncommon: 10% (+10% per stack) -> 40% (+40% per stack)
        * Rare: 20% (+20% per stack) -> 75% (+75% per stack)
        * Epic: 30% (+30% per stack) -> +1 missile (+1 per stack)
        * Legendary: 40% (+40% per stack) -> +1 missile (+1 per stack), 50% (+50% per stack) for an additional missile

* Will-o'-the-wisp:
    * Increased explosion size:
        * Uncommon: 10% (+10% per stack) -> 15% (+15% per stack)
        * Rare and up: Unchanged
    * Fixed explosion size increase not applying to:
        * Bandit smoke bomb
        * Captain beacon impact
        * False Son charged slam
        * False Son meridian's will
        * Royal Capacitor
        * Glowing Meteorite
        * Charged Perforator
        * Mithrix Phase 4 Orb Slam
        * Mithrix Phase 1 & 3 hammer slam
        * False Son (Boss) attacks
        * Stone Golem Clap
        * Stone Golem Laser
        * Halcyonite Laser
        * Imp Overlord teleport
        * Imp Overlord ground pound
        * Parent ground slam
    * Fixed explosion size not increasing visually:
        * Artificer Ion Surge
        * Drifter Junk Cube
        * Engineer Pressure Mines
        * Captain Orbital Strike
        * Captain Diablo Strike
        * REX Seed Barrage
        * False Son Lunar Stakes

* Tougher Times:
    * Now counts blocks from any source, not just blocks by Tougher Times.
    * Added AIBlacklist tag.

* War Bonds:
    * At least one Shrine of the Mountain will appear every stage.

* Spare Drone Parts:
    * Fixed additional drones only spawning to half capacity.
    * Fixed drone spawn cooldown being wasted if a drone fails to spawn.

* Power Elixir:
    * Fixed quality effect not working properly.

* Ben's Raincoat:
    * Fixed not deflecting burn from fire elites.

* Hiker's Boots:
    * Fixed quality effect not working on Blind Pests and Vultures.

* Box of Dynamite:
    * Procs can no longer happen from hitting allied drones.
    * Fixed attacks on allied drones that do not count towards stored damage showing damage numbers and playing hit sounds.
    * Fixed certain drones blocking their own attacks when this item was present.
    * Fixed allied drones sometimes attacking other drones after a player damaged it.

* Orphaned Core:
    * Buddy impact damage is now attributed to the player that launched it. (Can proc your items)
    * Buddy is now has a slight aim to return to you after impacting something and bouncing off of it.

* Lessened restrictions on Junk Drones dropping quality items:
    * Tier 0: No quality (Unchanged)
    * Tier 1: Uncommon -> Uncommon to Rare
    * Tier 2: Uncommon to Rare -> Uncommon to Legendary

* Fixed Quality Regenerating Scrap giving lower qualities than intended when used in Cauldrons.

* Quality items are now automatically sorted and grouped together in your inventory to help item visibility, can be toggled in config.

* Fixed Quality 3D Printers sometimes printing the incorrect quality items if used repeatedly as soon as they finish.

* Fixed Artifact of Command not showing all options for quality items.

* Fixed some qualities not being sorted in logbook.

* Fixed quality item descriptions not showing properly in languages other than English.

* Fixed incompatibility with RiskyTweaks if `Frost Relic - Remove Bubble` config was enabled.

<details>
<summary>0.5.3</summary>

* Open Beta Release.
</details>
