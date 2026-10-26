# ItemFighter art: rules for Codex

You make the PNG images for ItemFighter, a game by Luka. **What to make right now is in `ART-TODO.md`** (in this folder). Read it first and make only what it lists. This file is the permanent rulebook: style, sizes, file names and a catalogue of every picture in the game, so a remake matches the rest.

## How to work

- **Work in `D:\CODING\GameDev\ItemFighterArt`.** Write every image under the same folders the game uses: `src\ItemFighter.Game\Content\Art\<Kind>\<id>.png`, for example `src\ItemFighter.Game\Content\Art\Items\sword.png`. Luka copies that `src` folder over the game repo, so paths and names must match exactly.
- One image per file, at the exact size for its kind (table below), as a real PNG with an alpha channel (no JPEG renamed to .png). File names are the ids: lower case, underscores, `.png`.
- Save each image as soon as it is finished, so progress can be checked while you work.
- Don't touch any file that `ART-TODO.md` doesn't list, and never edit code. The game loads the PNGs by itself (F5 in the Fight Lab reloads them).
- When you finish an entry in `ART-TODO.md`, tick its box (`- [x]`). Don't delete entries; Luka clears the list.

## The game

ItemFighter is a roguelike autobattler, like The Bazaar. You fight one enemy at a time. Your items sit in a row of 6 slots and fire on their own cooldowns. Enemies have abilities that fire the same way, plus passives that trigger on their own. Each mission ends with a boss. The tone is light fantasy, a bit humorous (a martial arts squirrel, a furious boxer).

## Style (every image)

**Match the existing art exactly.** Before you start, open these and compare every new image against them side by side:

- `Statuses\`: `burn.png`, `freeze.png`, `power.png`
- `Passives\`: `fury.png`, `silver_knight.png`, `explosive.png`
- `Items\`: `sword.png`, `ice_shield.png`
- `Enemies\`: `furious_boxer.png`, `yeti.png`

What that style is:

- **Bold dark outline** around the whole subject: thick (about 6 to 8px at 256×256), dark navy (around `#141826`), even all the way round. Thinner dark lines inside separate the big parts.
- **Solid colour areas with hard edges.** Each part is a flat base colour plus one darker shadow shape and one lighter highlight shape, sometimes a small bright glint. Crisp edges between tones, like cel shading or a vector icon.
- **Simple, chunky shapes**, slightly exaggerated and cartoony: big readable forms, few small details.
- **Bright, saturated colours**, light from the top left: highlights upper left, shadows lower right.
- **Centred subject** filling about 75 to 90% of the canvas, slightly tilted where it helps.

Don't use: soft airbrushed gradients, painterly texture, scratches, grain, rust, noise, fine realistic detail, engravings, photo-like materials, thin or broken outlines, glows that smear outside the outline, drop shadows on the canvas, or any text and numbers inside an image.

**Colour language** (use the colour of what a thing does): burn orange `#FF8C28`, poison purple `#B060E6`, chill cyan `#6EDCF0`, freeze pale blue `#AAE6FF`, pain pink `#E84682`, nail steel grey `#AAB4C3`, power red-orange `#F05028`, vulnerable yellow `#FADC50`, heat fiery orange `#FF6E1E`, shield blue `#78AAE6`, heal green `#5AD26E`, damage red `#FF5A50`.

## Square pictures

The game shows items, enemy abilities, passives, portraits and shops like League of Legends item icons: **a plain square picture, edge to edge**, and the game draws its own frame on top (the frame colour shows rarity or side).

- **Fill the whole square.** No transparent pixels, sharp square corners, no border or frame of your own.
- **Background:** a simple flat backdrop in the same cel style: two or three solid tones plus one bold hard-edged shape behind the subject (a burst, a swirl, speed streaks) in the colour of what the thing does. Darker and calmer than the subject so the subject reads first.
- **Readable small:** passives are shown as small as 34×34, so one clear subject with a strong silhouette.
- **Statuses are the exception:** transparent background, one simple symbol in the status colour (the game puts them on a dark square with a tinted frame).
- **Portraits** (enemies, characters): the figure facing slightly toward the viewer, head and upper body, menacing but charming, on its own full-square background. Bosses look bigger and grander than normal enemies: armour, a cape, a strong pose, a dramatic background.
- **Passives** read as an emblem or symbol, like a buff icon, more than a full object.

