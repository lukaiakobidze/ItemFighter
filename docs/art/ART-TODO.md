# Art to make now

Make only what this list asks, following the rules in `AGENTS.md`. Tick each box (`- [x]`) when its file is saved. Luka clears this list when it's done.

## 1. Sharp-cornered frames (Luka 2026-10-17)

Luka wants every frame with sharp square corners, no rounding. Redo these UI pieces exactly as `AGENTS.md` ("UI pieces") describes, changing only the corners to sharp 90° corners:

- [x] `UI\square_frame.png` (256×256). The current one has rounded corners: redo it with sharp corners. This is the most important one; it is drawn on every square in the game.
- [x] `UI\icon_frame_bg.png` (128×128): full dark square, sharp corners, no transparency.
- [x] `UI\icon_frame_border.png` (128×128): sharp corners.
- [x] `UI\panel.png` (96×96)
- [x] `UI\card.png` (96×96)
- [x] `UI\tooltip.png` (96×96)
- [x] `UI\button.png`, `UI\button_hover.png`, `UI\button_selected.png`, `UI\button_disabled.png` (96×96 each)

## 2. Black Knight, the mission 1 boss (Luka 2026-10-17)

Same style as the other enemies, but grander: it is the first boss of the game. The ability and passive names are placeholders, so keep the pictures about what they do.

- [x] `Enemies\black_knight.png` (512×512 portrait): towering knight in jagged black plate armour, a glowing red eye slit in the helmet, tattered dark cape, a big black sword. Dark stormy background with a red hard-edged burst behind him.
- [x] `EnemyAbilities\crushing_blow.png` (256×256): Crushing Blow, a heavy hit for 15 that also makes you Vulnerable. A black armoured gauntlet or sword pommel smashing down onto a shield, the shield cracking with bright yellow (`#FADC50`) cracks. Red-dark backdrop.
- [x] `EnemyAbilities\dark_slash.png` (256×256): Dark Slash, a quick hit for 9. A black sword with a wide dark purple slash arc. Red-dark backdrop.
- [x] `Passives\black_bulwark.png` (256×256): Black Bulwark, every 5 times it is hit it gains shield (10% of its max HP). A black tower shield with a red emblem, small blue (`#78AAE6`) shield sparks around it. Blue-dark backdrop.

## 3. New status: Vulnerable (Luka 2026-10-17)

- [x] `Statuses\vulnerable.png` (128×128, transparent background like the other statuses): Vulnerable means hits do 50% more damage. A cracked shield or a target with a crack through it, in yellow `#FADC50` with the bold dark outline. Match `power.png` and `pain.png` in size and weight.

## 4. Locomotive Engineer and Heat (Luka 2026-10-19)

A new character built around Heat: his items heat him up, and when Heat reaches 100 he overheats, which hurts the enemy instead of him. Heat colour is a fiery orange `#FF6E1E`.

- [x] `Characters\locomotive_engineer.png` (512×512 portrait): a sturdy steam-train engineer in soot-stained blue overalls, a striped engineer's cap and goggles pushed up, a red neckerchief, sleeves rolled up, holding a coal shovel. Behind him the glowing open firebox of a locomotive, orange `#FF6E1E` hard-edged glow and puffs of steam.
- [x] `Passives\locomotive_engineer.png` (256×256): the Engineer's passive, overheating damages the enemy instead of him and stalls his items half as long. A locomotive pressure gauge with the needle in the red, a burst of steam and orange `#FF6E1E` sparks shooting outward. Orange-dark backdrop.
- [x] `Items\shovel_of_coal.png` (256×256): Shovel of Coal, adds Heat. A coal shovel heaped with black coal lumps, a few of them glowing orange `#FF6E1E`. Orange-dark backdrop.
- [x] `Items\hot_iron.png` (256×256): Hot Iron, a hit that gets stronger the more Heat you have. A blacksmith's iron bar or brand glowing red-orange at the tip, heat shimmer lines around it. Red-dark backdrop.
- [x] `Statuses\overheat.png` (128×128, transparent background like the other statuses): Overheated, every item is stalled for a moment. A steaming, cracked pressure valve or a thermometer bursting at the top, in orange `#FF6E1E` with the bold dark outline. Match `power.png` and `pain.png` in size and weight.

