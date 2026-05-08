using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Color_Finder;

public class MainForm : Form
{
    private Bitmap?          _image;
    private Rectangle        _imageSelection = Rectangle.Empty; // image pixel coords
    private Point            _dragStartImage;
    private bool             _isDragging;
    private List<ColorMatch> _matches = [];

    // zoom / pan
    private float  _zoom           = 1f;
    private PointF _panOffset      = PointF.Empty;
    private bool   _isPanning;
    private Point  _panStart;
    private PointF _panOffsetStart;

    private readonly BufferedPanel _imagePanel;
    private readonly BufferedPanel _magPanel;
    private readonly BufferedPanel _resultsPanel;
    private readonly NumericUpDown _numColorsSpinner;
    private readonly Button        _analyzeBtn;
    private readonly Label         _statusLabel;

    public MainForm()
    {
        Text        = "Color Finder for Artists";
        Size        = new Size(1180, 760);
        MinimumSize = new Size(860, 600);
        BackColor   = Color.FromArgb(28, 28, 28);
        Icon        = SystemIcons.Application;

        _imagePanel       = new BufferedPanel { Cursor = Cursors.Cross };
        _magPanel         = new BufferedPanel();
        _resultsPanel     = new BufferedPanel();
        _numColorsSpinner = new NumericUpDown();
        _analyzeBtn       = new StyledButton("Analyze Selection", Color.FromArgb(55, 160, 100));
        _statusLabel      = new Label();

        BuildLayout();
        WireEvents();
    }

