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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Allows the AI to issue the orders the AISupportPowerExplicitNotifier traits trigger.")]
	public class SupportPowerExplicitTargetBotModuleInfo : ConditionalTraitInfo
	{
		public override object Create(ActorInitializer init) { return new SupportPowerExplicitTargetBotModule(init.Self, this); }
	}

	public class SupportPowerExplicitTargetBotModule : ConditionalTrait<SupportPowerExplicitTargetBotModuleInfo>, IBotTick
	{
		readonly HashSet<TraitPair<AISupportPowerExplicitNotifier>> active = new HashSet<TraitPair<AISupportPowerExplicitNotifier>>();
		readonly World world;

		SupportPowerManager supportPowerManager;
		PlayerResources playerResource;

		public SupportPowerExplicitTargetBotModule(Actor self, SupportPowerExplicitTargetBotModuleInfo info)
			: base(info)
		{
			world = self.World;
		}

		protected override void Created(Actor self)
		{
			supportPowerManager = self.Trait<SupportPowerManager>();
			playerResource = self.Trait<PlayerResources>();

			base.Created(self);
		}

		public void AddEntry(TraitPair<AISupportPowerExplicitNotifier> entry)
		{
			active.Add(entry);
		}

		void IBotTick.BotTick(IBot bot)
		{
			foreach (var entry in active)
			{
				if (entry.Actor.IsDead || !entry.Actor.IsInWorld)
					continue;

				if (world.LocalRandom.Next(100) > entry.Trait.Info.ActivationChance)
					return;

				foreach (var power in supportPowerManager.Powers.Values)
				{
					if (power.Ready && entry.Trait.Info.OrderName.StartsWith(power.Info.OrderName))
					{
						if (power.Info.Cost != 0 && playerResource.Cash + playerResource.Resources < power.Info.Cost)
							continue;

						bot.QueueOrder(new Order(power.Key, supportPowerManager.Self,
							Target.FromCell(world, world.Map.CellContaining(entry.Actor.CenterPosition)), false)
						{ SuppressVisualFeedback = true });
					}
				}
			}

			active.Clear();
		}
	}
}
