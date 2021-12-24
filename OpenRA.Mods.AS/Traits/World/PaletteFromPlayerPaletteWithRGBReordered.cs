#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Create a palette by reordering the channels of a player palette.")]
	class PaletteFromPlayerPaletteWithRGBReorderedInfo : TraitInfo
	{
		[Desc("Order of the channels in the new palette. The alpha channel can't be reordered. Available modes: BGRA, RBGA, BRGA, GBRA and GRBA.")]
		public readonly RGBReorderMode ReorderMode = RGBReorderMode.BGRA;

		[PaletteDefinition]
		[FieldLoader.Require]
		[Desc("Internal palette name")]
		public readonly string BaseName = null;

		[FieldLoader.Require]
		[PaletteReference(true)]
		[Desc("The name of the player palette to base off.")]
		public readonly string BasePalette = null;

		[Desc("Allow palette modifiers to change the palette.")]
		public readonly bool AllowModifiers = true;

		public override object Create(ActorInitializer init) { return new PaletteFromPlayerPaletteWithRGBReordered(this); }
	}

	class PaletteFromPlayerPaletteWithRGBReordered : ILoadsPlayerPalettes
	{
		readonly PaletteFromPlayerPaletteWithRGBReorderedInfo info;

		public PaletteFromPlayerPaletteWithRGBReordered(PaletteFromPlayerPaletteWithRGBReorderedInfo info) { this.info = info; }

		public void LoadPlayerPalettes(WorldRenderer wr, string playerName, Color color, bool replaceExisting)
		{
			var remap = new RGBRemap(info.ReorderMode);
			var pal = new ImmutablePalette(wr.Palette(info.BasePalette + playerName).Palette, remap);
			wr.AddPalette(info.BaseName + playerName, pal, info.AllowModifiers, replaceExisting);
		}
	}
}
