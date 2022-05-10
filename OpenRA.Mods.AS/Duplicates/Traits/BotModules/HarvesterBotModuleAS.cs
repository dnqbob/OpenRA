#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Put this on the Player actor. Manages bot harvesters to ensure they always continue harvesting as long as there are resources on the map.")]
	public class HarvesterBotModuleASInfo : ConditionalTraitInfo
	{
		[Desc("Actor types that are considered harvesters. If harvester count drops below RefineryTypes count, a new harvester is built.",
			"Leave empty to disable harvester replacement. Currently only needed by harvester replacement system.")]
		public readonly HashSet<string> HarvesterTypes = new HashSet<string>();

		[Desc("Number of harvesters per refinery to maintain.")]
		public readonly int HarvestersPerRefinery = 1;

		[Desc("Actor types that are counted as refineries. Currently only needed by harvester replacement system.")]
		public readonly HashSet<string> RefineryTypes = new HashSet<string>();

		[Desc("Interval (in ticks) between giving out orders to idle harvesters.")]
		public readonly int ScanForIdleHarvestersInterval = 50;

		[Desc("Avoid enemy actors nearby when searching for a new resource patch. Should be somewhere near the max weapon range.")]
		public readonly WDist HarvesterEnemyAvoidanceRadius = WDist.FromCells(8);

		public override object Create(ActorInitializer init) { return new HarvesterBotModuleAS(init.Self, this); }
	}

	public class HarvesterBotModuleAS : ConditionalTrait<HarvesterBotModuleASInfo>, IBotTick
	{
		class HarvesterTraitWrapper
		{
			public readonly Actor Actor;
			public readonly Harvester Harvester;
			public readonly Parachutable Parachutable;
			public readonly Mobile Mobile;

			public HarvesterTraitWrapper(Actor actor)
			{
				Actor = actor;
				Harvester = actor.Trait<Harvester>();
				Parachutable = actor.TraitOrDefault<Parachutable>();
				Mobile = actor.Trait<Mobile>();
			}
		}

		readonly World world;
		readonly Player player;
		readonly Func<Actor, bool> unitCannotBeOrdered;
		readonly Dictionary<Actor, HarvesterTraitWrapper> harvesters = new Dictionary<Actor, HarvesterTraitWrapper>();

		DomainIndex domainIndex;
		IResourceLayer resLayer;
		ResourceClaimLayer claimLayer;
		IBotRequestUnitProduction[] requestUnitProduction;
		int scanForIdleHarvestersTicks;

		public HarvesterBotModuleAS(Actor self, HarvesterBotModuleASInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			unitCannotBeOrdered = a => a.Owner != self.Owner || a.IsDead || !a.IsInWorld;
		}

		protected override void Created(Actor self)
		{
			// Special case handling is required for the Player actor.
			// Created is called before Player.PlayerActor is assigned,
			// so we must query player traits from self, which refers
			// for bot modules always to the Player actor.
			requestUnitProduction = self.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
		}

		protected override void TraitEnabled(Actor self)
		{
			domainIndex = world.WorldActor.Trait<DomainIndex>();
			resLayer = world.WorldActor.TraitOrDefault<ResourceLayer>();
			claimLayer = world.WorldActor.TraitOrDefault<ResourceClaimLayer>();
			scanForIdleHarvestersTicks = Info.ScanForIdleHarvestersInterval;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (resLayer == null || resLayer.IsEmpty)
				return;

			if (--scanForIdleHarvestersTicks > 0)
				return;

			var toRemove = harvesters.Keys.Where(unitCannotBeOrdered).ToList();
			foreach (var a in toRemove)
				harvesters.Remove(a);

			scanForIdleHarvestersTicks = Info.ScanForIdleHarvestersInterval;

			// Find new harvesters
			// TODO: Look for a more performance-friendly way to update this list
			var newHarvesters = world.ActorsHavingTrait<Harvester>().Where(a => a.Owner == player && !harvesters.ContainsKey(a));
			foreach (var a in newHarvesters)
				harvesters[a] = new HarvesterTraitWrapper(a);

			// Find idle harvesters and give them orders:
			foreach (var h in harvesters)
			{
				if (!h.Key.IsIdle)
				{
					// Ignore this actor if FindAndDeliverResources is working fine or it is performing a different activity
					if (!(h.Key.CurrentActivity is FindAndDeliverResources act) || !act.LastSearchFailed)
						continue;
				}

				if (h.Value.Parachutable != null && h.Value.Parachutable.IsInAir)
					continue;

				// Tell the idle harvester to quit slacking:
				var newSafeResourcePatch = FindNextResource(h.Key, h.Value);
				AIUtils.BotDebug("AI: Harvester {0} is idle. Ordering to {1} in search for new resources.".F(h.Key, newSafeResourcePatch));
				bot.QueueOrder(new Order("Harvest", h.Key, newSafeResourcePatch, false));
			}

			// Less harvesters than refineries - build a new harvester
			var unitBuilder = requestUnitProduction.FirstOrDefault(Exts.IsTraitEnabled);
			if (unitBuilder != null && Info.HarvesterTypes.Any())
			{
				var harvInfo = AIUtils.GetInfoByCommonName(Info.HarvesterTypes, player);
				var harvCountTooLow = AIUtils.CountActorByCommonName(Info.HarvesterTypes, player)
					< AIUtils.CountBuildingByCommonName(Info.RefineryTypes, player) * Info.HarvestersPerRefinery;
				if (harvCountTooLow && unitBuilder.RequestedProductionCount(bot, harvInfo.Name) == 0)
					unitBuilder.RequestUnitProduction(bot, harvInfo.Name);
			}
		}

		Target FindNextResource(Actor actor, HarvesterTraitWrapper harv)
		{
			bool IsValidResource(CPos cell) =>
				domainIndex.IsPassable(actor.Location, cell, harv.Mobile.Locomotor) &&
				harv.Harvester.CanHarvestCell(cell) &&
				claimLayer.CanClaimCell(actor, cell);

			var path = harv.Mobile.PathFinder.FindUnitPathToTargetCellByPredicate(
				actor, new[] { actor.Location }, IsValidResource, BlockedByActor.Stationary,
				loc => world.FindActorsInCircle(world.Map.CenterOfCell(loc), Info.HarvesterEnemyAvoidanceRadius)
					.Where(u => !u.IsDead && actor.Owner.RelationshipWith(u.Owner) == PlayerRelationship.Enemy)
					.Sum(u => Math.Max(WDist.Zero.Length, Info.HarvesterEnemyAvoidanceRadius.Length - (world.Map.CenterOfCell(loc) - u.CenterPosition).Length)));

			if (path.Count == 0)
				return Target.Invalid;

			return Target.FromCell(world, path[0]);
		}
	}
}
