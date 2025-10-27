using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GraphPlot
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
#if NET6_0_OR_GREATER
            ApplicationConfiguration.Initialize();
#else
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#endif
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
    // Controls
    private Panel? topPanel;
    private Panel? plotPanel;
    private NumericUpDown? nudXmin;
    private NumericUpDown? nudXmax;
    private NumericUpDown? nudDx;
    private Button? btnReplot;
    private Button? btnSave;

        // Plot padding inside plotPanel
        private readonly Padding plotPadding = new Padding(50, 30, 30, 50);

        public MainForm()
        {
            Text = "Function Plotter";
            MinimumSize = new Size(600, 420);
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Top panel for controls
            topPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };

            var lblXmin = new Label { Text = "x_min:", AutoSize = true, Location = new Point(8, 14) };
            nudXmin = new NumericUpDown { DecimalPlaces = 3, Increment = 0.1M, Minimum = -1000, Maximum = 1000, Value = 0.1M, Width = 80, Location = new Point(56, 10) };

            var lblXmax = new Label { Text = "x_max:", AutoSize = true, Location = new Point(146, 14) };
            nudXmax = new NumericUpDown { DecimalPlaces = 3, Increment = 0.1M, Minimum = -1000, Maximum = 1000, Value = 1.2M, Width = 80, Location = new Point(196, 10) };

            var lblDx = new Label { Text = "Δx:", AutoSize = true, Location = new Point(286, 14) };
            nudDx = new NumericUpDown { DecimalPlaces = 3, Increment = 0.1M, Minimum = 0.0001M, Maximum = 1000, Value = 0.1M, Width = 80, Location = new Point(320, 10) };

            btnReplot = new Button { Text = "Replot", Location = new Point(420, 8), Width = 80 };
            btnReplot.Click += (s, e) => plotPanel!.Invalidate();

            btnSave = new Button { Text = "Save PNG", Location = new Point(508, 8), Width = 90 };
            btnSave.Click += BtnSave_Click;

            topPanel.Controls.AddRange(new Control[] { lblXmin, nudXmin, lblXmax, nudXmax, lblDx, nudDx, btnReplot, btnSave });

            // Plot panel
            plotPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            plotPanel.Paint += PlotPanel_Paint;
            plotPanel.Resize += (s, e) => plotPanel.Invalidate();

            Controls.Add(plotPanel);
            Controls.Add(topPanel);
        }

    private void BtnSave_Click(object? sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "PNG Image|*.png";
                dlg.DefaultExt = "png";
                dlg.FileName = "plot.png";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    using (var bmp = new Bitmap(plotPanel!.Width, plotPanel!.Height))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(plotPanel!.BackColor);
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        DrawPlot(g, new Rectangle(0, 0, plotPanel.Width, plotPanel.Height));
                        bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    MessageBox.Show("Saved image to: " + dlg.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PlotPanel_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (plotPanel != null) DrawPlot(e.Graphics, plotPanel.ClientRectangle);
        }

        private void DrawPlot(Graphics g, Rectangle clientRect)
        {
            // Read and validate inputs
            double xmin = (double)nudXmin!.Value;
            double xmax = (double)nudXmax!.Value;
            double dx = (double)nudDx!.Value;

            if (dx <= 0 || xmin >= xmax)
            {
                using (var f = new Font("Consolas", 10))
                using (var brush = Brushes.Black)
                {
                    g.Clear(plotPanel!.BackColor);
                    g.DrawString("Invalid input: ensure x_min < x_max and Δx > 0.", f, brush, 10, 10);
                }
                return;
            }

            var plotRect = new Rectangle(plotPadding.Left, plotPadding.Top, Math.Max(10, clientRect.Width - plotPadding.Left - plotPadding.Right), Math.Max(10, clientRect.Height - plotPadding.Top - plotPadding.Bottom));

            // background
            using (var bg = new SolidBrush(plotPanel!.BackColor)) g.FillRectangle(bg, clientRect);
            using (var white = new SolidBrush(Color.White)) g.FillRectangle(white, plotRect);

            // Compute points
            var xs = new System.Collections.Generic.List<double>();
            var ys = new System.Collections.Generic.List<double>();
            for (double x = xmin; x <= xmax + 1e-12; x = Math.Round(x + dx, 12))
            {
                xs.Add(x);
                ys.Add(Eval(x));
            }

            double ymin = double.PositiveInfinity, ymax = double.NegativeInfinity;
            foreach (var y in ys)
            {
                if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                ymin = Math.Min(ymin, y);
                ymax = Math.Max(ymax, y);
            }
            if (ymin == double.PositiveInfinity)
            {
                using (var f = new Font("Consolas", 10))
                using (var brush = Brushes.Black)
                {
                    g.DrawString("No valid y values to plot.", f, brush, plotPadding.Left, 10);
                }
                return;
            }
            if (Math.Abs(ymax - ymin) < 1e-9)
            {
                ymax += 1.0;
                ymin -= 1.0;
            }

            // Map function
            PointF Map(double x, double y)
            {
                float px = (float)(plotRect.Left + (x - xmin) / (xmax - xmin) * plotRect.Width);
                float py = (float)(plotRect.Bottom - (y - ymin) / (ymax - ymin) * plotRect.Height);
                return new PointF(px, py);
            }

            // Grid
            using (var penGrid = new Pen(Color.FromArgb(230, 230, 230)))
            {
                for (double x = xmin; x <= xmax + 1e-12; x = Math.Round(x + dx, 12))
                {
                    var a = Map(x, ymin);
                    var b = Map(x, ymax);
                    g.DrawLine(penGrid, a, b);
                }
                int hTicks = 5;
                for (int i = 0; i <= hTicks; i++)
                {
                    double y = ymin + (ymax - ymin) * i / hTicks;
                    var a = Map(xmin, y);
                    var b = Map(xmax, y);
                    g.DrawLine(penGrid, a, b);
                }
            }

            // Axes
            using (var penAxis = new Pen(Color.Black, 1.2f))
            {
                if (ymin <= 0 && ymax >= 0)
                {
                    var p1 = Map(xmin, 0); var p2 = Map(xmax, 0); g.DrawLine(penAxis, p1, p2);
                }
                else
                {
                    g.DrawLine(penAxis, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);
                }
                if (xmin <= 0 && xmax >= 0)
                {
                    var p1 = Map(0, ymin); var p2 = Map(0, ymax); g.DrawLine(penAxis, p1, p2);
                }
                else
                {
                    g.DrawLine(penAxis, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
                }
            }

            // Polyline
            var points = new System.Collections.Generic.List<PointF>();
            for (int i = 0; i < xs.Count; i++)
            {
                var y = ys[i];
                if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                points.Add(Map(xs[i], y));
            }
            if (points.Count >= 2)
            {
                using (var pen = new Pen(Color.DodgerBlue, 2f)) g.DrawLines(pen, points.ToArray());
                foreach (var p in points) g.FillEllipse(Brushes.Red, p.X - 3, p.Y - 3, 6, 6);
            }

            // Tick labels
            using (var font = new Font("Consolas", 9))
            using (var brush = Brushes.Black)
            {
                for (double x = xmin; x <= xmax + 1e-12; x = Math.Round(x + dx, 12))
                {
                    var p = Map(x, ymin);
                    string s = x.ToString("0.###");
                    var sz = g.MeasureString(s, font);
                    g.DrawString(s, font, brush, p.X - sz.Width / 2, plotRect.Bottom + 4);
                }
                int yTicks = 4;
                for (int i = 0; i <= yTicks; i++)
                {
                    double y = ymin + (ymax - ymin) * i / yTicks;
                    var p = Map(xmin, y);
                    string s = y.ToString("0.###");
                    var sz = g.MeasureString(s, font);
                    g.DrawString(s, font, brush, plotRect.Left - sz.Width - 6, p.Y - sz.Height / 2);
                }
            }

            // Header
            using (var f = new Font("Consolas", 10))
            {
                g.DrawString("y = tan(0.5*x) / (x^3 + 7.5)", f, Brushes.Black, plotPadding.Left, 6);
                g.DrawString($"x ∈ [{xmin}, {xmax}], Δx = {dx}", f, Brushes.Black, plotPadding.Left + 300, 6);
            }
        }

        // Function evaluation
        private double Eval(double x)
        {
            // y = tan(0.5*x) / (x^3 + 7.5)
            double denom = Math.Pow(x, 3) + 7.5;
            if (denom == 0) return double.NaN;
            return Math.Tan(0.5 * x) / denom;
        }
    }
}

