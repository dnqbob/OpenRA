#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;
namespace OpenRA.Mods.Common.Traits
{
	public class PlayerGlobalOrderConfigInfo : ILobbyOptions, ITraitInfo
	{
		[Translate]
		[Desc("Descriptive label for the global order option in the lobby.")]
		public readonly string GlobalOrderSettingLabel = "Advanced Command";

		[Translate]
		[Desc("Tooltip description for the global order option in the lobby")]
		public readonly string GlobalOrderSettingDescription = "Repair and Powerdown can be applied to group with dragging";

		[Desc("Default global order option: should be on or off.")]
		public readonly bool GlobalOrderSettingDefault = false;

		[Desc("Force the global order option by disabling changes in the lobby.")]
		public readonly bool DefaultGlobalOrderSettingLocked = false;

		[Desc("Whether to display the global order option in the lobby.")]
		public readonly bool DefaultGlobalOrderSettingVisible = true;

		[Desc("Display order for the global order option.")]
		public readonly int DefaultGlobalOrderSettingDisplayOrder = 0;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(Ruleset rules)
		{
			yield return new LobbyBooleanOption("globalorder", GlobalOrderSettingLabel, GlobalOrderSettingDescription,
				DefaultGlobalOrderSettingVisible, DefaultGlobalOrderSettingDisplayOrder,
				GlobalOrderSettingDefault, DefaultGlobalOrderSettingLocked);
		}

		public object Create(ActorInitializer init) { return new PlayerGlobalOrder(this); }
	}

	public class PlayerGlobalOrder
	{
		public readonly PlayerGlobalOrderConfigInfo Info;

		public PlayerGlobalOrder(PlayerGlobalOrderConfigInfo info)
		{
			Info = info;
		}
	}
}
