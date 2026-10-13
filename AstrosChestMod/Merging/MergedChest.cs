using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstrosChestMod.Merging;

public record MergedChest(InventoryBase Inventory, BlockPos Pos, string Title, int Columns, SoundAttributes CloseSound);