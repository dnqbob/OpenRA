﻿#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Orders
{
	class PowerDownOrderGenerator : GlobalButtonOrderGenerator
	{
		protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == MouseButton.Right)
				world.CancelInputMode();

			return OrderInner(world, mi);
		}

		protected bool IsValidTrait(ToggleConditionOnOrder t)
		{
			return !t.IsTraitDisabled && !t.IsTraitPaused;
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			mi.Button = MouseButton.Left;
			return OrderInner(world, mi).Any() ? "powerdown" : "powerdown-blocked";
		}

		protected IEnumerable<Order> OrderInner(World world, MouseInput mi)
		{
			if (mi.Button == MouseButton.Left)
			{
				var underCursor = world.ScreenMap.ActorsAtMouse(mi)
					.Select(a => a.Actor)
					.FirstOrDefault(a => a.Owner == world.LocalPlayer && a.TraitsImplementing<ToggleConditionOnOrder>()
						.Any(IsValidTrait));

				if (underCursor == null)
					yield break;

				yield return new Order("PowerDown", underCursor, false);
			}
		}
	}
}