## 5. Bolder UI: textured bars and colourful panels (Luka 2026-10-20)

Luka wants the whole UI in the same art style as the items and characters: bright colours and harsh, hard-edged lines, nothing greyed out. Follow the updated "UI pieces" section in `AGENTS.md` (it replaces the old "calmer and darker" rule). The game draws tick lines and numbers on the bars itself, so put none in the pictures.

Health bar (the game stretches these along each side's long HP bar):

- [x] `UI\hp_bar_back.png` (256×64): the empty bar trough.
- [x] `UI\hp_bar_fill.png` (1024×64): the red health fill.
- [x] `UI\shield_bar_fill.png` (1024×64): the blue shield layer drawn over the health.
- [x] `UI\hp_bar_frame.png` (256×64): the frame around the bar, see-through in the middle.

Heat bar (tall and thin, right of the items):

- [x] `UI\heat_bar_back.png` (64×256): the empty heat tube.
- [x] `UI\heat_bar_fill.png` (64×512): the heat fill, green at the bottom through yellow to red at the top.
- [x] `UI\heat_bar_frame.png` (64×256): the frame around the tube, see-through in the middle.

Panels and buttons, redone with colour (same file names, so they replace the current ones):

- [x] `UI\panel_enemy.png` (96×96, new): the enemy's side of a fight, with red trim.
- [x] `UI\panel_player.png` (96×96, new): your side of a fight, with gold trim.
- [x] `UI\panel.png`, `UI\card.png`, `UI\tooltip.png` (96×96 each): redo with colour.
- [x] `UI\button.png`, `UI\button_hover.png`, `UI\button_selected.png`, `UI\button_disabled.png` (96×96 each): redo with colour.
- [x] `UI\background.png` (1920×1080): redo with colour.

## 6. More Heat: three items, three passives, a status (Luka 2026-10-26)

All Rare. Heat is fiery orange `#FF6E1E`. The passive names are placeholders, so keep their pictures about what they do.

- [ ] `Items\stabilizer.png` (256×256): Stabilizer, click to stop gaining Heat (click again to allow it). A brass steam valve wheel with a locked clamp or a pressure regulator, cool blue (`#78AAE6`) frost edging the brass. Blue-dark backdrop.
- [ ] `Items\hot_plate.png` (256×256): Hot Plate, gives shield and Heat. A thick square iron plate, its centre glowing orange `#FF6E1E` with heat lines, shaped like a small shield. Orange-dark backdrop.
- [ ] `Items\flaming_coal.png` (256×256): Flaming Coal, spends some of your Heat to Burn the enemy. A single big lump of coal cracked open with bright orange `#FF8C28` flames bursting out of it. Red-dark backdrop.
- [ ] `Passives\stoker.png` (256×256): Stoker, every 5 times you gain Heat it Burns the enemy. A coal shovel flinging flaming coals forward. Orange-dark backdrop.
- [ ] `Passives\firebox.png` (256×256): Firebox, every Heat gain is 1 bigger. An open locomotive firebox door with roaring flames inside. Orange-dark backdrop.
- [ ] `Passives\big_boiler.png` (256×256): Big Boiler, you can hold 10 more Heat before overheating. A fat riveted steam boiler with a pressure gauge, a puff of steam on top. Orange-dark backdrop.
- [ ] `Statuses\no_heat_gain.png` (128×128, transparent background like the other statuses): No Heat Gain, your Heat is locked. A flame inside a padlock, or a flame with a bold bar across it, in pale blue `#AAE6FF` with the bold dark outline. Match `power.png` and `pain.png` in size and weight.
