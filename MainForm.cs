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

    // mag-panel sub-selection
    private bool      _isMagDragging;
    private Point     _magDragStart;
    private Rectangle _magSubSelection = Rectangle.Empty; // image pixel coords, live during drag

    // zoom / pan
    private float  _zoom           = 1f;
    private PointF _panOffset      = PointF.Empty;
    private bool   _isPanning;
    private Point  _panStart;
    private PointF _panOffsetStart;

    private const int HoverRadiusPx  = 14;  // radius used for click-sample area
    private const int SwatchGripH    = 8;   // height of the drag-to-resize grip strip
    private const int SwatchMinH     = 80;
    private const int SwatchMaxH     = 420;
    private Point     _mouseDownScreenPt;
    private bool      _isResizingSwatches;
    private int       _swatchResizeStartScreenY;
    private int       _swatchResizeStartH;

    private readonly List<List<ColorMatch>> _swatches = [];

    private readonly BufferedPanel _imagePanel;
    private readonly BufferedPanel _magPanel;
    private readonly BufferedPanel _resultsPanel;
    private readonly BufferedPanel _swatchesPanel;
    private readonly NumericUpDown _numColorsSpinner;
    private readonly Button        _analyzeBtn;
    private readonly Button        _addSwatchBtn;
    private readonly Label         _statusLabel;

    public MainForm()
    {
        Text        = "Color Finder for Artists";
        Size        = new Size(1180, 760);
        MinimumSize = new Size(860, 600);
        BackColor   = Color.FromArgb(10, 10, 10);
        Icon        = SystemIcons.Application;

        _imagePanel       = new BufferedPanel { Cursor = Cursors.Cross };
        _magPanel         = new BufferedPanel { Cursor = Cursors.Cross };
        _resultsPanel     = new BufferedPanel();
        _swatchesPanel    = new BufferedPanel();
        _numColorsSpinner = new NumericUpDown();
        _analyzeBtn       = new StyledButton("Analyze Selection", Color.FromArgb(55, 160, 100));
        _addSwatchBtn     = new StyledButton("Save Color Swatch", Color.FromArgb(130, 80, 160));
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
            Height    = 58,
            BackColor = Color.FromArgb(18, 18, 18),
            Padding   = new Padding(0),
        };

        var titleLabel = new Label
        {
            Text      = "Color Finder",
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize  = true,
            Location  = new Point(14, 19),
        };

        var openBtn = new StyledButton("Open Image", Color.FromArgb(45, 110, 175));
        openBtn.SetBounds(152, 13, 130, 32);
        openBtn.Click += OpenImage_Click;

        var colorsLabel = new Label
        {
            Text      = "Colors:",
            ForeColor = Color.FromArgb(150, 150, 150),
            AutoSize  = true,
            Location  = new Point(296, 22),
        };

        _numColorsSpinner.SetBounds(344, 16, 52, 26);
        _numColorsSpinner.Minimum     = 2;
        _numColorsSpinner.Maximum     = 6;
        _numColorsSpinner.Value       = 3;
        _numColorsSpinner.BackColor   = Color.FromArgb(34, 34, 34);
        _numColorsSpinner.ForeColor   = Color.White;
        _numColorsSpinner.BorderStyle = BorderStyle.FixedSingle;

        _analyzeBtn.SetBounds(410, 13, 154, 32);
        _analyzeBtn.Enabled = false;
        _analyzeBtn.Click  += Analyze_Click;

        _addSwatchBtn.SetBounds(574, 13, 154, 32);
        _addSwatchBtn.Enabled = false;
        _addSwatchBtn.Click  += AddSwatch_Click;

        _statusLabel.Text      = "Open an image to begin";
        _statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
        _statusLabel.AutoSize  = true;
        _statusLabel.Location  = new Point(742, 22);

        toolbar.Controls.AddRange([titleLabel, openBtn, colorsLabel, _numColorsSpinner,
                                   _analyzeBtn, _addSwatchBtn, _statusLabel]);

        var mainSplit = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            SplitterDistance = 740,
            BackColor        = Color.FromArgb(10, 10, 10),
        };
        mainSplit.Panel1.Controls.Add(_imagePanel);
        _imagePanel.Dock = DockStyle.Fill;

        var rightSplit = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterDistance = 300,
            BackColor        = Color.FromArgb(10, 10, 10),
        };
        rightSplit.Panel1.Controls.Add(_magPanel);
        _magPanel.Dock = DockStyle.Fill;
        rightSplit.Panel2.Controls.Add(_resultsPanel);
        _resultsPanel.Dock = DockStyle.Fill;

        mainSplit.Panel2.Controls.Add(rightSplit);

        _swatchesPanel.Dock   = DockStyle.Bottom;
        _swatchesPanel.Height = 165;

        Controls.Add(mainSplit);
        Controls.Add(_swatchesPanel);
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
        _magPanel.MouseDown += MagPanel_MouseDown;
        _magPanel.MouseMove += MagPanel_MouseMove;
        _magPanel.MouseUp   += MagPanel_MouseUp;
        _resultsPanel.Paint += ResultsPanel_Paint;

        _swatchesPanel.Paint      += SwatchesPanel_Paint;
        _swatchesPanel.MouseDown  += SwatchesPanel_MouseDown;
        _swatchesPanel.MouseMove  += SwatchesPanel_MouseMove;
        _swatchesPanel.MouseUp    += (_, _) => { _isResizingSwatches = false; _swatchesPanel.Cursor = Cursors.Default; };
        _swatchesPanel.MouseLeave += (_, _) => { if (!_isResizingSwatches) _swatchesPanel.Cursor = Cursors.Default; };
        _swatchesPanel.MouseClick += SwatchesPanel_MouseClick;

        _imagePanel.Resize    += (_, _) => _imagePanel.Invalidate();
        _magPanel.Resize      += (_, _) => _magPanel.Invalidate();
        _resultsPanel.Resize  += (_, _) => _resultsPanel.Invalidate();
        _swatchesPanel.Resize += (_, _) => _swatchesPanel.Invalidate();
    }

    // ── Paint ────────────────────────────────────────────────────────────────

    private void ImagePanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(12, 12, 12));

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
            using var overlay = new SolidBrush(Color.FromArgb(50, 255, 255, 255));
            g.FillRectangle(overlay, screenSel);
            using var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, screenSel);
        }


        // zoom level badge
        if (_zoom > 1.01f)
        {
            var label = $"{_zoom:F1}×";
            using var f  = new Font("Segoe UI", 9f, FontStyle.Bold);
            var       sz = g.MeasureString(label, f);
            float     tx = _imagePanel.Width  - sz.Width  - 14;
            float     ty = 10;
            var badgeR = new Rectangle((int)(tx - 7), (int)(ty - 5), (int)(sz.Width + 14), (int)(sz.Height + 10));
            using var bgPath = RoundedRect(badgeR, 5);
            using (var bg = new SolidBrush(Color.FromArgb(195, 0, 0, 0)))
                g.FillPath(bg, bgPath);
            g.DrawString(label, f, Brushes.White, tx, ty);
        }
    }

    private void MagPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(12, 12, 12));

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

        // sub-selection overlay drawn while dragging in mag panel
        if (!_magSubSelection.IsEmpty)
        {
            var magRect = ImageToMag(_magSubSelection);
            using var overlay = new SolidBrush(Color.FromArgb(45, 255, 255, 255));
            g.FillRectangle(overlay, magRect);
            using var selPen = new Pen(Color.White, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(selPen, magRect);
        }

        using var hdr = new SolidBrush(Color.FromArgb(210, 12, 12, 12));
        g.FillRectangle(hdr, 0, 0, _magPanel.Width, 24);
        using var hdrFont = new Font("Segoe UI", 8f);
        g.DrawString($"Magnified  ·  {_imageSelection.Width}×{_imageSelection.Height} px  ·  drag to refine",
            hdrFont, new SolidBrush(Color.FromArgb(125, 125, 125)), 8, 5);
    }

    private void ResultsPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(18, 18, 18));

        using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        g.DrawString("Color Recipe", titleFont, Brushes.White, 14, 12);
        // green accent line under title
        g.FillRectangle(new SolidBrush(Color.FromArgb(55, 160, 100)), 14, 32, 58, 2);

        if (_matches.Count == 0)
        {
            using var hint = new Font("Segoe UI", 9f, FontStyle.Italic);
            g.DrawString("Select an area to get the color recipe", hint,
                new SolidBrush(Color.FromArgb(56, 56, 56)), 14, 46);
            return;
        }

        int y      = 44;
        int margin = 10;
        int cardW  = _resultsPanel.Width - margin * 2;

        using var nameFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var catFont  = new Font("Segoe UI", 8f);
        using var pctFont  = new Font("Segoe UI", 11f, FontStyle.Bold);

        foreach (var m in _matches)
        {
            const int cardH = 52;
            if (y + cardH + 6 > _resultsPanel.Height - 22) break;

            // card
            var cardRect = new Rectangle(margin, y, cardW, cardH);
            using var cardPath = RoundedRect(cardRect, 6);
            using (var cardBrush = new SolidBrush(Color.FromArgb(28, 28, 28)))
                g.FillPath(cardBrush, cardPath);
            using (var borderPen = new Pen(Color.FromArgb(42, 42, 42), 1f))
                g.DrawPath(borderPen, cardPath);

            // swatch
            int swatchSize = 34;
            int sx = margin + 10;
            int sy = y + (cardH - swatchSize) / 2;
            using var swatchPath = RoundedRect(new Rectangle(sx, sy, swatchSize, swatchSize), 5);
            using (var swatchBrush = new SolidBrush(m.DisplayColor))
                g.FillPath(swatchBrush, swatchPath);
            using (var swatchBorder = new Pen(Color.FromArgb(55, 255, 255, 255), 1f))
                g.DrawPath(swatchBorder, swatchPath);

            // calculate percentage position first so name can be clipped to it
            int textX = sx + swatchSize + 10;
            var pctStr  = $"{m.Percentage}%";
            var pctSize = g.MeasureString(pctStr, pctFont);
            float pctX  = margin + cardW - pctSize.Width - 12;
            float pctY  = y + (cardH - pctSize.Height) / 2f;

            // name clipped so it never overlaps the percentage
            float nameMaxW = Math.Max(20f, pctX - textX - 6);
            using var nameSf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(m.Pigment.Name, nameFont, Brushes.White,
                new RectangleF(textX, y + 7, nameMaxW, 20), nameSf);
            g.DrawString(m.Pigment.Category, catFont,
                new SolidBrush(Color.FromArgb(90, 90, 90)), textX, y + 27);

            g.DrawString(pctStr, pctFont,
                new SolidBrush(Color.FromArgb(215, 180, 75)), pctX, pctY);

            // thin progress bar
            int barX    = textX;
            int barY    = y + cardH - 9;
            int barMaxW = Math.Max(10, (int)(pctX - textX - 8));
            int barW    = Math.Max(0, (int)(barMaxW * m.Percentage / 100.0));
            g.FillRectangle(new SolidBrush(Color.FromArgb(38, 38, 38)), barX, barY, barMaxW, 3);
            g.FillRectangle(new SolidBrush(Color.FromArgb(160, m.DisplayColor)), barX, barY, barW, 3);

            y += cardH + 6;
        }

        using var footFont = new Font("Segoe UI", 7.5f, FontStyle.Italic);
        g.DrawString("Mix these pigments in the percentages shown",
            footFont, new SolidBrush(Color.FromArgb(48, 48, 48)),
            14, _resultsPanel.Height - 18);
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

        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
        {
            _isPanning       = true;
            _panStart        = e.Location;
            _panOffsetStart  = _panOffset;
            _imagePanel.Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button != MouseButtons.Left) return;
        _mouseDownScreenPt = e.Location;
        _dragStartImage    = ScreenToImagePoint(e.Location);
        _isDragging        = true;
        _imageSelection    = Rectangle.Empty;
        _magSubSelection   = Rectangle.Empty;
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

        if (_isDragging)
        {
            var cur = ScreenToImagePoint(e.Location);
            _imageSelection = NormalizeRect(_dragStartImage, cur);
            _imagePanel.Invalidate();
            _magPanel.Invalidate();
            return;
        }

    }

    private void ImagePanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isPanning && (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right))
        {
            _isPanning         = false;
            _imagePanel.Cursor = Cursors.Cross;
            return;
        }

        if (!_isDragging) return;
        _isDragging = false;

        // detect click: mouse barely moved from the down point
        int dx = e.X - _mouseDownScreenPt.X;
        int dy = e.Y - _mouseDownScreenPt.Y;
        bool isClick = dx * dx + dy * dy < 25; // within 5px radius

        if (isClick)
        {
            // build a circle-shaped selection in image coords and analyze immediately
            _imageSelection  = CircleSelectionAtScreen(_mouseDownScreenPt);
            _magSubSelection = Rectangle.Empty;
            _imagePanel.Invalidate();
            _magPanel.Invalidate();
            _analyzeBtn.Enabled = !_imageSelection.IsEmpty;
            if (!_imageSelection.IsEmpty)
                RunAnalysis(_imageSelection);
            return;
        }

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
        _statusLabel.Text   = "Click to sample  ·  Drag to select area  ·  Scroll to zoom  ·  Middle-drag to pan";

        _imagePanel.Invalidate();
        _magPanel.Invalidate();
        _resultsPanel.Invalidate();
    }

    private void Analyze_Click(object? sender, EventArgs e)
    {
        if (_image == null || _imageSelection.IsEmpty) return;
        RunAnalysis(!_magSubSelection.IsEmpty ? _magSubSelection : _imageSelection);
    }

    private void RunAnalysis(Rectangle selection)
    {
        if (_image == null) return;
        if (selection.Width < 2 || selection.Height < 2)
        {
            _statusLabel.Text = "Selection too small — drag a larger area";
            return;
        }

        _statusLabel.Text   = "Analyzing…";
        _analyzeBtn.Enabled = false;
        Cursor = Cursors.WaitCursor;
        Application.DoEvents();

        try
        {
            _matches = ColorAnalyzer.Analyze(_image, selection, (int)_numColorsSpinner.Value);
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
            _analyzeBtn.Enabled  = !_imageSelection.IsEmpty;
            _addSwatchBtn.Enabled = _matches.Count > 0 && _swatches.Count < 5;
            Cursor = Cursors.Default;
        }
    }

    // ── Mag panel mouse (sub-selection) ─────────────────────────────────────

    private void MagPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_image == null || _imageSelection.IsEmpty) return;
        if (e.Button != MouseButtons.Left) return;
        _magDragStart    = MagToImagePoint(e.Location);
        _isMagDragging   = true;
        _magSubSelection = Rectangle.Empty;
    }

    private void MagPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isMagDragging) return;
        _magSubSelection = NormalizeRect(_magDragStart, MagToImagePoint(e.Location));
        _magPanel.Invalidate();
    }

    private void MagPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isMagDragging) return;
        _isMagDragging = false;

        if (_magSubSelection.Width > 1 && _magSubSelection.Height > 1)
        {
            RunAnalysis(_magSubSelection);
        }
        else
        {
            _magSubSelection = Rectangle.Empty;
        }
        _magPanel.Invalidate();
    }

    // ── Swatches ─────────────────────────────────────────────────────────────

    private void AddSwatch_Click(object? sender, EventArgs e)
    {
        if (_matches.Count == 0 || _swatches.Count >= 5) return;
        _swatches.Add([.. _matches]);
        _addSwatchBtn.Enabled = _swatches.Count < 5;
        _swatchesPanel.Invalidate();
    }

    private void SwatchesPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(14, 14, 14));

        // drag-to-resize grip strip
        g.FillRectangle(new SolidBrush(Color.FromArgb(22, 22, 22)), 0, 0, _swatchesPanel.Width, SwatchGripH);
        int dotCx = _swatchesPanel.Width / 2;
        using var dotBrush = new SolidBrush(Color.FromArgb(68, 68, 68));
        for (int d = -2; d <= 2; d++)
            g.FillEllipse(dotBrush, dotCx + d * 7 - 1, SwatchGripH / 2 - 1, 3, 3);

        using var titleFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        g.DrawString("Color Swatches", titleFont, new SolidBrush(Color.FromArgb(85, 85, 85)), 12, SwatchGripH + 4);

        if (_swatches.Count == 0)
        {
            using var hint = new Font("Segoe UI", 9f, FontStyle.Italic);
            g.DrawString("Analyze a color and click 'Save Color Swatch' — up to 5 swatches",
                hint, new SolidBrush(Color.FromArgb(50, 50, 50)),
                12, _swatchesPanel.Height / 2f - 8);
            return;
        }

        int outerMargin = 10;
        int gap         = 8;
        int totalW      = _swatchesPanel.Width - outerMargin * 2;
        int cardW       = (totalW - gap * 4) / 5;
        int cardTop     = SwatchGripH + 20;
        int cardH       = _swatchesPanel.Height - cardTop - 8;
        int colorBlockH = (int)(cardH * 0.52f);

        using var nameFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var pctFont  = new Font("Segoe UI", 7.5f);

        for (int i = 0; i < _swatches.Count; i++)
        {
            var matches = _swatches[i];
            int cx = outerMargin + i * (cardW + gap);

            var cardRect = new Rectangle(cx, cardTop, cardW, cardH);
            using var cardPath = RoundedRect(cardRect, 7);
            using (var bg = new SolidBrush(Color.FromArgb(26, 26, 26)))
                g.FillPath(bg, cardPath);
            using (var border = new Pen(Color.FromArgb(44, 44, 44), 1f))
                g.DrawPath(border, cardPath);

            // proportional color stripes clipped to top of card
            var colorRect = new Rectangle(cx + 1, cardTop + 1, cardW - 2, colorBlockH);
            g.SetClip(colorRect);
            int bx = colorRect.X;
            foreach (var m in matches)
            {
                int bw = Math.Max(1, (int)(colorRect.Width * m.Percentage / 100.0));
                using var fill = new SolidBrush(m.DisplayColor);
                g.FillRectangle(fill, bx, colorRect.Y, bw, colorRect.Height);
                bx += bw;
            }
            g.ResetClip();

            // divider between color block and text area
            g.FillRectangle(new SolidBrush(Color.FromArgb(44, 44, 44)),
                cx + 1, cardTop + colorBlockH, cardW - 2, 1);

            // pigment list
            int textY  = cardTop + colorBlockH + 7;
            int textX  = cx + 8;
            int maxTextW = cardW - 22;
            using var dotFont = new Font("Segoe UI", 6f);
            foreach (var m in matches)
            {
                if (textY + 16 > cardTop + cardH - 4) break;
                // color dot
                using var dot = new SolidBrush(m.DisplayColor);
                g.FillEllipse(dot, textX, textY + 3, 8, 8);
                using var dotBorder = new Pen(Color.FromArgb(60, 255, 255, 255), 0.5f);
                g.DrawEllipse(dotBorder, textX, textY + 3, 8, 8);

                // name
                using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
                float nameMaxW = maxTextW - 34;
                g.DrawString(m.Pigment.Name, nameFont,
                    new SolidBrush(Color.FromArgb(200, 200, 200)),
                    new RectangleF(textX + 12, textY, nameMaxW, 15), sf);

                // percentage right-aligned in card
                var pctStr  = $"{m.Percentage}%";
                var pctSz   = g.MeasureString(pctStr, pctFont);
                g.DrawString(pctStr, pctFont,
                    new SolidBrush(Color.FromArgb(210, 175, 70)),
                    cx + cardW - pctSz.Width - 6, textY);

                textY += 17;
            }

            // × remove button (top-right corner)
            int xr = cx + cardW - 20;
            int yr = cardTop + 5;
            using var xbg = new SolidBrush(Color.FromArgb(55, 55, 55));
            g.FillEllipse(xbg, xr, yr, 14, 14);
            using var xfont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var xsf   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("×", xfont, new SolidBrush(Color.FromArgb(180, 180, 180)),
                new RectangleF(xr, yr, 14, 14), xsf);
        }
    }

    private void SwatchesPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Y < SwatchGripH + 2)
        {
            _isResizingSwatches          = true;
            _swatchResizeStartScreenY    = Cursor.Position.Y;
            _swatchResizeStartH          = _swatchesPanel.Height;
        }
    }

    private void SwatchesPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isResizingSwatches)
        {
            // drag up = larger panel (panel is docked bottom, grows upward)
            int delta = _swatchResizeStartScreenY - Cursor.Position.Y;
            _swatchesPanel.Height = Math.Clamp(_swatchResizeStartH + delta, SwatchMinH, SwatchMaxH);
            return;
        }
        _swatchesPanel.Cursor = e.Y < SwatchGripH + 2 ? Cursors.SizeNS : Cursors.Default;
    }

    private void SwatchesPanel_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Y < SwatchGripH + 2) return; // ignore clicks on the grip strip

        int outerMargin = 10;
        int gap         = 8;
        int totalW      = _swatchesPanel.Width - outerMargin * 2;
        int cardW       = (totalW - gap * 4) / 5;
        int cardTop     = SwatchGripH + 20;

        for (int i = 0; i < _swatches.Count; i++)
        {
            int cx   = outerMargin + i * (cardW + gap);
            int xr   = cx + cardW - 20;
            int yr   = cardTop + 5;
            var xBtn = new Rectangle(xr - 2, yr - 2, 18, 18); // slightly enlarged hit area
            if (xBtn.Contains(e.Location))
            {
                _swatches.RemoveAt(i);
                _addSwatchBtn.Enabled = _swatches.Count < 5 && _matches.Count > 0;
                _swatchesPanel.Invalidate();
                return;
            }
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

    private Point MagToImagePoint(Point magPt)
    {
        if (_imageSelection.Width == 0 || _imageSelection.Height == 0) return Point.Empty;
        float sx = _imageSelection.Width  / (float)_magPanel.Width;
        float sy = _imageSelection.Height / (float)_magPanel.Height;
        return new Point(
            Math.Clamp(_imageSelection.X + (int)(magPt.X * sx), _imageSelection.X, _imageSelection.Right  - 1),
            Math.Clamp(_imageSelection.Y + (int)(magPt.Y * sy), _imageSelection.Y, _imageSelection.Bottom - 1));
    }

    private Rectangle ImageToMag(Rectangle img)
    {
        if (_imageSelection.Width == 0 || _imageSelection.Height == 0) return Rectangle.Empty;
        float sx = _magPanel.Width  / (float)_imageSelection.Width;
        float sy = _magPanel.Height / (float)_imageSelection.Height;
        return Rectangle.FromLTRB(
            (int)((img.Left   - _imageSelection.X) * sx),
            (int)((img.Top    - _imageSelection.Y) * sy),
            (int)((img.Right  - _imageSelection.X) * sx),
            (int)((img.Bottom - _imageSelection.Y) * sy));
    }

    // Builds an image-space rectangle corresponding to the screen-space hover circle.
    private Rectangle CircleSelectionAtScreen(Point screenPt)
    {
        if (_image == null) return Rectangle.Empty;
        var dr = ZoomedDrawRect();
        if (dr.Width <= 0 || dr.Height <= 0) return Rectangle.Empty;

        float scaleX  = _image.Width  / dr.Width;
        float scaleY  = _image.Height / dr.Height;
        // keep center as float to avoid truncation-induced offset
        float centerX = (screenPt.X - dr.X) * scaleX;
        float centerY = (screenPt.Y - dr.Y) * scaleY;
        float radX    = Math.Max(1f, HoverRadiusPx * scaleX);
        float radY    = Math.Max(1f, HoverRadiusPx * scaleY);

        return Rectangle.FromLTRB(
            Math.Max(0,            (int)Math.Round(centerX - radX)),
            Math.Max(0,            (int)Math.Round(centerY - radY)),
            Math.Min(_image.Width,  (int)Math.Round(centerX + radX)),
            Math.Min(_image.Height, (int)Math.Round(centerY + radY)));
    }

    private static Rectangle NormalizeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X,                r.Y,                  radius * 2, radius * 2, 180, 90);
        p.AddArc(r.Right - radius*2, r.Y,                  radius * 2, radius * 2, 270, 90);
        p.AddArc(r.Right - radius*2, r.Bottom - radius * 2, radius * 2, radius * 2,   0, 90);
        p.AddArc(r.X,                r.Bottom - radius * 2, radius * 2, radius * 2,  90, 90);
        p.CloseFigure();
        return p;
    }

    private static void DrawHint(Graphics g, Control ctrl, string text)
    {
        using var f  = new Font("Segoe UI", 10f, FontStyle.Italic);
        var       sz = g.MeasureString(text, f);
        g.DrawString(text, f, new SolidBrush(Color.FromArgb(68, 68, 68)),
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
