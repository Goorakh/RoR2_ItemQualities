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
    * Decreased damage gained per 25 gold (maximum damage unchanged):
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

* Chance Doll:
    * Now stops price scaling after succesful shrine hits:
        * Uncommon: 2 succesful hit
        * Rare and up: 1 succesful hit

* Spare Drone Parts:
    * Fixed additional drones only spawning to half capacity.
    * Fixed drone spawn cooldown being wasted if a drone fails to spawn.

* Power Elixir:
    * Fixed quality effect not working properly.

* Networked Suffering:
    * Fixed crystals taking aggro from player allies.

* Ben's Raincoat:
    * Fixed not deflecting burn from fire elites.

* Fixed some qualities not being sorted in logbook.

* Fixed incompatibility with RiskyTweaks if `Frost Relic - Remove Bubble` config was enabled.

<details>
<summary>0.5.3</summary>

* Open Beta Release.
</details>