#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Manages bot support power handling.")]
	public class SupportPowerBotModuleInfo : ConditionalTraitInfo, Requires<SupportPowerManagerInfo>
	{
		[Desc("Tells the AI how to use its support powers.")]
		[FieldLoader.LoadUsing(nameof(LoadDecisions))]
		public readonly List<SupportPowerDecision> Decisions = new();

		[Desc("Harmful terrain and locomotors for teleport weapon.")]
		public readonly Dictionary<string, HashSet<string>> LocmotorsAndHarmfulTerrain = new();

		[Desc("The interval of checking support powers when attacked.")]
		public int RespondToAttackCoolDown = 31;

		static object LoadDecisions(MiniYaml yaml)
		{
			var ret = new List<SupportPowerDecision>();
			var decisions = yaml.NodeWithKeyOrDefault("Decisions");
			if (decisions != null)
				foreach (var d in decisions.Value.Nodes)
					ret.Add(new SupportPowerDecision(d.Value));

			return ret;
		}

		public override object Create(ActorInitializer init) { return new SupportPowerBotModule(init.Self, this); }
	}

	public class SupportPowerBotModule : ConditionalTrait<SupportPowerBotModuleInfo>, IBotTick, IBotRespondToAttack, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly Dictionary<SupportPowerInstance, int> waitingPowers = new();
		readonly Dictionary<string, SupportPowerDecision> powerDecisions = new();
		readonly Dictionary<string, SupportPowerDecision> powerDecisionsWhenAttacked = new();
		readonly List<SupportPowerInstance> stalePowers = new();
		SupportPowerManager supportPowerManager;
		int attackedcooldown;

		public SupportPowerBotModule(Actor self, SupportPowerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			supportPowerManager = self.Owner.PlayerActor.Trait<SupportPowerManager>();
		}

		protected override void TraitEnabled(Actor self)
		{
			foreach (var decision in Info.Decisions)
			{
				switch (decision.DecisionMode)
				{
					case BotSupportPowerTriggerMode.OnAttacked:
						powerDecisionsWhenAttacked.Add(decision.OrderName, decision);
						break;

					case BotSupportPowerTriggerMode.Periodically:
						powerDecisions.Add(decision.OrderName, decision);
						break;

					default:
						break;
				}
			}
		}

		void IBotTick.BotTick(IBot bot)
		{
			attackedcooldown--;

			// We only check one support power per tick, as the support power check here is expensive,
			// which will go through all map cells in coarse and fine scans.
			var supportPowerNotChecked = true;
			foreach (var sp in supportPowerManager.Powers.Values)
			{
				if (sp.Disabled)
					continue;

				// Add power to dictionary if not in delay dictionary yet
				waitingPowers.TryAdd(sp, 0);
				if (waitingPowers[sp] > 0)
					waitingPowers[sp]--;

				// If we have recently tried and failed to find a use location for a power, then do not try again until later
				if (supportPowerNotChecked && sp.Ready && waitingPowers[sp] <= 0 && powerDecisions.TryGetValue(sp.Info.OrderName, out var powerDecision))
				{
					var strategyName = powerDecision.GetRandomStrategy(world.LocalRandom.Next());

					WPos? targetPosition = null;
					WPos? extraPosition = null;
					Actor actorNeedsPosition = null;
					supportPowerNotChecked = false;
					waitingPowers[sp] += powerDecision.GetNextScanTime(world, Info.Decisions.Count);

					if (powerDecision.CheckTargetLocationFirst)
					{
						if (powerDecision.NeedsConsiderTargetPosition(strategyName))
						{
							var (targetLocation, harmfulcellcheck) = FindCoarseAttackLocationToSupportPower(powerDecision, strategyName, false);
							if (targetLocation == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable coarse target location for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}

							// Found a target location, check for precise target
							(targetPosition, actorNeedsPosition) = FindFineAttackPositionToSupportPower(powerDecision, harmfulcellcheck, targetLocation.Value, strategyName, false);
							if (targetPosition == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final target position for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}
						}

						if (powerDecision.NeedsConsiderExtraLocation(strategyName))
						{
							if (actorNeedsPosition == null && powerDecision.ExtraLocationMode == BotSupportPowerTargetLocationMode.CellCanBeEnteredByTargetedActor)
							{
								AIUtils.BotDebug(
									$"{player.ResolvedPlayerName} can't find suitable target location actor for extra location for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}

							var (extraLocation, harmfulcellcheck) = FindCoarseAttackLocationToSupportPower(powerDecision, strategyName, true);
							if (extraLocation == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable coarse extra location for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}

							// Found a target location, check for precise target
							(extraPosition, _) = FindFineAttackPositionToSupportPower(powerDecision, harmfulcellcheck, extraLocation.Value, strategyName, true, actorNeedsPosition);
							if (extraPosition == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final extra position for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}
						}
					}
					else
					{
						if (powerDecision.NeedsConsiderExtraLocation(strategyName))
						{
							var (extraLocation, harmfulcellcheck) = FindCoarseAttackLocationToSupportPower(powerDecision, strategyName, true);
							if (extraLocation == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable coarse extra location for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}

							// Found a target location, check for precise target
							(extraPosition, actorNeedsPosition) = FindFineAttackPositionToSupportPower(powerDecision, harmfulcellcheck, extraLocation.Value, strategyName, true);
							if (extraPosition == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final extra position for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}
						}

						if (powerDecision.NeedsConsiderTargetPosition(strategyName))
						{
							if (actorNeedsPosition == null && powerDecision.TargetLocationMode == BotSupportPowerTargetLocationMode.CellCanBeEnteredByTargetedActor)
							{
								AIUtils.BotDebug(
									$"{player.ResolvedPlayerName} can't find suitable extra location actor for target location for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}

							var (targetLocation, harmfulcellcheck) = FindCoarseAttackLocationToSupportPower(powerDecision, strategyName, false);
							if (targetLocation == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable coarse target location for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}

							// Found a target location, check for precise target
							(targetPosition, _) =
								FindFineAttackPositionToSupportPower(powerDecision, harmfulcellcheck, targetLocation.Value, strategyName, false, actorNeedsPosition);
							if (targetPosition == null)
							{
								AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final target position for support power {sp.Info.OrderName}. Delaying rescan.");
								continue;
							}
						}
					}

					// Valid target found, delay by a few ticks to avoid rescanning before power fires via order
					AIUtils.BotDebug($"{player.ResolvedPlayerName} found new target position {(targetPosition != null ? targetPosition.Value.ToString() : "null")}" +
						$"and extra location {(extraPosition != null ? extraPosition.Value.ToString() : "null")} for support power {sp.Info.OrderName}.");

					var order = new Order(sp.Key, supportPowerManager.Self, false);

					if (powerDecision.NeedsConsiderTargetPosition(strategyName))
					{
						if (targetPosition != null)
							order = new Order(sp.Key, supportPowerManager.Self, Target.FromPos(targetPosition.Value), false);
						else
							continue;
					}

					order.SuppressVisualFeedback = true;
					order.ExtraData = powerDecision.ExtraData;

					if (powerDecision.NeedsConsiderExtraLocation(strategyName))
					{
						if (extraPosition != null)
							order.ExtraLocation = world.Map.CellContaining(extraPosition.Value);
						else
							continue;
					}

					// Note: SelectDirectionalTarget uses uint.MaxValue in ExtraData to indicate that the player did not pick a direction.
					bot.QueueOrder(order);
				}
			}

			// Remove stale powers
			stalePowers.AddRange(waitingPowers.Keys.Where(wp => !supportPowerManager.Powers.ContainsKey(wp.Key)));
			foreach (var p in stalePowers)
				waitingPowers.Remove(p);

			stalePowers.Clear();
		}

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			// Only consider enmey attack
			if (attackedcooldown < 0 && self != null && !self.IsDead && self.IsInWorld && e.Attacker.AppearsHostileTo(self))
			{
				attackedcooldown = Info.RespondToAttackCoolDown;
				foreach (var sp in supportPowerManager.Powers.Values)
				{
					// Note: We only check one support power per tick, as the support power check here may be expensive
					if (!sp.Disabled && sp.Ready && waitingPowers[sp] <= 0 && powerDecisionsWhenAttacked.TryGetValue(sp.Info.OrderName, out var powerDecision))
					{
						// Add power to dictionary if not in delay dictionary yet
						waitingPowers.TryAdd(sp, 0);

						var strategyName = powerDecision.GetRandomStrategy(world.LocalRandom.Next());

						if (strategyName != null && powerDecision.GetAttractiveness(self, player, strategyName) <= 0)
							continue;

						waitingPowers[sp] += powerDecision.GetNextScanTime(world, Info.Decisions.Count);

						WPos? targetPosition = null;
						WPos? extraPosition = null;
						Actor actorNeedsPosition = null;

						// Found a target location, check for precise target
						if (powerDecision.CheckTargetLocationFirst)
						{
							if (powerDecision.NeedsConsiderTargetPosition(strategyName))
							{
								(targetPosition, actorNeedsPosition) = FindFineAttackPositionToSupportPower(powerDecision, false, self.Location, strategyName, false);
								if (targetPosition == null)
								{
									AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final target position for support power {sp.Info.OrderName}. Delaying rescan.");
									break;
								}
							}

							if (powerDecision.NeedsConsiderExtraLocation(strategyName))
							{
								if (actorNeedsPosition == null && powerDecision.ExtraLocationMode == BotSupportPowerTargetLocationMode.CellCanBeEnteredByTargetedActor)
								{
									AIUtils.BotDebug(
										$"{player.ResolvedPlayerName} can't find suitable target location actor for extra location for support power {sp.Info.OrderName}. Delaying rescan.");
									break;
								}

								(extraPosition, _) = FindFineAttackPositionToSupportPower(powerDecision, false, self.Location, strategyName, true, actorNeedsPosition);
								if (extraPosition == null)
								{
									AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final extra position for support power {sp.Info.OrderName}. Delaying rescan.");
									break;
								}
							}
						}
						else
						{
							if (powerDecision.NeedsConsiderExtraLocation(strategyName))
							{
								(extraPosition, actorNeedsPosition) = FindFineAttackPositionToSupportPower(powerDecision, false, self.Location, strategyName, true);
								if (extraPosition == null)
								{
									AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final extra position for support power {sp.Info.OrderName}. Delaying rescan.");
									break;
								}
							}

							if (powerDecision.NeedsConsiderTargetPosition(strategyName))
							{
								if (actorNeedsPosition == null && powerDecision.TargetLocationMode == BotSupportPowerTargetLocationMode.CellCanBeEnteredByTargetedActor)
								{
									AIUtils.BotDebug(
										$"{player.ResolvedPlayerName} can't find suitable extra location actor for target location for support power {sp.Info.OrderName}. Delaying rescan.");
									break;
								}

								(targetPosition, _) = FindFineAttackPositionToSupportPower(powerDecision, false, self.Location, strategyName, false, actorNeedsPosition);
								if (targetPosition == null)
								{
									AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final target position for support power {sp.Info.OrderName}. Delaying rescan.");
									break;
								}
							}
						}

						// Valid target found, delay by a few ticks to avoid rescanning before power fires via order
						AIUtils.BotDebug($"{player.ResolvedPlayerName} found new target position {(targetPosition != null ? targetPosition.Value.ToString() : "null")}" +
							$"and extra location {(extraPosition != null ? extraPosition.Value.ToString() : "null")} for support power {sp.Info.OrderName}.");

						var order = new Order(sp.Key, supportPowerManager.Self, false);

						if (powerDecision.NeedsConsiderTargetPosition(strategyName))
						{
							if (targetPosition != null)
								order = new Order(sp.Key, supportPowerManager.Self, Target.FromPos(targetPosition.Value), false);
							else
								continue;
						}

						order.SuppressVisualFeedback = true;
						order.ExtraData = powerDecision.ExtraData;

						if (powerDecision.NeedsConsiderExtraLocation(strategyName))
						{
							if (extraPosition != null)
								order.ExtraLocation = world.Map.CellContaining(extraPosition.Value);
							else
								continue;
						}

						// Note: SelectDirectionalTarget uses uint.MaxValue in ExtraData to indicate that the player did not pick a direction.
						bot.QueueOrder(order);
						break;
					}
				}
			}
		}

		/// <summary>Scans the map in chunks, evaluating all actors in each.</summary>
		(CPos? Location, bool HarmfulCellCheck) FindCoarseAttackLocationToSupportPower(SupportPowerDecision powerDecision, string strategyName, bool asExtraPos)
		{
			if ((asExtraPos && powerDecision.Considerations[strategyName].ExtraPositionConsiderations.Count == 0)
				|| (!asExtraPos && powerDecision.Considerations[strategyName].TargetPositionConsiderations.Count == 0))
				return (null, false);

			// If the consideration is purely based on harmful cell check, we don't need to do a coarse scan, just return a dummy position and let fine scan handle it.
			if (powerDecision.IsHarmfulLocationConsideration(strategyName, asExtraPos))
			{
				return (CPos.Zero, true);
			}

			var map = world.Map;
			var scanIndiceSideLength = powerDecision.CoarseScanRadius * 141 / 100; // CoarseScanRadius * sqrt(2);
			var suitableLocations = new List<(MPos UV, int Attractiveness)>();
			var totalAttractiveness = 0;

			var actualMapWidth = map.Bounds.Width;
			var actualMapHeight = map.Bounds.Height;
			var xoffset = map.Bounds.X;
			var yoffset = map.Bounds.Y;

			var mapIndicesColumnCount = (actualMapWidth + scanIndiceSideLength - 1) / scanIndiceSideLength;
			var mapIndicesRowCount = (actualMapHeight + scanIndiceSideLength - 1) / scanIndiceSideLength;

			var overallIndiceWidth = mapIndicesColumnCount * scanIndiceSideLength;
			var overallIndiceHeight = mapIndicesRowCount * scanIndiceSideLength;

			xoffset -= (overallIndiceWidth - actualMapWidth) >> 1;
			yoffset -= (overallIndiceHeight - actualMapHeight) >> 1;

			for (var i = 0; i < overallIndiceWidth; i += scanIndiceSideLength)
			{
				for (var j = 0; j < overallIndiceHeight; j += scanIndiceSideLength)
				{
					var tl = new MPos(i + xoffset, j + yoffset);
					var br = new MPos(i + xoffset + scanIndiceSideLength, j + yoffset + scanIndiceSideLength);
					var region = new CellRegion(map.Grid.Type, tl, br);

					// HACK: The AI code should not be messing with raw coordinate transformations
					var wtl = world.Map.CenterOfCell(tl.ToCPos(map));
					var wbr = world.Map.CenterOfCell(br.ToCPos(map));
					var targets = world.ActorMap.ActorsInBox(wtl, wbr);

					var frozenTargets = player.FrozenActorLayer != null ? player.FrozenActorLayer.FrozenActorsInRegion(region) : Enumerable.Empty<FrozenActor>();
					var consideredAttractiveness = powerDecision.GetAttractiveness(targets, player, strategyName, asExtraPos)
						+ powerDecision.GetAttractiveness(frozenTargets, player, strategyName, asExtraPos);
					if (consideredAttractiveness < powerDecision.MinimumAttractiveness)
						continue;

					suitableLocations.Add((tl, consideredAttractiveness));
					totalAttractiveness += consideredAttractiveness;
				}
			}

			if (suitableLocations.Count == 0)
				return (null, false);

			// Pick a random location with above average attractiveness.
			var averageAttractiveness = totalAttractiveness / suitableLocations.Count;
			return (suitableLocations.Shuffle(world.LocalRandom)
				.First(x => x.Attractiveness >= averageAttractiveness)
				.UV.ToCPos(map), false);
		}

		/// <summary>Detail scans an area, evaluating positions.</summary>
		(WPos? BestPos, Actor BestActor) FindFineAttackPositionToSupportPower(
			SupportPowerDecision powerDecision,
			bool harmfulcellcheck,
			CPos checkPos,
			string strategyName,
			bool asExtraPos,
			Actor actorNeedsPlace = null,
			int extendedRange = 1)
		{
			if ((asExtraPos && powerDecision.Considerations[strategyName].ExtraPositionConsiderations.Count == 0)
				|| (!asExtraPos && powerDecision.Considerations[strategyName].TargetPositionConsiderations.Count == 0))
				return (null, null);

			// we don't look into checkPos when harmfulcellcheck is true
			if (harmfulcellcheck)
			{
				var locm = actorNeedsPlace.Info.TraitInfoOrDefault<MobileInfo>()?.Locomotor;
				if (locm == null)
					return (null, null);

				var harmfulSet = Info.LocmotorsAndHarmfulTerrain.TryGetValue(locm, out var hs) ? hs : null;
				if (harmfulSet != null)
				{
					var cell = world.Map.AllCells.Where(c => harmfulSet.Contains(world.Map.GetTerrainInfo(c).Type)).RandomOrDefault(world.LocalRandom);
					if (cell != CPos.Zero)
						return (world.Map.CenterOfCell(cell), null);
				}

				return (null, null);
			}

			WPos? bestPos = null;
			Actor bestActor = null;

			var bestAttractiveness = powerDecision.MinimumAttractiveness;
			var scanIndiceSideLength = powerDecision.CoarseScanRadius * 141 / 100; // CoarseScanRadius * sqrt(2);
			var fineScanIndiceSideLength = powerDecision.FineScanRadius * 141 / 100; // FineScanRadius * sqrt(2);

			for (var i = 0 - extendedRange; i <= scanIndiceSideLength + extendedRange; i += fineScanIndiceSideLength)
			{
				var x = checkPos.X + i;

				for (var j = 0 - extendedRange; j <= scanIndiceSideLength + extendedRange; j += fineScanIndiceSideLength)
				{
					var y = checkPos.Y + j;
					var pos = world.Map.CenterOfCell(new CPos(x, y));
					var consideredAttractiveness = 0;

					var (attractiveness, targetActor, frozenTarget) = powerDecision.GetAttractiveness(pos, player, strategyName, asExtraPos);

					consideredAttractiveness += attractiveness;

					if (consideredAttractiveness < bestAttractiveness)
						continue;

					bestAttractiveness = consideredAttractiveness;

					if ((powerDecision.ExtraLocationMode == BotSupportPowerTargetLocationMode.ActorLocation && asExtraPos)
						|| (powerDecision.TargetLocationMode == BotSupportPowerTargetLocationMode.ActorLocation && !asExtraPos))
					{
						if (targetActor == null)
						{
							if (frozenTarget == null)
								continue;

							bestPos = frozenTarget.CenterPosition;
						}
						else
						{
							bestPos = world.Map.CenterOfCell(targetActor.Location);
							bestActor = targetActor;
						}
					}
					else if ((powerDecision.ExtraLocationMode == BotSupportPowerTargetLocationMode.ActorCenter && asExtraPos)
						|| (powerDecision.TargetLocationMode == BotSupportPowerTargetLocationMode.ActorCenter && !asExtraPos))
					{
						if (targetActor == null)
						{
							if (frozenTarget == null)
								continue;

							bestPos = frozenTarget.CenterPosition;
						}
						else
						{
							bestPos = targetActor.CenterPosition;
							bestActor = targetActor;
						}
					}
					else if ((powerDecision.ExtraLocationMode == BotSupportPowerTargetLocationMode.CellCanBeEnteredByTargetedActor && asExtraPos)
						|| (powerDecision.TargetLocationMode == BotSupportPowerTargetLocationMode.CellCanBeEnteredByTargetedActor && !asExtraPos))
					{
						var cell = world.Map.FindTilesInAnnulus(new CPos(x, y), 0, fineScanIndiceSideLength).Shuffle(world.LocalRandom)
							.FirstOrDefault(c => actorNeedsPlace?.TraitOrDefault<IPositionable>()?.CanEnterCell(c) ?? false);

						if (cell == CPos.Zero)
							continue;

						bestPos = world.Map.CenterOfCell(cell);
					}
					else
						bestPos = world.Map.CenterOfCell(new CPos(x, y));
				}
			}

			return (bestPos, bestActor);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var waitingPowersNodes = waitingPowers
				.Select(kv => new MiniYamlNode(kv.Key.Key, FieldSaver.FormatValue(kv.Value)))
				.ToList();

			return new List<MiniYamlNode>()
			{
				new("WaitingPowers", "", waitingPowersNodes)
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
		{
			if (self.World.IsReplay)
				return;

			var waitingPowersNode = data.NodeWithKeyOrDefault("WaitingPowers");
			if (waitingPowersNode != null)
			{
				foreach (var n in waitingPowersNode.Value.Nodes)
				{
					if (supportPowerManager.Powers.TryGetValue(n.Key, out var instance))
						waitingPowers[instance] = FieldLoader.GetValue<int>("WaitingPowers", n.Value.Value);
				}
			}
		}
	}
}
