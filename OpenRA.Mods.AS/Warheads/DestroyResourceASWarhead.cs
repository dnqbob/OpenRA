#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Warheads
{
	public class DestroyResourceASWarhead : WarheadAS
	{
		[Desc("Size of the area. The resources are seeded within this area.", "Provide 2 values for a ring effect (outer/inner).")]
		public readonly int[] Size = { 0, 0 };

		[FieldLoader.Require]
		[Desc("Types of resource which should be destroyed.")]
		public readonly string[] ResourceTypes;

		[Desc("Amount of resources to be destroyed per cell.")]
		public readonly int Density = int.MaxValue;

		// TODO: Allow maximum resource removal to be defined in total.
		public override void DoImpact(in Target target, WarheadArgs args)
		{
			var firedBy = args.SourceActor;
			if (!target.IsValidFor(firedBy))
				return;

			if (!IsValidImpact(target.CenterPosition, firedBy))
				return;

			var world = firedBy.World;
			var targetTile = world.Map.CellContaining(target.CenterPosition);
			var resLayer = world.WorldActor.Trait<IResourceLayer>();

			var minRange = (Size.Length > 1 && Size[1] > 0) ? Size[1] : 0;
			var allCells = world.Map.FindTilesInAnnulus(targetTile, minRange, Size[0]);

			// Destroy resources in the selected tiles
			foreach (var cell in allCells)
			{
				foreach (var resourceType in ResourceTypes)
				{
					resLayer.RemoveResource(resourceType, cell, Density);
				}
			}
		}
	}
}
