# Art to make now

Make only what this list asks, following the rules in `AGENTS.md`. Tick each box (`- [x]`) when its file is saved. Luka clears this list when it's done.

## 1. Sharp-cornered frames (Luka 2026-10-17)

Luka wants every frame with sharp square corners, no rounding. Redo these UI pieces exactly as `AGENTS.md` ("UI pieces") describes, changing only the corners to sharp 90° corners:

- [ ] `UI\square_frame.png` (256×256). The current one has rounded corners: redo it with sharp corners. This is the most important one; it is drawn on every square in the game.
- [ ] `UI\icon_frame_bg.png` (128×128): full dark square, sharp corners, no transparency.
- [ ] `UI\icon_frame_border.png` (128×128): sharp corners.
- [ ] `UI\panel.png` (96×96)
- [ ] `UI\card.png` (96×96)
- [ ] `UI\tooltip.png` (96×96)
- [ ] `UI\button.png`, `UI\button_hover.png`, `UI\button_selected.png`, `UI\button_disabled.png` (96×96 each)

## 2. Black Knight, the mission 1 boss (Luka 2026-10-17)

Same style as the other enemies, but grander: it is the first boss of the game. The ability and passive names are placeholders, so keep the pictures about what they do.

- [ ] `Enemies\black_knight.png` (512×512 portrait): towering knight in jagged black plate armour, a glowing red eye slit in the helmet, tattered dark cape, a big black sword. Dark stormy background with a red hard-edged burst behind him.
- [ ] `EnemyAbilities\crushing_blow.png` (256×256): Crushing Blow, a heavy hit for 15 that also makes you Vulnerable. A black armoured gauntlet or sword pommel smashing down onto a shield, the shield cracking with bright yellow (`#FADC50`) cracks. Red-dark backdrop.
- [ ] `EnemyAbilities\dark_slash.png` (256×256): Dark Slash, a quick hit for 9. A black sword with a wide dark purple slash arc. Red-dark backdrop.
- [ ] `Passives\black_bulwark.png` (256×256): Black Bulwark, every 5 times it is hit it gains shield (10% of its max HP). A black tower shield with a red emblem, small blue (`#78AAE6`) shield sparks around it. Blue-dark backdrop.

## 3. New status: Vulnerable (Luka 2026-10-17)

- [ ] `Statuses\vulnerable.png` (128×128, transparent background like the other statuses): Vulnerable means hits do 50% more damage. A cracked shield or a target with a crack through it, in yellow `#FADC50` with the bold dark outline. Match `power.png` and `pain.png` in size and weight.
