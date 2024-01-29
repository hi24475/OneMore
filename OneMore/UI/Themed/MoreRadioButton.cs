﻿//************************************************************************************************
// Copyright © 2022 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.UI
{
	using System;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Linq;
	using System.Windows.Forms;


	internal class MoreRadioButton : RadioButton
	{
		private const int BoxSize = 18;
		private const int Radius = 4;
		private const int Spacing = 4;
		private readonly ThemeManager manager;
		private readonly Color foreColor;


		/// <summary>
		/// Initialize a new instance.
		/// </summary>
		public MoreRadioButton()
		{
			//// force Paint event to fire
			SetStyle(ControlStyles.UserPaint, true);
			//// reduce flickering
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);

			foreColor = ForeColor;
			manager = ThemeManager.Instance;
		}


		/// <summary>
		/// Gets the state indicating normal, hover, or pressed.
		/// </summary>
		public MouseState MouseState { get; private set; }


		protected override void OnPaint(PaintEventArgs pevent)
		{
			var g = pevent.Graphics;
			g.Clear(BackColor);

			if (Appearance == Appearance.Button)
			{
				if (Enabled && (MouseState != MouseState.None || Checked))
				{
					using var brush = new SolidBrush(
						MouseState.HasFlag(MouseState.Pushed) || Checked
						? manager.ButtonHotBack
						: manager.ButtonBack);

					g.FillRoundedRectangle(brush, pevent.ClipRectangle, Radius);

					using var pen = new Pen(
						MouseState.HasFlag(MouseState.Pushed) || Checked
						? manager.ButtonPressBorder
						: manager.ButtonBorder);

					g.DrawRoundedRectangle(pen, pevent.ClipRectangle, Radius);
				}

				g.DrawImageUnscaled(Image,
					(pevent.ClipRectangle.Width - Image.Width) / 2,
					(pevent.ClipRectangle.Height - Image.Height) / 2
					);
			}
			else
			{
				var color = Enabled
					? manager.GetThemedColor(foreColor)
					: manager.GetThemedColor("GrayText");

				var text = string.IsNullOrWhiteSpace(Text) ? "M" : Text;
				var size = g.MeasureString(text, Font);

				g.SmoothingMode = SmoothingMode.HighQuality;

				using var pen = new Pen(color);
				var boxY = (size.Height - BoxSize) / 2;

				if (Checked)
				{
					using var fillBrush = new SolidBrush(Enabled
						? manager.GetThemedColor("Highlight")
						: color);

					g.FillEllipse(fillBrush, 0, boxY, BoxSize, BoxSize);

					using var dotbrush = new SolidBrush(BackColor);
					g.FillEllipse(dotbrush, 5, boxY + 5, BoxSize - 10, BoxSize - 10);
				}
				else
				{
					g.DrawEllipse(pen, 0, boxY, BoxSize, BoxSize);
				}

				using var brush = new SolidBrush(color);

				g.DrawString(Text, Font, brush,
					new Rectangle(BoxSize + Spacing,
						(pevent.ClipRectangle.Height - (int)size.Height) / 2,
						pevent.ClipRectangle.Width - (BoxSize + Spacing),
						(int)size.Height),
					new StringFormat
					{
						Trimming = StringTrimming.EllipsisCharacter,
						FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.NoWrap
					});
			}
		}


		protected override void OnTextChanged(EventArgs e)
		{
			base.OnTextChanged(e);

			// ensure we have something to measure and add fudge factor
			var text = string.IsNullOrWhiteSpace(Text) ? "M" : $"{Text}.";
			if (text.Contains(Environment.NewLine))
			{
				var parts = text.Split(
					new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

				var max = parts.Max(p => p.Length);
				text = $"{parts.First(p => p.Length == max)}.";
			}

			using var g = CreateGraphics();
			var size = g.MeasureString(text, Font);
			var w = (int)(size.Width + Spacing + BoxSize);
			if (Width != w)
			{
				AutoSize = false;
				Width = w;
			}
		}


		protected override void OnMouseDown(MouseEventArgs mevent)
		{
			if (Enabled)
			{
				MouseState |= MouseState.Pushed;
				base.OnMouseDown(mevent);
			}
		}


		protected override void OnMouseUp(MouseEventArgs mevent)
		{
			if (Enabled)
			{
				MouseState &= ~MouseState.Pushed;
				base.OnMouseUp(mevent);
			}
		}


		protected override void OnMouseLeave(EventArgs eventargs)
		{
			if (Enabled)
			{
				MouseState &= ~MouseState.Hover;
				base.OnMouseLeave(eventargs);
			}
		}


		protected override void OnMouseEnter(EventArgs eventargs)
		{
			if (Enabled)
			{
				MouseState |= MouseState.Hover;
				base.OnMouseEnter(eventargs);
			}
		}
	}
}
