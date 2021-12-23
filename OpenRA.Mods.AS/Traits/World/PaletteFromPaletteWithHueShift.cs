#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Create a palette by shifting the hue using HSV model on another palette.")]
	class PaletteFromPaletteWithHueShiftInfo : TraitInfo
	{
		[FieldLoader.Require]
		[Desc("Amount of hue shifted. 360 is a full circle.")]
		public readonly int HueOffset = 0;

		[PaletteDefinition]
		[FieldLoader.Require]
		[Desc("Internal palette name")]
		public readonly string Name = null;

		[PaletteReference]
		[FieldLoader.Require]
		[Desc("The name of the palette to base off.")]
		public readonly string BasePalette = null;

		[Desc("Allow palette modifiers to change the palette.")]
		public readonly bool AllowModifiers = true;

		public override object Create(ActorInitializer init) { return new PaletteFromPaletteWithHueShift(this); }
	}

	class PaletteFromPaletteWithHueShift : ILoadsPalettes, IProvidesAssetBrowserPalettes
	{
		readonly PaletteFromPaletteWithHueShiftInfo info;

		public PaletteFromPaletteWithHueShift(PaletteFromPaletteWithHueShiftInfo info) { this.info = info; }

		public void LoadPalettes(WorldRenderer wr)
		{
			var remap = new HueShiftRemap(info.HueOffset);
			wr.AddPalette(info.Name, new ImmutablePalette(wr.Palette(info.BasePalette).Palette, remap), info.AllowModifiers);
		}

		public IEnumerable<string> PaletteNames { get { yield return info.Name; } }
	}

	class HueShiftRemap : IPaletteRemap
	{
		readonly int hueoffset;

		public HueShiftRemap(int hueoffset)
		{
			this.hueoffset = hueoffset;
		}

		public Color GetRemappedColor(Color original, int index)
		{
			original.ToAhsv(out var a, out var h, out var s, out var v);

			h += hueoffset;
			if (h > 360.0f)
				h -= 360.0f;

			if (h < 0.0f)
				h += 360.0f;

			return Color.FromAhsv(a, h, s, v);
		}
	}
}
