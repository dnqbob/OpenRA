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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits.Render
{
	[Desc("Display the time remaining until the next cash is given by the owner's PlayerCashTrickler trait.")]
	class PlayerCashTricklerBarInfo : TraitInfo
	{
		[Desc("Defines to which players the bar is to be shown.")]
		public readonly PlayerRelationship DisplayPlayerRelationships = PlayerRelationship.Ally;

		public readonly Color Color = Color.Magenta;

		public override object Create(ActorInitializer init) { return new PlayerCashTricklerBar(init.Self, this); }
	}

	class PlayerCashTricklerBar : ISelectionBar, INotifyCreated, INotifyOwnerChanged
	{
		readonly Actor self;
		readonly PlayerCashTricklerBarInfo info;
		IEnumerable<PlayerCashTrickler> cashTricklers;

		public PlayerCashTricklerBar(Actor self, PlayerCashTricklerBarInfo info)
		{
			this.self = self;
			this.info = info;
		}

		float ISelectionBar.GetValue()
		{
			var viewer = self.World.RenderPlayer ?? self.World.LocalPlayer;
			if (viewer != null && !info.DisplayPlayerRelationships.HasRelationship(self.Owner.RelationshipWith(viewer)))
				return 0;

			var complete = cashTricklers.Min(ct => (float)ct.Ticks / ct.Info.Interval);
			return 1 - complete;
		}

		Color ISelectionBar.GetColor() { return info.Color; }

		void INotifyCreated.Created(Actor self)
		{
			cashTricklers = self.Owner.PlayerActor.TraitsImplementing<PlayerCashTrickler>().ToArray();
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			cashTricklers = newOwner.PlayerActor.TraitsImplementing<PlayerCashTrickler>().ToArray();
		}

		bool ISelectionBar.DisplayWhenEmpty { get { return false; } }
	}
}
