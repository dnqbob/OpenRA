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
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Flags]
	public enum GrantRandomConditionOnDeliveryTrigger
	{
		None = 0,
		IncomingDelivery = 1,
		Delivery = 2
	}

	[Desc("Grants a random condition from a predefined list to the actor when created." +
		"Rerandomized when the actor changes ownership and when the trigger hits.")]
	public class GrantRandomConditionOnDeliveryInfo : TraitInfo
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("List of conditions to grant from.")]
		public readonly string[] Conditions = null;

		public readonly GrantRandomConditionOnDeliveryTrigger Triggers = GrantRandomConditionOnDeliveryTrigger.IncomingDelivery;

		public override object Create(ActorInitializer init) { return new GrantRandomConditionOnDelivery(this); }
	}

	public class GrantRandomConditionOnDelivery : INotifyCreated, INotifyOwnerChanged, INotifyDelivery
	{
		readonly GrantRandomConditionOnDeliveryInfo info;

		int conditionToken = Actor.InvalidConditionToken;

		public GrantRandomConditionOnDelivery(GrantRandomConditionOnDeliveryInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			if (!info.Conditions.Any())
				return;

			var condition = info.Conditions.Random(self.World.SharedRandom);
			conditionToken = self.GrantCondition(condition);
		}

		void INotifyDelivery.Delivered(Actor self)
		{
			if (!info.Triggers.HasFlag(GrantRandomConditionOnDeliveryTrigger.Delivery))
				return;

			if (conditionToken != Actor.InvalidConditionToken)
			{
				self.RevokeCondition(conditionToken);
				var condition = info.Conditions.Random(self.World.SharedRandom);
				conditionToken = self.GrantCondition(condition);
			}
		}

		void INotifyDelivery.IncomingDelivery(Actor self)
		{
			if (!info.Triggers.HasFlag(GrantRandomConditionOnDeliveryTrigger.IncomingDelivery))
				return;

			if (conditionToken != Actor.InvalidConditionToken)
			{
				self.RevokeCondition(conditionToken);
				var condition = info.Conditions.Random(self.World.SharedRandom);
				conditionToken = self.GrantCondition(condition);
			}
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			if (conditionToken != Actor.InvalidConditionToken)
			{
				self.RevokeCondition(conditionToken);
				var condition = info.Conditions.Random(self.World.SharedRandom);
				conditionToken = self.GrantCondition(condition);
			}
		}
	}
}
