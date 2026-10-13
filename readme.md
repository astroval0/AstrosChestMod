# Astro's Chest Mod

this is a rewrite of a mod i made a little while ago.

This is a human written Vintage Story mod that aims to improve the experience for players who don't want to spend their time organizing chests.

## what it does

- press R or click the Sort Chests button at the top of the screen while you have chests open

https://github.com/user-attachments/assets/25a8c8d9-a869-4c8c-9a1e-f2e2e8ce4440


works on chests, trunks, reed chests, labeled chests and storage vessels. crates and barrels are left alone.

## searching


https://github.com/user-attachments/assets/b8caa0eb-7759-4cf4-9b6a-80b55bc87f6d


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
