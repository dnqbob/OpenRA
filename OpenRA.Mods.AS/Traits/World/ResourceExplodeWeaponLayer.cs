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
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Allows to play periodic explosions on resources.", "Attach this to the world actor.")]
	public class ResourceExplodeWeaponLayerInfo : TraitInfo, Requires<IResourceLayerInfo>, IRulesetLoaded
	{
		[FieldLoader.Require]
		[Desc("Resource types to trigger on.")]
		public readonly HashSet<string> Types = null;

		[Desc("The percentage of resource cells to trigger the explosion on in a single frame.", "Use two values to randomize between them.")]
		public readonly int[] Ratio = { 5 };

		[Desc("Tick interval between two explosions spawning.", "Use two values to randomize between them.")]
		public readonly int[] Interval = { 50 };

		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		public WeaponInfo WeaponInfo { get; private set; }

		public override object Create(ActorInitializer init) { return new ResourceExplodeWeaponLayer(init.Self, this); }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var weaponToLower = Weapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weaponInfo))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));

			WeaponInfo = weaponInfo;
		}
	}

	class ResourceExplodeWeaponLayer : ITick, IWorldLoaded
	{
		readonly IResourceLayer resourceLayer;
		readonly ResourceExplodeWeaponLayerInfo info;

		readonly World world;
		readonly HashSet<CPos> cells = new HashSet<CPos>();

		int ticks;

		public ResourceExplodeWeaponLayer(Actor self, ResourceExplodeWeaponLayerInfo info)
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
			{
				var args = new WarheadArgs
				{
					Weapon = info.WeaponInfo,
					Source = pos,
					SourceActor = self,
					WeaponTarget = Target.FromPos(pos),
				};

				info.WeaponInfo.Impact(Target.FromPos(pos), args);

				if (info.WeaponInfo.Report != null && info.WeaponInfo.Report.Any())
					Game.Sound.Play(SoundType.World, info.WeaponInfo.Report.Random(self.World.SharedRandom), self.CenterPosition);
			}

			ticks = info.Interval.Length == 2
				? world.SharedRandom.Next(info.Interval[0], info.Interval[1])
				: info.Interval[0];
		}
	}
}
