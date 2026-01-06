## 0.6.0

<details>
<summary>Crowbar rework</summary>

* Old: Decrease damage increase threshold.
* New: Builds up damage on stunned enemies, released when enemy regains control.
</details>

<details>
<summary>Armor Piercing Rounds rework</summary>

* Old: Mark enemies within the teleporter radius after boss. Deal bonus damage to marked enemies.
* New: Marks an enemy every 60 seconds for 10 seconds, deal bonus damage to marked enemies for every marked enemy killed.
</details>

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

* Cautios Slug:
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
    * Free recharge now triggers 'on skill cooldown' effects (Eclipse Lite).
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
    * Increased crystal aoe radius:
        * Uncommon: 15m (+15m per stack) -> 25m (+25m per stack)
        * Rare: 20m (+20m per stack) -> 35m (+35m per stack)
        * Epic: 25m (+25m per stack) -> 50m (+50m per stack)
        * Legendary: 35m (+35m per stack) -> 65m (+65m per stack)

* Chance Doll:
    * Now stops price scaling after succesful shrine hits:
        * Uncommon: 2 succesful hit
        * Rare and up: 1 succesful hit

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

* War Bonds:
    * At least one Shrine of the Mountain will appear every stage.

* Spare Drone Parts:
    * Fixed additional drones only spawning to half capacity.
    * Fixed drone spawn cooldown being wasted if a drone fails to spawn.

* Power Elixir:
    * Fixed quality effect not working properly.

* Ben's Raincoat:
    * Fixed not deflecting burn from fire elites.

* Fixed some qualities not being sorted in logbook.

* Fixed incompatibility with RiskyTweaks if `Frost Relic - Remove Bubble` config was enabled.

<details>
<summary>0.5.3</summary>

* Open Beta Release.
</details>