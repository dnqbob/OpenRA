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

using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public struct GlobalButtonOrderSelectionBoxAnnotationRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos pos;
		readonly Rectangle decorationBounds;
		readonly Color color;

		public GlobalButtonOrderSelectionBoxAnnotationRenderable(Actor actor, Rectangle decorationBounds, Color color)
			: this(actor.CenterPosition, decorationBounds, color) { }

		public GlobalButtonOrderSelectionBoxAnnotationRenderable(WPos pos, Rectangle decorationBounds, Color color)
		{
			this.pos = pos;
			this.decorationBounds = decorationBounds;
			this.color = color;
		}

		public WPos Pos { get { return pos; } }

		public PaletteReference Palette { get { return null; } }
		public int ZOffset { get { return 0; } }
		public bool IsDecoration { get { return true; } }

		public IRenderable WithPalette(PaletteReference newPalette) { return this; }
		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(WVec vec) { return new GlobalButtonOrderSelectionBoxAnnotationRenderable(pos + vec, decorationBounds, color); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var tl = wr.Viewport.WorldToViewPx(new float2(decorationBounds.Left, decorationBounds.Top)).ToFloat2();
			var br = wr.Viewport.WorldToViewPx(new float2(decorationBounds.Right, decorationBounds.Bottom)).ToFloat2();
			var tr = new float2(br.X, tl.Y);
			var bl = new float2(tl.X, br.Y);
			var u = new float2(6, 0);
			var v = new float2(0, 6);
			var u1 = new float2(3, 0);
			var v1 = new float2(0, 3);
			float width = 1F;

			var cr = Game.Renderer.RgbaColorRenderer;
			cr.DrawLine(new float3[] { tl + u, tl, tl + v }, width, color, true);
			cr.DrawLine(new float3[] { tr - u, tr, tr + v }, width, color, true);
			cr.DrawLine(new float3[] { br - u, br, br - v }, width, color, true);
			cr.DrawLine(new float3[] { bl + u, bl, bl - v }, width, color, true);

			cr.DrawLine(new float3[] { tl + u1 + v1, tl }, width, color, true);
			cr.DrawLine(new float3[] { tr - u1 + v1, tr }, width, color, true);
			cr.DrawLine(new float3[] { br - u1 - v1, br }, width, color, true);
			cr.DrawLine(new float3[] { bl + u1 - v1, bl }, width, color, true);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
