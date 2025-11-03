using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GraphPlotApp
{
	static class Program
	{
		[STAThread]
		static void Main()
		{
			// Standard WinForms initialization
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new GraphForm());
		}
	}

	public class GraphForm : Form
	{
		// UI controls for rendering mode
		private FlowLayoutPanel topPanel;
		private RadioButton rbLine;
		private RadioButton rbPoints;
		private float pointSize = 6f;

		// Plot parameters (change if needed)
		private readonly double xMin = 0.1;
		private readonly double xMax = 1.2;
		private readonly double step = 0.01; // delta x

		// Fields used for drawing bounds
		private double xMinPlot, xMaxPlot, yMinPlot, yMaxPlot;

		public GraphForm()
		{
			Text = "Графік: y = tan(0.5*x) / (x^3) + 7.5";
			MinimumSize = new Size(480, 320);
			DoubleBuffered = true; // reduce flicker
			BackColor = Color.White;

			// Top panel with rendering mode selection
			topPanel = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				Padding = new Padding(6),
				WrapContents = false,
				FlowDirection = FlowDirection.LeftToRight
			};

			var lbl = new Label { Text = "Режим:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 8, 6, 0) };
			rbLine = new RadioButton { Text = "Лінійний", AutoSize = true, Checked = true, Padding = new Padding(6) };
			rbPoints = new RadioButton { Text = "Точковий", AutoSize = true, Padding = new Padding(6) };
			rbLine.CheckedChanged += (s, e) => Invalidate();
			rbPoints.CheckedChanged += (s, e) => Invalidate();

			topPanel.Controls.Add(lbl);
			topPanel.Controls.Add(rbLine);
			topPanel.Controls.Add(rbPoints);
			Controls.Add(topPanel);

			// When the form is resized, invalidate so Paint runs again and rescales
			Resize += (s, e) => Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

			var rect = ClientRectangle;
			int marginLeftRight = 50;
			int marginTop = topPanel?.Height + 10 ?? 60; // leave room for top controls
			var plotRect = new Rectangle(rect.Left + marginLeftRight, rect.Top + marginTop, Math.Max(10, rect.Width - 2 * marginLeftRight), Math.Max(10, rect.Height - marginTop - marginLeftRight));

			// Calculate function samples
			var xs = new List<double>();
			var ys = new List<double>();
			for (double x = xMin; x <= xMax + 1e-12; x += step)
			{
				double cosHalf = Math.Cos(0.5 * x);
				// avoid vertical asymptotes of tan(0.5*x)
				if (Math.Abs(cosHalf) < 1e-9) continue;

				double tanHalf = Math.Tan(0.5 * x);
				double denom = x * x * x; // x^3
				// function: y = tan(0.5*x) / (x^3) + 7.5
				double y = tanHalf / denom + 7.5;
				if (double.IsInfinity(y) || double.IsNaN(y)) continue;
				xs.Add(x);
				ys.Add(y);
			}

			if (xs.Count < 2) return;

			// determine y-range
			double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
			for (int i = 0; i < ys.Count; i++)
			{
				double y = ys[i];
				if (y < yMin) yMin = y;
				if (y > yMax) yMax = y;
			}
			if (Math.Abs(yMax - yMin) < 1e-9)
			{
				yMax = yMin + 1;
				yMin = yMin - 1;
			}

			// Add small padding to data ranges so curve doesn't touch borders
			double xPad = (xMax - xMin) * 0.02;
			double yPad = (yMax - yMin) * 0.08;
			xMinPlot = xMin - xPad;
			xMaxPlot = xMax + xPad;
			yMinPlot = yMin - yPad;
			yMaxPlot = yMax + yPad;

			// Map samples to screen points
			PointF[] pts = new PointF[xs.Count];
			for (int i = 0; i < xs.Count; i++)
			{
				float sx = (float)(plotRect.Left + (xs[i] - xMinPlot) / (xMaxPlot - xMinPlot) * plotRect.Width);
				float sy = (float)(plotRect.Top + (1 - (ys[i] - yMinPlot) / (yMaxPlot - yMinPlot)) * plotRect.Height);
				pts[i] = new PointF(sx, sy);
			}

			// draw plot background/border
			using (var borderPen = new Pen(Color.Black, 1))
				g.DrawRectangle(borderPen, plotRect);

			// draw grid
			using (var pen = new Pen(Color.LightGray, 1))
			{
				pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
				int gridX = 6, gridY = 6;
				for (int i = 0; i <= gridX; i++)
				{
					float x = plotRect.Left + i * (plotRect.Width) / (float)gridX;
					g.DrawLine(pen, x, plotRect.Top, x, plotRect.Bottom);
				}
				for (int i = 0; i <= gridY; i++)
				{
					float y = plotRect.Top + i * (plotRect.Height) / (float)gridY;
					g.DrawLine(pen, plotRect.Left, y, plotRect.Right, y);
				}
			}

			// draw axes (y=0 and x=0) if they are inside current data range
			using (var axisPen = new Pen(Color.Black, 2))
			{
				// horizontal axis y=0
				if (yMinPlot <= 0 && yMaxPlot >= 0)
				{
					float y0 = (float)(plotRect.Top + (1 - (0 - yMinPlot) / (yMaxPlot - yMinPlot)) * plotRect.Height);
					g.DrawLine(axisPen, plotRect.Left, y0, plotRect.Right, y0);
				}
				// vertical axis x=0
				if (xMinPlot <= 0 && xMaxPlot >= 0)
				{
					float x0 = (float)(plotRect.Left + (0 - xMinPlot) / (xMaxPlot - xMinPlot) * plotRect.Width);
					g.DrawLine(axisPen, x0, plotRect.Top, x0, plotRect.Bottom);
				}
			}

			// draw the curve either as lines or as points depending on UI
			if (rbPoints != null && rbPoints.Checked)
			{
				using (var brush = new SolidBrush(Color.Teal))
				{
					for (int i = 0; i < pts.Length; i++)
					{
						var p = pts[i];
						// draw small filled ellipse centered at the sample
						float ps = pointSize;
						g.FillEllipse(brush, p.X - ps / 2f, p.Y - ps / 2f, ps, ps);
					}
				}
			}
			else
			{
				using (var pen = new Pen(Color.Teal, 2))
				{
					g.DrawLines(pen, pts);
				}
			}

			// draw labels (simple)
			using (var brush = new SolidBrush(Color.Black))
			using (var font = new Font("Segoe UI", 9))
			{
				string xLabel = $"x: [{xMin:0.###} .. {xMax:0.###}], step={step}";
				string yLabel = $"y: [{yMin:0.###} .. {yMax:0.###}]";
				g.DrawString(xLabel, font, brush, rect.Left + 8, rect.Bottom - 20);
				g.DrawString(yLabel, font, brush, rect.Left + 8, rect.Bottom - 36);
			}
		}
	}
}
