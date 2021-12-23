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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Allows to play twinkle animations on resources.", "Attach this to the world actor.")]
	public class ResourceTwinkleLayerInfo : TraitInfo, Requires<IResourceLayerInfo>
	{
		[FieldLoader.Require]
		[Desc("Resource types to twinkle.")]
		public readonly HashSet<string> Types = null;

		[Desc("The percentage of resource cells to play the twinkle animation on.", "Use two values to randomize between them.")]
		public readonly int[] Ratio = { 5 };

		[Desc("Tick interval between two twinkle animation spawning.", "Use two values to randomize between them.")]
		public readonly int[] Interval = { 50 };

		[FieldLoader.Require]
		[Desc("Twinkle animation image.")]
		public readonly string Image = null;

		[SequenceReference(nameof(Image))]
		[Desc("Twinkle animation sequences.")]
		public readonly string[] Sequences = new string[] { "idle" };

		[PaletteReference]
		[Desc("Twinkle animation palette.")]
		public readonly string Palette = null;

		public override object Create(ActorInitializer init) { return new ResourceTwinkleLayer(init.Self, this); }
	}

	class ResourceTwinkleLayer : ITick, IWorldLoaded
	{
		readonly IResourceLayer resourceLayer;
		readonly ResourceTwinkleLayerInfo info;

		readonly World world;
		readonly HashSet<CPos> cells = new HashSet<CPos>();

		int ticks;

		public ResourceTwinkleLayer(Actor self, ResourceTwinkleLayerInfo info)
		{
			world = self.World;
			this.info = info;

			ticks = info.Interval.Length == 2
				? world.SharedRandom.Next(info.Interval[0], info.Interval[1])
				: info.Interval[0];

			resourceLayer = self.Trait<IResourceLayer>();
			resourceLayer.CellChanged += UpdateCells;
		}

		void UpdateCells(CPos cell, string resType)
		{
			if (resType == null)
			{
				cells.Remove(cell);
				return;
			}

			if (info.Types.Contains(resType))
			{
				var resourceContent = resourceLayer.GetResource(cell);
				if (resourceContent.Density > 0)
					cells.Add(cell);
				else
					cells.Remove(cell);
			}
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			foreach (var cell in w.Map.AllCells)
			{
				var type = resourceLayer.GetResource(cell).Type;
				if (type != null && info.Types.Contains(type))
					cells.Add(cell);
			}
		}

		void ITick.Tick(Actor self)
		{
			if (--ticks > 0)
				return;

			var twinkleable = cells.Shuffle(world.SharedRandom);
			var ratio = info.Ratio.Length == 2
					? world.SharedRandom.Next(info.Ratio[0], info.Ratio[1])
					: info.Ratio[0];

			var twinkamount = twinkleable.Count() * ratio / 100;
			var twinkpositions = twinkleable.Take(twinkamount).Select(x => world.Map.CenterOfCell(x));

			foreach (var pos in twinkpositions)
				world.AddFrameEndTask(w => w.Add(new SpriteEffect(pos, w, info.Image, info.Sequences.Random(w.SharedRandom), info.Palette)));

			ticks = info.Interval.Length == 2
				? world.SharedRandom.Next(info.Interval[0], info.Interval[1])
				: info.Interval[0];
		}
	}
}
