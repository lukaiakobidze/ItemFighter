# ItemFighter art brief (for Codex)

This brief asks for **PNG images only**. The game already loads them: drop each file at the path given and it shows up in game (press F5 in the Fight Lab to reload). You don't need to change any code. The fight screen layout described at the end is already built, so the images just need to fit it.

## How to work (Codex)

- **Work in `D:\CODING\GameDev\ItemFighterArt`** (this brief sits in that folder). Write every image there, under the same folders the game uses: `src\ItemFighter.Game\Content\Art\<Kind>\<id>.png`, for example `src\ItemFighter.Game\Content\Art\Items\sword.png`. Luka copies that `src` folder over the game repo, so the paths must match exactly.
- Make one image per file, at the exact size in the table below, as a real PNG with an alpha channel (no JPEG renamed to .png).
- Keep the 15 images in `Statuses\` and `Passives\`: they are the style reference. **Redo every image in `Items\`** (the 16 made so far drifted into a detailed, realistic, blended look), then add the missing ones. Check the folder first to see what's done.
- Work through the tables in order and save each image as soon as it's finished, so progress can be checked while you work.
- Don't touch anything else in the folder or in the game repo.

## What the game is

ItemFighter is a roguelike autobattler, similar to The Bazaar. You fight one enemy at a time. Your items sit in a row of 6 slots and fire on their own cooldowns. Enemies have abilities that fire the same way, plus passives that trigger on their own. The tone is light fantasy, a bit humorous (a martial arts squirrel, a furious boxer).

## Style (same for every image)

**Match the existing status and passive icons exactly.** Open these before you start and compare every new image against them side by side:

- `src\ItemFighter.Game\Content\Art\Statuses\`: `burn.png`, `chill.png`, `freeze.png`, `haste.png`, `nail.png`, `pain.png`, `poison.png`, `power.png`
- `src\ItemFighter.Game\Content\Art\Passives\`: `cold_fur.png`, `explosive.png`, `fast_reload.png`, `fury.png`, `silver_knight.png`, `thick_skin.png`, `venomous.png`

The best single references are `fury.png`, `silver_knight.png`, `explosive.png` and `freeze.png`.

What that style is:

- **Bold dark outline** around the whole subject: thick (about 6 to 8px at 256×256), the same dark navy (around `#141826`), even all the way round. Thinner dark lines inside separate the big parts.
- **Solid colour areas with hard edges.** Each part is a flat base colour plus one darker shadow shape and one lighter highlight shape, sometimes a small bright glint. The edges between those tones are crisp, like cel shading or a vector icon. Depth comes from those stepped tones, not from blending.
- **Simple, chunky shapes**, slightly exaggerated and cartoony: big readable forms, few small details. A sword is a blade, a guard and a grip, not engraved metal.
- **Bright, saturated colours**, with the light from the top left: highlights on the upper left, shadows on the lower right.
- **Centred subject** filling about 80 to 90% of the canvas, slightly tilted where it helps (like the nail and the bullets).

Don't use:

- Soft airbrushed gradients or blended shading
- Painterly brush texture, scratches, grain, rust, wood grain or noise
- Fine realistic detail, rivets everywhere, engravings, or photo-like metal and fabric
- Thin or broken outlines, glows that smear outside the outline, or a drop shadow on the canvas

