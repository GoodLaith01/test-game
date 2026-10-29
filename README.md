# Hollowmark (test-game)

A third-person 3D dungeon adventure for Unity, set in the 1700s. You defend your village from six bosses
whose creatures keep attacking it. You fight through dungeons, take your enemies' weapons and armor, and try
to stay fed, watered, warm and sane.

This is a **playable vertical slice** built with code-generated art (human-shaped bodies, primitives for props). All gameplay content lives
in JSON data, so the six dungeons, five enemy types, bosses, weapons, armor and items can be tuned or extended
without touching code.

## How to run

1. Install **Unity 2022.3 LTS or Unity 6** with Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and select this folder, then open it.
   Unity creates `ProjectSettings/`, `Library/` and the `.meta` files on first open. Commit the `.meta` files after that.
3. Press **Play** in any scene, including the default empty one. A bootstrap script builds the village, the
   player, the camera and the UI at runtime. No scene setup or prefabs are needed.

The project uses the Built-in render pipeline. If you open it with a URP template, the materials fall back to
URP Lit automatically. Input uses the legacy Input Manager. If your project is set to *Input System (New)* only,
install the Input System package (the code supports it) or set **Project Settings > Player > Active Input
Handling** to *Both*.

## Controls

| Key | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Shift | Sprint (uses stamina) |
| Space | Jump |
| C or Ctrl | Dodge roll (brief invulnerability) |
| Left click | Attack, or fire a firearm |
| Right click | Block with a melee weapon, or aim a firearm |
| R | Reload a firearm (uses Powder & Shot) |
| E | Interact (hold E on the weights) |
| Tab or I | Inventory: equip weapons, wear armor, use items |
| Q / F / H | Eat the best food / drink / use a bandage |
| Esc or F1 | Pause and help |

## What is in the slice

**Village hub (Hollowmark).** Your house with a bed (sleep until morning). A training yard with dummies (hit them
to raise Swordsmanship) and light and heavy weights (hold E to raise Strength and Endurance). A market with a
weaponsmith, an armorer (who also repairs armor) and a provisioner selling food, water, medicine and ammunition.
A well for free water. Villagers, and six dungeon gates. A gate unlocks when the boss of the previous dungeon is dead.

**Dungeons.** Six dungeons, each with its own look, temperature and changing weather:

| Dungeon | Tier | Weather | Enemies | Boss |
| --- | --- | --- | --- | --- |
| Mossy Catacombs | 1 | damp, rain seep | Gnarlings, Hollowed | Old Mother Gnarl |
| Frostfang Mines | 2 | frost, blizzard (down to -20°C) | Hollowed, Bog Crawlers, Gnarlings | The Frost Warden |
| Sunken Marsh Abbey | 2 | humid mire, downpour with lightning | Bog Crawlers, Hollowed | The Drowned Abbess |
| Ember Foundry | 3 | furnace heat (44°C), ash fall | Ashen Imps, Rotfang Brutes, Gnarlings | The Forge Tyrant |
| Howling Cliffs Fortress | 4 | gale, thunderstorm | Brutes, Hollowed, Imps | Colonel Grieve |
| The Hollow Throne | 5 | red dread, silent void (insanity x2-3) | all five | The Hollow King |

The layout is generated on each visit: a winding chain of rooms and corridors, supply crates, a tent camp every few
rooms (always one just before the boss), and a boss arena. The way home is always open from the first room.

**Enemies (5 types).** Each type has its own body plan, and every individual is randomly varied in colour, head and
arm size, eye count and horns, so no two look alike.
- *Gnarling*: short and fast, hides in shadow (only its eyes glow) and ambushes for extra damage.
- *Hollowed*: humans who gave up or chose to join the creatures. Slow and hard-hitting. During a village raid, a
  villager can despair and turn into one.
- *Bog Crawler*: six-legged, burrows under the floor and erupts beneath you.
- *Ashen Imp*: small and very fast, keeps its distance and throws embers.
- *Rotfang Brute*: short, very wide, tireless, swings a bone cleaver.

Enemies have more stamina than a human and dart back after striking.

**Bosses.** All six are 2.8 to 4.2 m tall. They have heavy swings, a telegraphed ground slam (red circle),
summon minions at 66% and 33% health, and enrage when low. Each boss drops gold, a guaranteed weapon and armor piece,
lowers the village threat, and opens a portal home.

**Loot.** Enemies drop gold, random weapons, pieces of the armor they wore (including monster armor such as the
Gnarling Bone Mask or the Forsaken Plate), and supplies.

**Weapons of the 1700s.** Hunting hanger, Highland dirk, tomahawk, smallsword, naval cutlass, cavalry sabre,
basket-hilted broadsword, boarding axe, spontoon, halberd, claymore, executioner's sword, flintlock pistol,
Land Pattern "Brown Bess" musket, blunderbuss (spread shot) and Pennsylvania long rifle, plus creature weapons.
Heavy and Great weapons need Strength, which you train with the weights.

**Armor and the underwear mode.** Head, chest, hands and legs. Each piece has defense, durability, warmth and
weight. Hits wear armor down, and broken pieces fall off. When nothing is left you fight in your underwear: defense 0,
you take 60% more damage, and the HUD lets you know. The armorer repairs worn pieces.

**Survival.**
- *Hunger and thirst* drain over time. At zero you lose health. Low meters slow stamina regeneration.
- *Temperature*: each weather has an air temperature. Armor insulates you. Without armor you get cold (slower, worse
  reloads) or hot (thirstier), and extremes hurt. Campfires and hot tea warm you.
