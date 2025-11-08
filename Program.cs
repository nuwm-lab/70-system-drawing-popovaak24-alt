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
    // UI controls for rendering mode and parameters
    private FlowLayoutPanel topPanel;
    private RadioButton rbLine;
    private RadioButton rbPoints;
    private float pointSize = 6f;
    // Sampling provider (separated responsibility and cached internally)
    private readonly SampleProvider sampleProvider = new SampleProvider(0.1, 1.2, 0.01);
    // Plot parameters accessible from UI
    // Note: sampleProvider keeps the authoritative parameter values; UI changes update it.
        // Fields used for drawing bounds
        private double xMinPlot, xMaxPlot, yMinPlot, yMaxPlot;
        public GraphForm()
        {
            Text = "Графік: y = tan(0.5*x) / (x^3) + 7.5";
            MinimumSize = new Size(480, 320);
            DoubleBuffered = true; // reduce flicker
            BackColor = Color.White;
            // Top panel with rendering mode selection and parameter controls
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

            // Parameter controls: xMin, xMax, step, point size and Y-clamp
            var lblXMin = new Label { Text = "xMin:", AutoSize = true, Padding = new Padding(6, 8, 6, 0) };
            var nudXMin = new NumericUpDown { DecimalPlaces = 3, Increment = 1m / 100m, Minimum = -1000, Maximum = 1000, Value = (decimal)sampleProvider.XMin, Width = 80 };
            var lblXMax = new Label { Text = "xMax:", AutoSize = true, Padding = new Padding(6, 8, 6, 0) };
            var nudXMax = new NumericUpDown { DecimalPlaces = 3, Increment = 1m / 100m, Minimum = -1000, Maximum = 1000, Value = (decimal)sampleProvider.XMax, Width = 80 };
            var lblStep = new Label { Text = "step:", AutoSize = true, Padding = new Padding(6, 8, 6, 0) };
            var nudStep = new NumericUpDown { DecimalPlaces = 4, Increment = 1m / 1000m, Minimum = 0.0001m, Maximum = 1, Value = (decimal)sampleProvider.Step, Width = 80 };
            var lblPoint = new Label { Text = "point:", AutoSize = true, Padding = new Padding(6, 8, 6, 0) };
            var nudPoint = new NumericUpDown { DecimalPlaces = 1, Increment = 0.5m, Minimum = 1, Maximum = 20, Value = (decimal)pointSize, Width = 60 };
            var chkClamp = new CheckBox { Text = "Clamp Y", AutoSize = true, Padding = new Padding(6) };
            var nudClamp = new NumericUpDown { DecimalPlaces = 0, Increment = 1m, Minimum = 1, Maximum = 100000, Value = 100, Width = 80, Enabled = false };

            chkClamp.CheckedChanged += (s, e) => { nudClamp.Enabled = chkClamp.Checked; Invalidate(); };

            // Wire numeric controls to update provider / settings
            nudXMin.ValueChanged += (s, e) => { sampleProvider.XMin = (double)nudXMin.Value; Invalidate(); };
            nudXMax.ValueChanged += (s, e) => { sampleProvider.XMax = (double)nudXMax.Value; Invalidate(); };
            nudStep.ValueChanged += (s, e) => { sampleProvider.Step = (double)nudStep.Value; Invalidate(); };
            nudPoint.ValueChanged += (s, e) => { pointSize = (float)nudPoint.Value; Invalidate(); };

            topPanel.Controls.Add(lblXMin);
            topPanel.Controls.Add(nudXMin);
            topPanel.Controls.Add(lblXMax);
            topPanel.Controls.Add(nudXMax);
            topPanel.Controls.Add(lblStep);
            topPanel.Controls.Add(nudStep);
            topPanel.Controls.Add(lblPoint);
            topPanel.Controls.Add(nudPoint);
            topPanel.Controls.Add(chkClamp);
            topPanel.Controls.Add(nudClamp);
            Controls.Add(topPanel);
  // When the form is resized, invalidate so Paint runs again and rescales
            Resize += (s, e) => Invalidate();
        }

        // (Sampling is delegated to SampleProvider)
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
 g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = ClientRectangle;
            int marginLeftRight = 50;
            int marginTop = topPanel?.Height + 10 ?? 60; // leave room for top controls
            var plotRect = new Rectangle(rect.Left + marginLeftRight, rect.Top + marginTop, Math.Max(10, rect.Width - 2 * marginLeftRight), Math.Max(10, rect.Height - marginTop - marginLeftRight));

            // Retrieve samples from provider (cached inside provider)
            var tup = sampleProvider.GetSamples();
            var xs = tup.Xs;
            var ys = tup.Ys;
            if (xs.Count < 2) return;
            // determine y-range (optionally clamp to avoid extreme scaling)
            double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
            bool clampY = false;
            double clampVal = 100;
            // find clamp controls by scanning topPanel (safe check for null)
            if (topPanel != null)
            {
                foreach (Control c in topPanel.Controls)
                {
                    if (c is CheckBox cb && cb.Text == "Clamp Y") clampY = cb.Checked;
                    if (c is NumericUpDown nud && nud.Enabled && nud.Maximum == 100000 && nud.DecimalPlaces == 0) clampVal = (double)nud.Value;
                }
            }

            for (int i = 0; i < ys.Count; i++)
            {
                double y = ys[i];
                if (clampY)
                {
                    if (y > clampVal) y = clampVal;
                    if (y < -clampVal) y = -clampVal;
                }
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
            if (Math.Abs(yMax - yMin) < 1e-9)
            {
                yMax = yMin + 1;
                yMin = yMin - 1;
            }
            // Add small padding to data ranges so curve doesn't touch borders
            double xPad = (sampleProvider.XMax - sampleProvider.XMin) * 0.02;
            double yPad = (yMax - yMin) * 0.08;
            xMinPlot = sampleProvider.XMin - xPad;
            xMaxPlot = sampleProvider.XMax + xPad;
            yMinPlot = yMin - yPad;
            yMaxPlot = yMax + yPad;
            // Map samples to screen points
            PointF[] pts = new PointF[xs.Count];
            for (int i = 0; i < xs.Count; i++)
            {
                float sx = (float)(plotRect.Left + (xs[i] - xMinPlot) / (xMaxPlot - xMinPlot) * plotRect.Width);
                double yVal = ys[i];
                if (clampY)
                {
                    if (yVal > clampVal) yVal = clampVal;
                    if (yVal < -clampVal) yVal = -clampVal;
                }
                float sy = (float)(plotRect.Top + (1 - (yVal - yMinPlot) / (yMaxPlot - yMinPlot)) * plotRect.Height);
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
            // draw integer ticks along X and Y (no numeric labels)
            using (var tickPen = new Pen(Color.Black, 1))
            using (var labelFont = new Font("Segoe UI", 8))
            using (var labelBrush = new SolidBrush(Color.Black))
            {
                int tickLen = 6;
                // X ticks along bottom of plotRect for integer x within range
                int xStart = (int)Math.Ceiling(xMinPlot);
                int xEnd = (int)Math.Floor(xMaxPlot);
                for (int xi = xStart; xi <= xEnd; xi++)
                {
                    double xVal = xi;
                    float sx = (float)(plotRect.Left + (xVal - xMinPlot) / (xMaxPlot - xMinPlot) * plotRect.Width);
                    // draw tick only (no numeric label)
                    g.DrawLine(tickPen, sx, plotRect.Bottom, sx, plotRect.Bottom - tickLen);
                }

                // Y ticks along left of plotRect for integer y within range
                int yStart = (int)Math.Ceiling(yMinPlot);
                int yEnd = (int)Math.Floor(yMaxPlot);
                for (int yi = yStart; yi <= yEnd; yi++)
                {
                    double yVal = yi;
                    float sy = (float)(plotRect.Top + (1 - (yVal - yMinPlot) / (yMaxPlot - yMinPlot)) * plotRect.Height);
                    // draw tick only (no numeric label)
                    g.DrawLine(tickPen, plotRect.Left, sy, plotRect.Left + tickLen, sy);
                }

                // Axis labels 'x' and 'y'
                g.DrawString("x", labelFont, labelBrush, plotRect.Right - 12, plotRect.Bottom + 2);
                g.DrawString("y", labelFont, labelBrush, plotRect.Left - 18, plotRect.Top + 2);
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
    string xLabel = $"x: [{sampleProvider.XMin:0.###} .. {sampleProvider.XMax:0.###}], step={sampleProvider.Step}";
                string yLabel = $"y: [{yMin:0.###} .. {yMax:0.###}]";
          g.DrawString(xLabel, font, brush, rect.Left + 8, rect.Bottom - 20);
          g.DrawString(yLabel, font, brush, rect.Left + 8, rect.Bottom - 36);
            }
        }
    }
}