    // ── Layout ──────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 52,
            BackColor = Color.FromArgb(40, 40, 40),
            Padding   = new Padding(0),
        };

        var openBtn = new StyledButton("Open Image", Color.FromArgb(55, 120, 185));
        openBtn.SetBounds(10, 10, 140, 32);
        openBtn.Click += OpenImage_Click;

        var colorsLabel = new Label
        {
            Text      = "Colors:",
            ForeColor = Color.FromArgb(190, 190, 190),
            AutoSize  = true,
            Location  = new Point(165, 20),
        };

        _numColorsSpinner.SetBounds(215, 14, 56, 26);
        _numColorsSpinner.Minimum     = 2;
        _numColorsSpinner.Maximum     = 6;
        _numColorsSpinner.Value       = 3;
        _numColorsSpinner.BackColor   = Color.FromArgb(58, 58, 58);
        _numColorsSpinner.ForeColor   = Color.White;
        _numColorsSpinner.BorderStyle = BorderStyle.FixedSingle;

        _analyzeBtn.SetBounds(286, 10, 154, 32);
        _analyzeBtn.Enabled = false;
        _analyzeBtn.Click  += Analyze_Click;

        _statusLabel.Text      = "Open an image to begin";
        _statusLabel.ForeColor = Color.FromArgb(150, 150, 150);
        _statusLabel.AutoSize  = true;
        _statusLabel.Location  = new Point(456, 20);

        toolbar.Controls.AddRange([openBtn, colorsLabel, _numColorsSpinner,
                                   _analyzeBtn, _statusLabel]);

        var mainSplit = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            SplitterDistance = 740,
            BackColor        = Color.FromArgb(28, 28, 28),
        };
        mainSplit.Panel1.Controls.Add(_imagePanel);
        _imagePanel.Dock = DockStyle.Fill;

        var rightSplit = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterDistance = 300,
            BackColor        = Color.FromArgb(28, 28, 28),
        };
        rightSplit.Panel1.Controls.Add(_magPanel);
        _magPanel.Dock = DockStyle.Fill;
        rightSplit.Panel2.Controls.Add(_resultsPanel);
        _resultsPanel.Dock = DockStyle.Fill;

        mainSplit.Panel2.Controls.Add(rightSplit);

        Controls.Add(mainSplit);
        Controls.Add(toolbar);
    }

    private void WireEvents()
    {
        _imagePanel.Paint      += ImagePanel_Paint;
        _imagePanel.MouseDown  += ImagePanel_MouseDown;
        _imagePanel.MouseMove  += ImagePanel_MouseMove;
        _imagePanel.MouseUp    += ImagePanel_MouseUp;
        _imagePanel.MouseWheel += ImagePanel_MouseWheel;
        _imagePanel.MouseEnter += (_, _) => _imagePanel.Focus();

        _magPanel.Paint     += MagPanel_Paint;
        _resultsPanel.Paint += ResultsPanel_Paint;

        _imagePanel.Resize   += (_, _) => _imagePanel.Invalidate();
        _magPanel.Resize     += (_, _) => _magPanel.Invalidate();
        _resultsPanel.Resize += (_, _) => _resultsPanel.Invalidate();
    }

    // ── Paint ────────────────────────────────────────────────────────────────

    private void ImagePanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(18, 18, 18));

        if (_image == null)
        {
            DrawHint(g, _imagePanel, "Open an image to get started");
            return;
        }

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(_image, ZoomedDrawRect());

        if (!_imageSelection.IsEmpty)
        {
            var screenSel = ImageToScreen(_imageSelection);
            using var overlay = new SolidBrush(Color.FromArgb(45, 255, 255, 255));
            g.FillRectangle(overlay, screenSel);
            using var pen = new Pen(Color.White, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, screenSel);
        }

        // zoom level badge
        if (_zoom > 1.01f)
        {
            var label = $"{_zoom:F1}×";
            using var f  = new Font("Segoe UI", 9f, FontStyle.Bold);
            var       sz = g.MeasureString(label, f);
            float     tx = _imagePanel.Width  - sz.Width  - 10;
            float     ty = 8;
            using var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            g.FillRectangle(bg, tx - 5, ty - 3, sz.Width + 10, sz.Height + 6);
            g.DrawString(label, f, Brushes.White, tx, ty);
        }
    }

    private void MagPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(18, 18, 18));

        if (_image == null || _imageSelection.IsEmpty)
        {
            DrawHint(g, _magPanel, "Select an area");
            return;
        }

        if (_imageSelection.Width <= 0 || _imageSelection.Height <= 0) return;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(_image,
            new Rectangle(0, 0, _magPanel.Width, _magPanel.Height),
            _imageSelection, GraphicsUnit.Pixel);

        using var hdr = new SolidBrush(Color.FromArgb(180, 18, 18, 18));
        g.FillRectangle(hdr, 0, 0, _magPanel.Width, 22);
        using var hdrFont = new Font("Segoe UI", 8f);
        g.DrawString($"Magnified  ·  {_imageSelection.Width} × {_imageSelection.Height} px",
            hdrFont, Brushes.Silver, 6, 4);
    }

    private void ResultsPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(32, 32, 32));

        using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        g.DrawString("Color Recipe", titleFont, Brushes.White, 12, 10);

        if (_matches.Count == 0)
        {
            using var hint = new Font("Segoe UI", 9f, FontStyle.Italic);
            g.DrawString("Select an area and click Analyze", hint,
                new SolidBrush(Color.FromArgb(80, 80, 80)), 12, 36);
            return;
        }

        int y      = 38;
        int barMax = Math.Max(60, _resultsPanel.Width - 220);

        using var nameFont = new Font("Segoe UI", 9.5f);
        using var pctFont  = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var catFont  = new Font("Segoe UI", 7.5f, FontStyle.Italic);

        foreach (var m in _matches)
        {
            if (y + 42 > _resultsPanel.Height - 24) break;

            using var swatchBrush = new SolidBrush(m.DisplayColor);
            g.FillRectangle(swatchBrush, 12, y + 3, 24, 24);
            g.DrawRectangle(Pens.DimGray, 12, y + 3, 24, 24);

            int barW = (int)(barMax * m.Percentage / 100.0);
            using var barFill = new SolidBrush(Color.FromArgb(55, m.DisplayColor));
            g.FillRectangle(barFill, 46, y + 9, barW, 12);

            g.DrawString(m.Pigment.Name, nameFont, Brushes.White, 46, y + 3);

            g.DrawString($"({m.Pigment.Category})", catFont,
                new SolidBrush(Color.FromArgb(110, 110, 110)), 48, y + 22);

            var pctStr  = $"{m.Percentage}%";
            var pctSize = g.MeasureString(pctStr, pctFont);
            g.DrawString(pctStr, pctFont,
                new SolidBrush(Color.FromArgb(160, 210, 255)),
                _resultsPanel.Width - pctSize.Width - 10, y + 3);

            y += 42;
        }

        using var footFont = new Font("Segoe UI", 7.5f, FontStyle.Italic);
        g.DrawString("Mix these pigments in the percentages shown to match the selected area.",
            footFont, new SolidBrush(Color.FromArgb(65, 65, 65)),
            12, _resultsPanel.Height - 18);
    }

    // ── Mouse ────────────────────────────────────────────────────────────────

    private void ImagePanel_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (_image == null) return;

        float factor  = e.Delta > 0 ? 1.25f : 1f / 1.25f;
        float newZoom = Math.Clamp(_zoom * factor, 1f, 16f);
        if (Math.Abs(newZoom - _zoom) < 0.001f) return;

        var dr = ZoomedDrawRect();
        if (dr.Width <= 0) return;

        // image fractions under the cursor — preserved after zoom
        float fracX = (e.X - dr.X) / dr.Width;
        float fracY = (e.Y - dr.Y) / dr.Height;

        _zoom = newZoom;

        // recalculate pan so the same image point stays under the cursor
        var (fitW, fitH) = FitSize();
        float zW = fitW * _zoom, zH = fitH * _zoom;
        float pw = _imagePanel.Width, ph = _imagePanel.Height;
        _panOffset.X = e.X - pw / 2f - zW * (fracX - 0.5f);
        _panOffset.Y = e.Y - ph / 2f - zH * (fracY - 0.5f);

        ClampPan();
        _imagePanel.Invalidate();
        _magPanel.Invalidate();
    }

    private void ImagePanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_image == null) return;

        if (e.Button == MouseButtons.Middle)
        {
            _isPanning       = true;
            _panStart        = e.Location;
            _panOffsetStart  = _panOffset;
            _imagePanel.Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button != MouseButtons.Left) return;
        _dragStartImage = ScreenToImagePoint(e.Location);
        _isDragging     = true;
        _imageSelection = Rectangle.Empty;
        _matches.Clear();
        _resultsPanel.Invalidate();
    }

    private void ImagePanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            _panOffset = new PointF(
                _panOffsetStart.X + e.X - _panStart.X,
                _panOffsetStart.Y + e.Y - _panStart.Y);
            ClampPan();
            _imagePanel.Invalidate();
            _magPanel.Invalidate();
            return;
        }

        if (!_isDragging) return;
        var cur = ScreenToImagePoint(e.Location);
        _imageSelection = NormalizeRect(_dragStartImage, cur);
        _imagePanel.Invalidate();
        _magPanel.Invalidate();
    }

    private void ImagePanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isPanning && e.Button == MouseButtons.Middle)
        {
            _isPanning         = false;
            _imagePanel.Cursor = Cursors.Cross;
            return;
        }

        if (!_isDragging) return;
        _isDragging = false;

        bool valid = _imageSelection.Width > 1 && _imageSelection.Height > 1;
        _analyzeBtn.Enabled = valid;
        _statusLabel.Text   = valid
            ? "Click 'Analyze Selection' to get the color recipe"
            : "Selection too small — drag a larger area";

        if (!valid) { _imageSelection = Rectangle.Empty; _imagePanel.Invalidate(); }
        _magPanel.Invalidate();
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void OpenImage_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*",
            Title  = "Open Image",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        Bitmap? loaded;
        try   { loaded = new Bitmap(dlg.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open image:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _image?.Dispose();
        _image          = loaded;
        _imageSelection = Rectangle.Empty;
        _zoom           = 1f;
        _panOffset      = PointF.Empty;
        _matches.Clear();
        _analyzeBtn.Enabled = false;
        _statusLabel.Text   = "Scroll to zoom  ·  Middle-drag to pan  ·  Left-drag to select";

        _imagePanel.Invalidate();
        _magPanel.Invalidate();
        _resultsPanel.Invalidate();
    }

    private void Analyze_Click(object? sender, EventArgs e)
    {
        if (_image == null || _imageSelection.IsEmpty) return;

        if (_imageSelection.Width < 2 || _imageSelection.Height < 2)
        {
            _statusLabel.Text = "Selection maps to too few pixels — select a larger area";
            return;
        }

        _statusLabel.Text   = "Analyzing…";
        _analyzeBtn.Enabled = false;
        Cursor = Cursors.WaitCursor;
        Application.DoEvents();

        try
        {
            _matches = ColorAnalyzer.Analyze(_image, _imageSelection, (int)_numColorsSpinner.Value);
            _resultsPanel.Invalidate();
            _statusLabel.Text = _matches.Count > 0
                ? $"Found {_matches.Count} dominant pigment{(_matches.Count == 1 ? "" : "s")}"
                : "No colors found";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Analysis failed";
        }
        finally
        {
            _analyzeBtn.Enabled = !_imageSelection.IsEmpty;
            Cursor = Cursors.Default;
        }
    }

    // ── Geometry helpers ─────────────────────────────────────────────────────

    // Returns the natural (zoom=1) fit size of the image inside the panel.
    private (float W, float H) FitSize()
    {
        if (_image == null) return (0, 0);
        float aspect = _image.Width / (float)_image.Height;
        float pw = _imagePanel.Width, ph = _imagePanel.Height;
        if (aspect > pw / ph) return (pw, pw / aspect);
        return (ph * aspect, ph);
    }

    private RectangleF ZoomedDrawRect()
    {
        var (fitW, fitH) = FitSize();
        if (fitW == 0) return RectangleF.Empty;
        float zW = fitW * _zoom, zH = fitH * _zoom;
        float cx = _imagePanel.Width  / 2f + _panOffset.X;
        float cy = _imagePanel.Height / 2f + _panOffset.Y;
        return new RectangleF(cx - zW / 2f, cy - zH / 2f, zW, zH);
    }

    private void ClampPan()
    {
        if (_image == null || _zoom <= 1f) { _panOffset = PointF.Empty; return; }
        var (fitW, fitH) = FitSize();
        float maxX = Math.Max(0f, (fitW * _zoom - _imagePanel.Width)  / 2f);
        float maxY = Math.Max(0f, (fitH * _zoom - _imagePanel.Height) / 2f);
        _panOffset.X = Math.Clamp(_panOffset.X, -maxX, maxX);
        _panOffset.Y = Math.Clamp(_panOffset.Y, -maxY, maxY);
    }

    private Point ScreenToImagePoint(Point pt)
    {
        if (_image == null) return Point.Empty;
        var dr = ZoomedDrawRect();
        if (dr.Width <= 0 || dr.Height <= 0) return Point.Empty;
        float sx = _image.Width  / dr.Width;
        float sy = _image.Height / dr.Height;
        return new Point(
            Math.Clamp((int)((pt.X - dr.X) * sx), 0, _image.Width  - 1),
            Math.Clamp((int)((pt.Y - dr.Y) * sy), 0, _image.Height - 1));
    }

    private Rectangle ScreenToImage(Rectangle screen)
    {
        if (_image == null) return Rectangle.Empty;
        var dr = ZoomedDrawRect();
        if (dr.Width <= 0 || dr.Height <= 0) return Rectangle.Empty;
        float sx = _image.Width  / dr.Width;
        float sy = _image.Height / dr.Height;
        return Rectangle.FromLTRB(
            Math.Clamp((int)((screen.Left   - dr.X) * sx), 0, _image.Width),
            Math.Clamp((int)((screen.Top    - dr.Y) * sy), 0, _image.Height),
            Math.Clamp((int)((screen.Right  - dr.X) * sx), 0, _image.Width),
            Math.Clamp((int)((screen.Bottom - dr.Y) * sy), 0, _image.Height));
    }

    private Rectangle ImageToScreen(Rectangle img)
    {
        if (_image == null) return Rectangle.Empty;
        var dr = ZoomedDrawRect();
        if (_image.Width == 0 || _image.Height == 0) return Rectangle.Empty;
        float sx = dr.Width  / _image.Width;
        float sy = dr.Height / _image.Height;
        return Rectangle.FromLTRB(
            (int)(dr.X + img.Left   * sx),
            (int)(dr.Y + img.Top    * sy),
            (int)(dr.X + img.Right  * sx),
            (int)(dr.Y + img.Bottom * sy));
    }

    private static Rectangle NormalizeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    private static void DrawHint(Graphics g, Control ctrl, string text)
    {
        using var f  = new Font("Segoe UI", 10f, FontStyle.Italic);
        var       sz = g.MeasureString(text, f);
        g.DrawString(text, f, new SolidBrush(Color.FromArgb(60, 60, 60)),
            (ctrl.Width - sz.Width) / 2f, (ctrl.Height - sz.Height) / 2f);
    }

    // ── Styled button ────────────────────────────────────────────────────────

    private sealed class StyledButton : Button
    {
        private readonly Color _base;
        private bool _hovered;
        private bool _pressed;

        public StyledButton(string text, Color baseColor)
        {
            _base     = baseColor;
            Text      = text;
            ForeColor = Color.White;
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            Cursor    = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnEnabledChanged(EventArgs e)      { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e)          { base.OnMouseEnter(e); _hovered = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e)          { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)      { base.OnMouseDown(e);  _pressed = true;  Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)        { base.OnMouseUp(e);    _pressed = false; Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e) { } // suppressed — OnPaint handles everything

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Fill the whole control area with the parent background first so
            // anti-aliased rounded corners don't bleed against stale pixels.
            g.Clear(Parent?.BackColor ?? Color.FromArgb(40, 40, 40));

            var r = new Rectangle(1, 1, Width - 3, Height - 3);

            Color fill = Enabled ? _base : Desaturate(_base);
            Color top, bot;
            if (_pressed)      { top = Darken(fill, 0.18f);  bot = Lighten(fill, 0.06f); }
            else if (_hovered) { top = Lighten(fill, 0.28f); bot = Lighten(fill, 0.10f); }
            else               { top = Lighten(fill, 0.20f); bot = Darken(fill, 0.15f);  }

            using var path = RoundedPath(r, 8);
            using (var grad = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height), top, bot, LinearGradientMode.Vertical))
                g.FillPath(grad, path);

            if (Enabled && !_pressed && r.Height > 10)
            {
                var shine = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height / 2 - 2);
                if (shine.Width > 4 && shine.Height > 2)
                {
                    using var shineGrad = new LinearGradientBrush(shine,
                        Color.FromArgb(65, 255, 255, 255),
                        Color.FromArgb(5,  255, 255, 255),
                        LinearGradientMode.Vertical);
                    g.SetClip(path);
                    g.FillRectangle(shineGrad, shine);
                    g.ResetClip();
                }
            }

            using var border = new Pen(Color.FromArgb(90, 0, 0, 0), 1f);
            g.DrawPath(border, path);

            var textColor = Enabled ? Color.White : Color.FromArgb(120, 120, 120);
            using var tb = new SolidBrush(textColor);
            using var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(Text, Font, tb, new RectangleF(0, 0, Width, Height), sf);
        }

        private static GraphicsPath RoundedPath(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X,             r.Y,              rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad*2, r.Y,              rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad*2, r.Bottom - rad*2, rad * 2, rad * 2,   0, 90);
            p.AddArc(r.X,             r.Bottom - rad*2, rad * 2, rad * 2,  90, 90);
            p.CloseFigure();
            return p;
        }

        private static Color Lighten(Color c, float f) => Color.FromArgb(c.A,
            Math.Min(255, (int)(c.R + (255 - c.R) * f)),
            Math.Min(255, (int)(c.G + (255 - c.G) * f)),
            Math.Min(255, (int)(c.B + (255 - c.B) * f)));

        private static Color Darken(Color c, float f) => Color.FromArgb(c.A,
            Math.Max(0, (int)(c.R * (1 - f))),
            Math.Max(0, (int)(c.G * (1 - f))),
            Math.Max(0, (int)(c.B * (1 - f))));

        private static Color Desaturate(Color c)
        {
            int lum = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
            return Color.FromArgb(c.A, (lum + c.R) / 2, (lum + c.G) / 2, (lum + c.B) / 2);
        }
    }

    // ── Inner helper ─────────────────────────────────────────────────────────

    private class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.Selectable, true);
            TabStop = false;
        }
    }
}