- *Stamina* is used to sprint, roll, swing and block, and it regenerates more slowly when you are hungry, cold or heavily armored.
- *Insanity*: after 16 hours awake, insanity rises (faster in some weathers). It makes you miss swings, makes your aim
  wobble, sways the camera, tints the screen, and brings whispers and phantom creatures. At 100 your mind breaks and you
  start dying. Sleep in your bed or a dungeon tent to reset it. You can't sleep in a tent with monsters nearby.

**Village threat and raids.** Threat rises every in-game day while bosses live. At 100, the next time you are in the
village it is raided. Killing a boss lowers threat a lot.

**Death.** You wake in the village and lose half your gold. You keep your gear.

## Adding content (data-driven)

Everything is in `Assets/Resources/Data/*.json`. Ids link entries together, and typos are reported as console warnings on load.

- **New weapon**: add to `weapons.json`. `kind` is `Melee` or `Firearm`, `weightClass` is `Light`/`Medium`/`Heavy`/`Great`.
  Set `requiredStrength`, `tier` (where it drops and is sold), and `sold: false` for drop-only weapons.
- **New armor**: add to `armor.json` with `slot` (`Head`/`Chest`/`Hands`/`Legs`), `defense`, `durability`, `insulation`.
- **New enemy type**: add to `enemies.json`. Pick a `bodyShape` (`Goblin`, `Humanoid`, `Crawler`, `Imp`, `Brute`), `stealth`
  (`None`, `Shadow`, `Burrow`), `ranged`, and drop lists. Then reference its id in a dungeon's `enemies`.
- **New dungeon**: add to `dungeons.json` with its colours, `enemies`, `bossId`, `weathers`, `supplyItems` and
  `unlockAfter`. A gate appears in the village automatically.
- **New weather**: add to `weather.json` (temperature, fog, light, `particles`: `Rain`/`Snow`/`Ash`/`Embers`/`Spores`/`Dust`, wind, lightning).
- **Balance**: `balance.json` holds time speed, hunger and thirst rates, the insanity threshold, the underwear
  damage multiplier, threat speed and starting gear.

## Art: bodies and faces

Humans, villagers and the human-shaped creatures use a **realistic sculpted body**: the MakeHuman base mesh
(CC0, free for any use, see `Assets/Resources/Humans/LICENSE.txt`) with its professional skin weights. It comes in
several shapes (`HumanMesh.Slim`, `Average`, `Muscular`, `Giant`, `Female`, `FemaleSlim`), is skinned at runtime to
the game's skeleton (hips, knees, ankles, spine, shoulders, elbows, wrists, neck), and is animated in code: walking
bends the knees, attacks bend the elbow, idle breathes and looks around. The hero is muscular, villagers are slim or
average, the Hollowed are gaunt, Brutes and bosses use the giant build, and Gnarlings and Imps get bigger heads.
Clothes and armor are fitted by copying the skin over body regions and pushing it outwards (`CharacterRig.Cover`):
shirts, waistcoats, breeches, stockings, gowns, frock coats, tricorne hats, helmets and boots. Hair grows from the
scalp. Creatures add horns, tusks, pointed ears, wings and glowing eyes.

The body data lives in `Assets/Resources/Humans/*.bytes`; `Tools/makehuman_export.py` regenerates it or adds shapes.
If that folder is missing, the game falls back to simpler code-generated bodies (`Visuals/BodyMesh.cs`).

The sculpted faces are realistic in shape but flat-coloured (no skin textures yet). **Photo-realistic human faces cannot be produced in code. They need real 3D art assets.** Typical options:

- **Reallusion Character Creator 4 + Headshot** (photo to 3D head) exported to FBX with the Unity Auto Setup plugin.
- **Photogrammetry or scanned heads** (for example from a 3D scan service or a marketplace), retopologised and rigged.
- **Unity Asset Store** period character packs (18th-century soldiers, peasants, pirates) for villagers and the hero.
- For high-end skin, the HDRP skin and eye shaders (Unity's *Enemies* and *Digital Human* samples show the setup).

The code is ready for this. Every entry in the JSON files has an optional `"model"` field. Put a prefab under
`Assets/Resources/` (for example `Assets/Resources/Models/Hero.prefab`) and set `"model": "Models/Hero"`, and the game
uses it instead of the generated body (see `CharacterBuilder.TryModel`). If the prefab has an Animator with a `Speed`
float, `Attack` and `Hurt` triggers and a `Dead` bool (for example a Mixamo character), the game drives them. The five creature types should each get their
own sculpted model with several head and body variants to keep the "no two alike" feel.

## Code map

```
Assets/Scripts
  Core/      GameBootstrap (auto-start), GameManager (clock, threat, world switching), GameInput, Damage
  Data/      GameData (JSON schema), GameDatabase (loading, validation, weighted loot)
  Player/    Hero, PlayerController, ThirdPersonCamera, PlayerCombat, PlayerStats (survival), Inventory,
             PlayerVisual (armor, underwear), InsanityEffects (whispers, phantoms)
  Enemies/   EnemyAI (stealth, ambush, ranged), BossAI (slam, summons, enrage), Projectile
  World/     VillageBuilder, DungeonBuilder, WeatherSystem, Interactables (bed, tent, campfire, shop, dummy,
             weights, gates, portals, crates), Loot, VillageRaid (villagers, raids)
  UI/        Hud (IMGUI: bars, shop, inventory, death, effects)
  Visuals/   Prim (primitives/materials), HumanMesh (realistic skinned bodies), BodyMesh (fallback bodies), CharacterRig (rig, faces, clothes, animation), Effects
```

## Not in this slice yet

Saving and loading, audio, real animation (the rig uses simple procedural swings), NavMesh pathfinding (enemies
steer directly toward you), line-of-sight for enemy vision, and a controller/gamepad layout.
