# Astro's Chest Mod

this is a rewrite of a mod i made a little while ago.

This is a human written Vintage Story mod that aims to improve the experience for players who don't want to spend their time organizing chests.

## what it does

- press R or click the Sort Chests button at the top of the screen while you have chests open
- merges partial stacks of the same item
- sorts every open chest like it's one big chest, in this order: valuables, tools, weapons and armor, clothing and bags, food, farming and plants, ores and metals, fuel, crafting materials, building, containers and cookware, lighting
- every category starts on its own row
- a group of the same items goes to the next chest instead of getting split between two, if there's room
- chests remember the order they were first sorted in, so your stuff ends up in the same chests every time

works on chests, trunks, reed chests, labeled chests and storage vessels. crates and barrels are left alone.

## merging chests

- every chest window gets a checkbox next to its title, tick it to move that chest into one window with the others you merged
- chests you merge together stay together, opening one of them opens the rest that are in reach
- untick a chest in the merged window to give it its own window back, it opens on its own after that
- the merged window fits itself to your screen and sits right above your inventory without covering your other chests
- turn on MergeIntoOneChest to show all the merged slots in one grid like it's one big chest, sorting treats it like one chest too
- type in the search box at the top of the merged window and everything that doesn't match gets dimmed, so you can see right away which chest your stuff is in
- the search looks at item names and categories, so typing food lights up all your food

## client side only

every move is a normal inventory move, same as if you clicked it yourself, so the server does not need the mod. it works on any server.

opening a merged group just right clicks the other chests for you, so that works on any server too.

## settings

R can be rebound in Settings > Controls.

the rest is in `VintagestoryData/ModConfig/astroschestmod.json`. if you have [Configlib](https://mods.vintagestory.at/configlib) you can change them in game through that.

| setting | default | what it does |
|---|---|---|
| ShowSortButton | true | shows the sort button while a chest is open |
| ShowSortSummary | true | says in chat how many stacks got merged and moved |
| StartCategoriesOnNewRow | true | leaves a gap so every category starts on its own row |
| KeepGroupsInOneChest | true | moves a group to the next chest instead of splitting it |
| MergeIntoOneChest | false | shows the merged chests as one big chest |

## building

needs the .NET 10 SDK and Vintage Story 1.22 installed in the default place (`%APPDATA%\Vintagestory`). building copies the mod straight into `VintagestoryData\Mods\AstrosChestMod`, so close the game first.

## license

LGPL 2.1, see `LICENSE`.
