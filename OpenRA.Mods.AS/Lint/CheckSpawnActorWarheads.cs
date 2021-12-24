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
using OpenRA.Mods.AS.Warheads;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits;
using OpenRA.Server;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Lint
{
	class CheckSpawnActorWarheads : ILintRulesPass, ILintServerMapPass
	{
		void ILintRulesPass.Run(Action<string> emitError, Action<string> emitWarning, ModData modData, Ruleset rules)
		{
			Run(emitError, rules);
		}

		void ILintServerMapPass.Run(Action<string> emitError, Action<string> emitWarning, ModData modData, MapPreview map, Ruleset mapRules)
		{
			Run(emitError, mapRules);
		}

		void Run(Action<string> emitError, Ruleset rules)
		{
			foreach (var weaponInfo in rules.Weapons)
			{
				var warheads = weaponInfo.Value.Warheads.OfType<SpawnActorWarhead>().ToList();

				foreach (var warhead in warheads)
				{
					foreach (var a in warhead.Actors)
					{
						if (!rules.Actors.ContainsKey(a.ToLowerInvariant()))
						{
							emitError("Warhead type {0} tries to spawn invalid actor {1}!"
							.F(weaponInfo.Key, a));
							break;
						}

						if (!rules.Actors[a.ToLowerInvariant()].HasTraitInfo<IOccupySpaceInfo>())
							emitError("Warhead type {0} tries to spawn unpositionable actor {1}!"
							.F(weaponInfo.Key, a));

						if (rules.Actors[a.ToLowerInvariant()].HasTraitInfo<BuildingInfo>())
							emitError("Warhead type {0} tries to spawn building {1}!"
							.F(weaponInfo.Key, a));

						if (!rules.Actors[a.ToLowerInvariant()].HasTraitInfo<ParachutableInfo>() && warhead.Paradrop == true)
							emitError("Warhead type {0} tries to paradrop actor {1} which doesn't have the Parachutable trait!"
							.F(weaponInfo.Key, a));
					}
				}
			}
		}
	}
}
