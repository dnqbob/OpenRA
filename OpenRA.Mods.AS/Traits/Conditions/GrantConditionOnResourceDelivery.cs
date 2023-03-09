#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Grants a condition when this refinery receives resources.")]
	public class GrantConditionOnResourceDeliveryInfo : PausableConditionalTraitInfo, Requires<RefineryInfo>
	{
		[GrantedConditionReference]
		[FieldLoader.Require]
		[Desc("The condition to grant.")]
		public readonly string Condition = null;

		[FieldLoader.Require]
		public readonly int Duration;

		[Desc("ResourceTypes to grant this condition. When empty, all resources trigger.")]
		public readonly string[] ResourceTypes = System.Array.Empty<string>();

		public override object Create(ActorInitializer init) { return new GrantConditionOnResourceDelivery(this); }
	}

	public class GrantConditionOnResourceDelivery : PausableConditionalTrait<GrantConditionOnResourceDeliveryInfo>, ITick, INotifyCreated, INotifyResourceAccepted
	{
		readonly GrantConditionOnResourceDeliveryInfo info;

		int token = Actor.InvalidConditionToken;

		int ticks;

		public GrantConditionOnResourceDelivery(GrantConditionOnResourceDeliveryInfo info)
			: base(info)
		{
			this.info = info;
		}

		void INotifyResourceAccepted.OnResourceAccepted(Actor self, Actor refinery, string resourceType, int count, int value)
		{
			if (self != refinery)
				return;

			if (IsTraitDisabled)
				return;

			if (info.ResourceTypes.Length != 0 && !Info.ResourceTypes.Contains(resourceType))
				return;

			ticks = info.Duration;

			if (token == Actor.InvalidConditionToken)
				token = self.GrantCondition(info.Condition);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || IsTraitPaused || --ticks > 0)
				return;

			if (token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);
		}
	}
}