The items made before this change (the realistic apple, axe, potion, dagger and others in `Items\`) are exactly what to avoid.

- **Readable small:** the icons are shown as small as 36×36 pixels, so use one clear subject, strong silhouettes and high contrast. No text or numbers inside the image.
- **Background:** transparent for items, enemy abilities, passives and statuses. Enemies, the character and the shop get a simple background in the same flat style: two or three solid dark tones, no detail, so the subject stands out.
- **Lighting:** light comes from the top left, and every image uses the same palette family. The game's colour language: burn orange `#FF8C28`, poison purple `#B060E6`, chill cyan `#6EDCF0`, freeze pale blue `#AAE6FF`, pain pink `#E84682`, nail steel grey `#AAB4C3`, power red-orange `#F05028`, shield blue `#78AAE6`, heal green `#5AD26E`.
- **Format:** PNG, square, RGBA. The file name is the id in the tables, in lower case, with `.png` at the end.

| Kind | Folder | Size |
|---|---|---|
| Items | `src/ItemFighter.Game/Content/Art/Items/` | 256×256 |
| Enemy abilities | `src/ItemFighter.Game/Content/Art/EnemyAbilities/` | 256×256 |
| Passives | `src/ItemFighter.Game/Content/Art/Passives/` | 256×256 |
| Statuses | `src/ItemFighter.Game/Content/Art/Statuses/` | 128×128 |
| Enemies (portraits) | `src/ItemFighter.Game/Content/Art/Enemies/` | 512×512 |
| Characters (portraits) | `src/ItemFighter.Game/Content/Art/Characters/` | 512×512 |
| Map encounters | `src/ItemFighter.Game/Content/Art/Encounters/` | 512×512 |
| UI pieces | `src/ItemFighter.Game/Content/Art/UI/` | 128×128 |

## Items (28)

Rarity is shown by the game as a coloured strip, so don't put a frame or border on the image.

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
| `tasty_cleanser.png` | Tasty Cleanser | Rare | Removes your poison and burn | Fizzy green drink with a straw and a lemon slice |
| `tower_shield.png` | Tower Shield | Common | Big shield | Tall rectangular tower shield |
| `twin_blades.png` | Twin Blades | Rare | Two hits | Two short curved blades crossed |
| `venom_fang.png` | Venom Fang | Common | Poison | A single snake fang dripping venom |
| `war_banner.png` | War Banner | Rare | Hits harder for each weapon you have | Tattered red war banner on a spear |
| `weights.png` | Weights | Common | Slows an enemy ability | Iron kettlebell on a chain |
| `whetstone_blade.png` | Whetstone Blade | Rare | Hits, then gets sharper | Sword resting on a whetstone with sparks |

## Enemies (10)

Portraits: the enemy facing slightly toward the viewer, full head and upper body, menacing but charming. Same bold outline and flat stepped shading as the icons, only bigger.

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

## Enemy abilities (12)

These are the enemy's "items": shown as boxes with a picture, a name and a cooldown bar, like the player's items.

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

## Passives (7)

Passives are shown as small square icons in a row under a fighter's health bar, with the details on mouse-over. Make them read as a symbol or emblem, like a buff icon, more than a full object.

| File | Name | Belongs to | What it does | Picture idea |
|---|---|---|---|---|
| `cold_fur.png` | Cold Fur | Yeti | Every 3 times it's hit, chills you | Tuft of white fur with ice crystals |
| `explosive.png` | Explosive | Flaming Rock | Every 5 times it's hit, burns you | Cracked rock bursting with fire |
| `fast_reload.png` | Fast Reload | The Blur | Each rifle shot makes the next one faster | Bullets with speed lines |
| `fury.png` | Fury | Furious Boxer | Each time it's hit, gains 1 Power | Angry vein symbol over a clenched fist |
| `silver_knight.png` | Silver Knight's passive | Silver Knight (player) | Every 5 times you're hit, gain shield | Silver shield with a glowing knight's crest |
| `thick_skin.png` | Thick Skin | Grizzly Bear | Takes 1 less damage from each hit | Thick leathery hide patch with a deflected scratch |
| `venomous.png` | Venomous | Green Snake | Its hits poison you | Venom drop with a snake-eye pupil |

## Statuses (8)

Small icons shown next to the stack count in the status row under the health bar. Keep them very simple: one symbol, the status colour, transparent background.

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

## Characters (1)

| File | Name | Picture idea |
|---|---|---|
| `silver_knight.png` | Silver Knight | Knight in polished silver armour with a sword and shield |

## Map encounters (1)

| File | Name | Picture idea |
|---|---|---|
| `shop.png` | Shop | Cosy merchant stall with a lantern and items on display |

## UI pieces (2)

The icon frame that every status icon and passive icon sits on, made of two layers. The game draws the background, then the icon inside it (with a small margin), then the border on top. The border is **pure white** so the game can tint it: orange for burn, purple for poison, red for enemy passives, gold for yours, and so on.

| File | What it is | How it should look |
|---|---|---|
| `icon_frame_bg.png` | Background behind the icon | Dark square that fills the whole 128×128, near-black blue-grey (`#0E0F16` to `#1A1C28`): one flat tone with a slightly lighter hard-edged band along the top for depth, no texture. Slightly rounded corners (about 10px), transparent outside the corners |
| `icon_frame_border.png` | Thin border on top | Thin **white** line (about 4px at 128×128) following the same rounded square, everything else fully transparent. A faint inner glow is fine, but keep it white so tinting works |

Both must line up exactly: same size, same corner radius, same edges.

## Where the images appear in a fight

Enemy panel, top of the screen:

```
+-----------+  Enemy name
| portrait  |  [========== health bar ==========]
|  (Enemies)|  [P][P][P]          <- passives row: framed square icons (Passives/), mouse-over = details
|           |  [B] 3  [C] 1/5     <- statuses row: framed status icon (Statuses/) + stack count
+-----------+  +------+ +------+ +------+
 time left     | art  | | art  | | art  |   <- enemy abilities as boxes (EnemyAbilities/),
               | name | | name | | name |      name and cooldown bar under the picture
               | ==== | | ==== | | ==== |
               +------+ +------+ +------+
```

Player panel, bottom: the item cards (Items/), the status row under the health bar, and the character's passive as the same small square icon.

Status and passive icons sit inside the UI frame. The smallest sizes on screen are passive icons at 36×36 and status icons at 30×30 (frame included; the icon itself is a little smaller), and ability box art at about 104×62 (the image is fitted inside and keeps its shape). Item art on cards is about 124×112.

## Checklist

- 28 items, 12 enemy abilities, 7 passives, 8 statuses, 10 enemies, 1 character, 1 shop, 2 UI frame pieces: 69 PNGs. The statuses and passives are done already.
- File names exactly as in the tables (lower case, underscores).
- Square, transparent background except portraits, the shop and the frame background.
- No text, numbers, frames or rarity borders in the images.