## Sizes and folders

| Kind | Folder (under `src\ItemFighter.Game\Content\Art\`) | Size | Background |
|---|---|---|---|
| Items | `Items\` | 256×256 | full square |
| Enemy abilities | `EnemyAbilities\` | 256×256 | full square |
| Passives | `Passives\` | 256×256 | full square |
| Statuses | `Statuses\` | 128×128 | transparent |
| Enemies and bosses (portraits) | `Enemies\` | 512×512 | full square |
| Characters (portraits) | `Characters\` | 512×512 | full square |
| Map encounters (shops) | `Encounters\` | 512×512 | full square |
| UI pieces | `UI\` | see "UI pieces" | see there |

On screen (the game draws its frame on top): items 112×112 in a fight and 96×96 on the hotbar, shop squares 120×120, enemy abilities 112×112, passives 34×34, statuses 60×60, portraits 156×156 in a fight and 200×200 on the map.

## UI pieces

**The same style as the items and characters** (Luka 2026-10-20): bold dark navy outline, bright saturated flat colours, one hard-edged shadow and one hard-edged highlight, harsh crisp lines, no texture or soft gradients. Nothing greyed out or washed out: backgrounds of panels are deep and rich (indigo, navy, wine), trims and highlights are bright. Items and enemies still stand out because they sit on dark fills inside bright frames. **Every frame, panel, card, tooltip, button and bar has sharp square corners, never rounded** (Luka 2026-10-17).

### Square frame

`square_frame.png`, 256×256: the frame the game draws on top of *every* square (items, abilities, passives, statuses, portraits, shop and drop squares), tinted per square: rarity colour for items and passives, red for enemies, gold for you, status colour for statuses.

- A square ring about 10 to 12px thick at 256×256, flush with the image edges, **sharp 90° corners**. Everything inside the ring fully transparent.
- **White and light greys only**, so tinting works: a light grey outer edge, white body and a slightly darker grey inner edge give it depth after tinting.
- A soft dark inner shadow just inside the ring (black at partial opacity, fading out within about 8px) separates the picture from the frame.
- The ring itself fully opaque: the game draws cooldown shades under it.
- Must stay crisp shrunk to 34×34: clean edges, no fine detail.

### Icon frame (fallback layers)

| File | What it is | How it should look |
|---|---|---|
| `icon_frame_bg.png` | Background behind every square, 128×128 | Dark square filling the whole image, near-black blue-grey (`#0E0F16` to `#1A1C28`): one flat tone with a slightly lighter hard-edged band along the top. **Sharp corners**, no transparency |
| `icon_frame_border.png` | Old thin border, used only when `square_frame.png` is missing, 128×128 | Thin **white** line (about 4px) around the edge, sharp corners, everything else transparent |

### Stretchable frames

Stretched to many sizes: the game keeps the **outer quarter of each side** as drawn and stretches only the middle. Put all detail (corner shapes, rivets, notches) in the corners; keep edges and centre plain and even. All 96×96, **sharp square corners**, no transparency.

| File | Used for | How it should look |
|---|---|---|
| `panel.png` | Big boxes: combat log, side panels, any panel without its own picture | Deep indigo fill (`#24204A`), thick dark navy outline, a bright violet-blue trim band (`#5A6CF0`) just inside the outline with a hard-edged lighter band along its top, chunky corner plates |
| `panel_enemy.png` | The enemy's side of a fight | Like `panel.png` with a deep wine fill (`#3A1626`) and a bright red trim (`#E8463C`), bolt-like corner plates |
| `panel_player.png` | Your side of a fight | Like `panel.png` with a deep indigo fill (`#1E2450`) and a bright gold trim (`#FFC440`), bolt-like corner plates |
| `card.png` | Cards and boxes | Like the panel, a bit lighter (`#2E2A5C`), with small bright corner details |
| `tooltip.png` | The mouse-over info box | Very dark indigo fill (`#14122C`), thick dark outline with a thin bright violet edge. The game adds a coloured border on top |
| `button.png` | Buttons | Raised slab: saturated blue top face (`#3A5CD0`) with a hard-edged lighter band on top and a dark navy bottom lip |
| `button_hover.png` | Button under the mouse | Same shape, brighter face (`#5A80F0`) and a white-blue edge |
| `button_selected.png` | Chosen option | Same shape, bright gold face (`#F0B030`) with a darker orange lip |
| `button_disabled.png` | Button you can't press | Same shape, muted slate-blue (`#3A3E5A`), lower contrast but still the same bold outline |

### Bars

The game draws tick lines and numbers on top, so the pictures have **no lines, marks or numbers**. Sharp square corners. Fills are cut, not squashed: the game shows only the filled part of the picture, so keep a fill even along its length (for the HP fill) or put the colours exactly where they should be on the bar (for the heat fill).

| File | Size | Used for | How it should look |
|---|---|---|---|
| `hp_bar_back.png` | 256×64 | Empty part of every HP bar (stretched like a panel: keep the outer quarter plain) | Dark wine trough (`#3C1A22`), a hard-edged darker band along the top as an inner shadow. Opaque |
| `hp_bar_fill.png` | 1024×64 | The health in the bar | Bright red (`#E03A3A`): flat base, a hard-edged lighter band along the top third (`#FF6A5A`), a hard-edged darker band along the bottom (`#A82030`). The same all the way along, no outline, opaque |
| `shield_bar_fill.png` | 1024×64 | Shield, drawn over the health | Like the HP fill in shield blue (`#78AAE6`, light band `#B4D8FF`, dark band `#4A78C0`). The game draws it slightly see-through |
| `hp_bar_frame.png` | 256×64 | Frame around every HP bar, drawn a little outside it | A ring about 8px thick: thick dark navy outline, a bright gold or steel band inside it with a hard-edged highlight, small square rivets in the corners. **Everything inside the ring transparent** |
| `heat_bar_back.png` | 64×256 | Empty part of the heat tube | Dark ember-brown trough (`#3C2014`), hard-edged darker band down the left as an inner shadow. Opaque |
| `heat_bar_fill.png` | 64×512 | The heat, filling from the bottom | Hard-edged colour bands, not a soft gradient: green (`#50C85A`) at the bottom, through yellow (`#FADC3C`) in the middle, orange, to red (`#E6322A`) at the top, about 6 to 8 flat bands. A lighter hard-edged stripe down the left third. Opaque |
| `heat_bar_frame.png` | 64×256 | Frame around the heat tube, drawn a little outside it | Like the HP frame, upright: thick dark navy outline, a bright copper or brass band (`#E08A3C`) with a hard-edged highlight, rivets in the corners. **Everything inside the ring transparent** |

### Small icons

Shown next to numbers, as small as 20×20: one simple bold shape each. 128×128, transparent background.

| File | Used for | Picture idea |
|---|---|---|
| `gold.png` | Gold amount in runs | Single gold coin, slightly tilted, with a bright glint |
| `heart.png` | Start of every health bar | Chunky red heart (`#CC3840`) |
| `shield.png` | Shield amount on health bars | Small blue kite shield (`#78AAE6`) |
| `clock.png` | Fight time left | Simple stopwatch or hourglass |

### Screen art

| File | Size | Used for | How it should look |
|---|---|---|---|
| `background.png` | 1920×1080 | Behind every screen | A rich dusk scene in the cel style: big flat shapes (mountains, a fortress or arena silhouette) in deep purples, indigos and wine reds with a few bright hard-edged accents (a warm sunset band, glints). Darker and simpler in the middle, where the panels sit. Not transparent |
| `logo.png` | 1024×256 | Title on the main menu | "ItemFighter" in chunky bold letters with the thick dark outline, gold (`#FFC440`) with a hard-edged orange shadow, maybe a small sword or shield worked in. Transparent background |

## Catalogue of every picture

What each existing picture shows, for remakes and to keep new art consistent. New content is added here as it comes.

### Items (`Items\`)

| File | Name | Rarity | What it does | Picture idea |
|---|---|---|---|---|
| `aggravating_potion.png` | Aggravating Potion | Rare | Doubles the poison on the enemy | Bubbling purple flask with an angry face in the fumes |
| `apple.png` | Apple | Common | Heals, speeds up another item | Shiny red apple with a small leaf |
| `axe.png` | Axe | Common | Big single hit | Heavy woodcutter's axe |
| `cinder.png` | Cinder | Common | Burn | Glowing ember coal with small flames |
| `dagger.png` | Dagger | Common | Fast small hit | Simple steel dagger |
| `fang_knife.png` | Fang Knife | Common | Poison and a hit | Knife made from a big curved fang, dripping green-purple |
| `flaming_heart.png` | Flaming Heart | Rare | Burn and heal | Anatomical-style heart wrapped in warm flames |
| `flammable_toxins.png` | Flammable Toxins | Rare | Poison and burn | Corked bottle of purple liquid with a fire wick |
| `gold_pouch.png` | Gold Pouch | Rare | Extra gold after a win | Leather coin pouch spilling gold coins |
| `hurling_hammer.png` | Hurling Hammer | Rare | Hit and puts a Nail in | Throwing hammer with nails stuck in the head |
| `ice_bow.png` | Ice Bow | Common | Chill and a hit | Bow made of blue ice with a frosted arrow |
| `ice_shield.png` | Ice Shield | Rare | Shield and chill | Round shield of thick ice with frost mist |
| `magnetized_glove.png` | Magnetized Glove | Rare | Rips out every Nail (click to use) | Armoured gauntlet with a magnet in the palm pulling nails |
| `metal_plating.png` | Metal Plating | Common | Small shield | Riveted steel plate |
| `molten_dagger.png` | Molten Dagger | Common | Burn and a hit | Dagger with a glowing orange molten blade |
| `sharpened_chestplate.png` | Sharpened Chestplate | Rare | Shield and Pain | Breastplate covered in sharp spikes |
| `shield_bash.png` | Shield Bash | Rare | Hit and shield | Heater shield with a dented, bloodied rim |
| `sledgehammer.png` | Sledgehammer | Rare | Huge slow hit | Massive two-handed sledgehammer |
| `spiked_buckler.png` | Spiked Buckler | Rare | Hits for your shield | Small round buckler with a central spike |
| `spiky_club.png` | Spiky Club | Rare | Hit and Pain | Wooden club studded with spikes |
| `sword.png` | Sword | Common | Steady hit | Classic knight's longsword |
| `shovel_of_coal.png` | Shovel of Coal | Common | Adds Heat | Coal shovel heaped with coal, some lumps glowing orange |
| `hot_iron.png` | Hot Iron | Common | Hit that grows with your Heat | Iron bar or brand glowing red-orange at the tip, heat shimmer |
| `stabilizer.png` | Stabilizer | Rare | Click to lock or unlock your Heat | Brass valve wheel or regulator with a clamp, blue frost edging |
| `hot_plate.png` | Hot Plate | Rare | Shield and Heat | Square iron plate, glowing orange centre, shield-shaped |
| `flaming_coal.png` | Flaming Coal | Rare | Spends 10% of your Heat to Burn the enemy | Coal lump cracked open with flames bursting out |
| `tasty_cleanser.png` | Tasty Cleanser | Rare | Removes your poison and burn | Fizzy green drink with a straw and a lemon slice |
| `tower_shield.png` | Tower Shield | Common | Big shield | Tall rectangular tower shield |
| `twin_blades.png` | Twin Blades | Rare | Two hits | Two short curved blades crossed |
| `venom_fang.png` | Venom Fang | Common | Poison | A single snake fang dripping venom |
| `war_banner.png` | War Banner | Rare | Hits harder for each weapon you have | Tattered red war banner on a spear |
| `weights.png` | Weights | Common | Slows an enemy ability | Iron kettlebell on a chain |
| `whetstone_blade.png` | Whetstone Blade | Rare | Hits, then gets sharper | Sword resting on a whetstone with sparks |

### Enemies and bosses (`Enemies\`)

| File | Name | Difficulty | Picture idea |
|---|---|---|---|
| `archer_skeleton.png` | Archer Skeleton | 1 | Skeleton in a ragged hood drawing a bow |
| `armoured_beast.png` | Armoured Beast | 2 | Hulking boar-like beast in heavy plate armour |
| `flaming_rock.png` | Flaming Rock | 1 | Living boulder with cracks of lava and angry eyes |
| `furious_boxer.png` | Furious Boxer | 3 | Red-faced boxer in gloves, steam coming off his head |
| `green_snake.png` | Green Snake | 1 | Coiled green snake, fangs out |
| `grizzly_bear.png` | Grizzly Bear | 2 | Big scarred grizzly standing up |
| `martial_arts_squirrel.png` | Martial Arts Squirrel | 1 | Squirrel in a karate gi and headband, fighting stance |
| `squire_zombie.png` | Squire Zombie | 1 | Shambling zombie in a squire's tabard |
| `the_blur.png` | The Blur | 2 | Gunslinger with a rifle, motion-blurred streaks |
| `yeti.png` | Yeti | 2 | Huge shaggy white yeti with frosty breath |
| `black_knight.png` | Black Knight | Mission 1 boss | Towering knight in jagged black plate armour, glowing red eye slit, cape |

### Enemy abilities (`EnemyAbilities\`)

| File | Name | Enemy | What it does | Picture idea |
|---|---|---|---|---|
| `bear_claw.png` | Bear Claw | Grizzly Bear | Hit for 10 | Three claw slash marks |
| `bite.png` | Bite | Squire Zombie | Hit for 5 | Rotten jaws biting |
| `bow_shot.png` | Bow Shot | Archer Skeleton | Hit for 4 | Bone arrow in flight |
| `fang_strike.png` | Fang Strike | Green Snake | Hit for 3 | Snake striking, fangs first |
| `fiery_stone.png` | Fiery Stone | Flaming Rock | Hit and burn | Flaming rock chunk flying |
| `flurry.png` | Flurry | Martial Arts Squirrel | Four quick hits | Small paws punching in a blur |
| `harden.png` | Harden | Armoured Beast | Gains 25 shield | Armour plates locking together |
| `heavy_swipe.png` | Heavy Swipe | Armoured Beast | Hit for 10 | Armoured claw in a wide swipe |
| `low_blow.png` | Low Blow | Furious Boxer | Gives you 1 Pain | Boxing glove hitting low with a pink impact star |
| `pummel.png` | Pummel | Yeti | Hit for 20 | Giant furry fist slamming down |
| `rifle_shot.png` | Rifle Shot | The Blur | Hit for 8 | Rifle firing with a muzzle flash |
| `right_hook.png` | Right Hook | Furious Boxer | Two hits for 10 | Boxing glove in a curving hook |
| `crushing_blow.png` | Crushing Blow | Black Knight | Hit for 15 and makes you Vulnerable | Black gauntlet smashing down, cracking a shield with yellow cracks |
| `dark_slash.png` | Dark Slash | Black Knight | Hit for 9 | Black sword leaving a dark purple slash arc |

### Passives (`Passives\`)

All passives are one shared pool: enemies use some and the player can buy any of them in the Passive Shop.

| File | Name | Belongs to | What it does | Picture idea |
|---|---|---|---|---|
| `cold_fur.png` | Cold Fur | Yeti | Every 3 times it's hit, chills you | Tuft of white fur with ice crystals |
| `explosive.png` | Explosive | Flaming Rock | Every 5 times it's hit, burns you | Cracked rock bursting with fire |
| `fast_reload.png` | Fast Reload | The Blur | Each rifle shot makes the next one faster | Bullets with speed lines |
| `fury.png` | Fury | Furious Boxer | Each time it's hit, gains 1 Power | Angry vein symbol over a clenched fist |
| `silver_knight.png` | Silver Knight's passive | Silver Knight (player) | Every 5 times you're hit, gain shield | Silver shield with a glowing knight's crest |
| `stoker.png` | Stoker | Player passive | Every 5 Heat gains, Burns the enemy | Shovel flinging flaming coals |
| `firebox.png` | Firebox | Player passive | Every Heat gain is 1 bigger | Open firebox door with roaring flames |
| `hot_nails.png` | Hot Nails | Player passive | Uproot does 1% more per Heat you have | Three nails glowing red-orange at the tips |
| `big_boiler.png` | Big Boiler | Player passive | 10 more Heat before overheating | Fat riveted boiler with a gauge and steam |
| `locomotive_engineer.png` | Locomotive Engineer's passive | Locomotive Engineer (player) | Overheating hurts the enemy instead, stall is half as long | Pressure gauge in the red, steam burst, orange sparks |
| `thick_skin.png` | Thick Skin | Grizzly Bear | Takes 1 less damage from each hit | Thick leathery hide patch with a deflected scratch |
| `venomous.png` | Venomous | Green Snake | Its hits poison you | Venom drop with a snake-eye pupil |
| `shield_mastery.png` | Shield Mastery | Shop | Shield items shield 5 more | Shield with a bright upward chevron on it |
| `kindling.png` | Kindling | Shop | Burn items burn 1 more | Small bundle of sticks catching a flame |
| `toxicology.png` | Toxicology | Shop | Poison items poison 1 more | Bubbling purple flask with a skull-shaped bubble |
| `cold_hands.png` | Cold Hands | Shop | Chill items 1s faster | Frosted open hand with a small clock or speed lines |
| `vanguard.png` | Vanguard | Shop | First-slot weapon +1 damage per weapon you have | Sword at the front of a row of weapons, the front one glowing |
| `parry.png` | Parry | Shop | Gain 2 shield whenever a weapon is used | Two crossed blades with a blue shield spark where they meet |
| `burning_roots.png` | Burning Roots | Shop | Burn for each nail uprooted | Nail pulled out with fiery roots trailing |
| `toxic_roots.png` | Toxic Roots | Shop | Poison for each nail uprooted | Nail pulled out with purple dripping roots |
| `frozen_roots.png` | Frozen Roots | Shop | Chill for every 2 nails uprooted | Nail pulled out with icy roots |
| `painful_roots.png` | Painful Roots | Shop | Pain for every 2 nails uprooted | Nail pulled out with pink thorny roots |
| `battle_hardened.png` | Battle Hardened | Shop | +5 max HP for every fight won | Red heart with a small upward arrow and a scar |
| `momentum.png` | Momentum | Shop | First-slot weapon grows +1 damage each hit | Sword with stacked motion arcs getting bigger |
| `black_bulwark.png` | Black Bulwark | Black Knight | Every 5 times it's hit, gains shield (10% of its max HP) | Black tower shield with a red emblem and blue shield sparks |

### Statuses (`Statuses\`)

| File | Status | Colour | Picture idea |
|---|---|---|---|
| `burn.png` | Burn | orange | Flame |
| `poison.png` | Poison | purple | Skull in a droplet |
| `chill.png` | Chill | cyan | Snowflake |
| `freeze.png` | Freeze | pale blue | Ice cube |
| `haste.png` | Haste | green | Winged boot or speed arrow |
| `pain.png` | Pain | pink | Cracked heart or thorn |
| `nail.png` | Nail | steel grey | Single iron nail |
| `power.png` | Power | red-orange | Flexed arm or upward fist |
| `vulnerable.png` | Vulnerable | yellow `#FADC50` | Cracked shield or a target with a crack through it |
| `no_heat_gain.png` | No Heat Gain | pale blue `#AAE6FF` | Flame in a padlock, or a flame with a bar across it |
| `overheat.png` | Overheated (items stalled) | orange `#FF6E1E` | Steaming cracked pressure valve or a bursting thermometer |

### Characters (`Characters\`)

| File | Name | Picture idea |
|---|---|---|
| `silver_knight.png` | Silver Knight | Knight in polished silver armour with a sword and shield |
| `locomotive_engineer.png` | Locomotive Engineer | Steam-train engineer in sooty overalls, cap and goggles, holding a coal shovel, glowing firebox behind |

### Map encounters (`Encounters\`)

| File | Name | Picture idea |
|---|---|---|
| `shop.png` | Shop | Cosy merchant stall with a lantern and items on display |
| `passive_shop.png` | Passive Shop | Mystic stall with glowing emblems and runes hanging on display |
