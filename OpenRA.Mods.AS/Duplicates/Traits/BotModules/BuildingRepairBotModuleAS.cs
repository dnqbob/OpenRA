#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Manages AI repairing base buildings.")]
	public class BuildingRepairBotModuleASInfo : ConditionalTraitInfo
	{
		public override object Create(ActorInitializer init) { return new BuildingRepairBotModuleAS(init.Self, this); }
	}

	public class BuildingRepairBotModuleAS : ConditionalTrait<BuildingRepairBotModuleASInfo>, IBotRespondToAttack
	{
		public BuildingRepairBotModuleAS(Actor self, BuildingRepairBotModuleASInfo info)
			: base(info) { }

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			var rb = self.TraitOrDefault<RepairableBuilding>();
			if (rb != null)
			{
				if (e.DamageState > DamageState.Light && e.PreviousDamageState <= DamageState.Light && !rb.RepairActive)
				{
					AIUtils.BotDebug("Bot noticed damage {0} {1}->{2}, repairing.",
						self, e.PreviousDamageState, e.DamageState);
					bot.QueueOrder(new Order("RepairBuilding", self.Owner.PlayerActor, Target.FromActor(self), false));
				}
			}
		}
	}
}
