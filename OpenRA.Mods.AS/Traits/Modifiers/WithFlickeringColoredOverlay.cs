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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Display a flickering colored overlay when a timed condition is active.")]
	public class WithFlickeringColoredOverlayInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		[Desc("Color to overlay.")]
		public readonly Color Color;

		[FieldLoader.Require]
		[Desc("Amount of maximum difference. Alpha is ignored.")]
		public readonly Color Amplitude;

		public readonly int QuantizationCount = 16;

		public override object Create(ActorInitializer init) { return new WithFlickeringColoredOverlay(this); }
	}

	public class WithFlickeringColoredOverlay : ConditionalTrait<WithFlickeringColoredOverlayInfo>, IRenderModifier, ITick
	{
		readonly WithFlickeringColoredOverlayInfo info;
		readonly float alpha;
		readonly int offset;

		float3 tint;
		int t;

		public WithFlickeringColoredOverlay(WithFlickeringColoredOverlayInfo info)
			: base(info)
		{
			this.info = info;
			tint = new float3(info.Color.R, info.Color.G, info.Color.B) / 255f;
			alpha = info.Color.A / 255f;
			offset = 1024 / info.QuantizationCount;
		}

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			if (IsTraitDisabled)
				return r;

			return ModifiedRender(r);
		}

		IEnumerable<IRenderable> ModifiedRender(IEnumerable<IRenderable> r)
		{
			foreach (var a in r)
			{
				yield return a;

				if (!a.IsDecoration && a is IModifyableRenderable ma)
					yield return ma.WithTint(tint, ma.TintModifiers | TintModifiers.ReplaceColor).WithAlpha(alpha);
			}
		}

		IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds)
		{
			return bounds;
		}

		void ITick.Tick(Actor self)
		{
			t = (t + offset) % 1024;

			var red = (info.Color.R + info.Amplitude.R * WAngle.FromDegrees(t).Cos() / 1024).Clamp(0, 255);
			var green = (info.Color.G + info.Amplitude.G * WAngle.FromDegrees(t).Cos() / 1024).Clamp(0, 255);
			var blue = (info.Color.B + info.Amplitude.B * WAngle.FromDegrees(t).Cos() / 1024).Clamp(0, 255);

			tint = new float3(red, green, blue) / 255f;
		}
	}
}
