using System.Drawing.Drawing2D;
using System.IO.Compression;

namespace Wizards;

internal sealed partial class TerritorySetup : Form
{
    private readonly Image board;
    private readonly Image back;
    private readonly Dictionary<string, Image> fronts = new();
    private readonly List<string> deck = new();
    private readonly List<Placement> placed = new();
    private readonly BoardCanvas canvas;
    private readonly Panel scroll = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(24, 36, 39) };
    private readonly Label status = new() { AutoSize = true, Padding = new Padding(8) };
    private readonly Button reveal = new() { Text = "Reveal territories", AutoSize = true, Enabled = false };
    private readonly Button undo = new() { Text = "Undo placement", AutoSize = true };
    private readonly Button rotate = new() { Text = "Rotate 60°", AutoSize = true };
    private int rotation;
    private bool revealed;
    private float zoom = 1;
    private const double Dx = 87.06664199358161, Dy = 104.0;
    private const double X0 = 48, Y0 = 51;
    private readonly record struct Hex(int Q, int R);
    private readonly record struct Placement(string Name, Hex Center, int Rotation);

    public static void Open(Form owner)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wizards", "Artwork-v2");
            if (!File.Exists(Path.Combine(folder, "ready.txt")))
            {
                using var picker = new OpenFileDialog
                {
                    Title = "Select your Wizards v1.0 VASSAL module (expanded artwork import)",
                    Filter = "Wizards module (*.vmod)|*.vmod|ZIP archive (*.zip)|*.zip"
                };
                if (picker.ShowDialog(owner) != DialogResult.OK) return;
                Import(picker.FileName, folder);
            }
            using var options = new SetupOptions();
            if (options.ShowDialog(owner) != DialogResult.OK) return;
            using var setup = new TerritorySetup(folder, options.Removed, options.Tower);
            setup.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "Unable to open Territory setup:\n" + ex.Message, "Wizards",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Import(string archive, string folder)
    {
        using var zip = ZipFile.OpenRead(archive);
        var entries = zip.Entries.Where(e => e.FullName.StartsWith("images/", StringComparison.Ordinal)
            && SetupAsset(e.Name)).ToArray();
        if (entries.Count(e => e.Name.StartsWith("Territory_", StringComparison.Ordinal)) != 18
            || !entries.Any(e => e.Name == "Board_2917x4276.png")
            || !entries.Any(e => e.Name == "Tile_Back.png")
            || entries.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidDataException("Expected the board, tile back and 18 Territory fronts from Wizards v1.0.");
        Directory.CreateDirectory(folder);
        foreach (var entry in entries)
        {
            using (var stream = entry.Open())
            using (var image = Image.FromStream(stream)) { _ = image.Width; }
            entry.ExtractToFile(Path.Combine(folder, entry.Name), true);
        }
        File.WriteAllText(Path.Combine(folder, "ready.txt"), "Wizards artwork v2");
    }

    private static Image LoadImage(string path)
    {
        using var source = Image.FromFile(path);
        return new Bitmap(source);
    }

    private TerritorySetup(string folder, int removed, int tower)
    {
        artworkFolder = folder;
        board = LoadImage(Path.Combine(folder, "Board_2917x4276.png"));
        back = LoadImage(Path.Combine(folder, "Tile_Back.png"));
        foreach (string file in Directory.GetFiles(folder, "Territory_*.png"))
            fronts.Add(Path.GetFileNameWithoutExtension(file), LoadImage(file));
        if (fronts.Count != 18) throw new InvalidDataException("The artwork cache must contain 18 Territory fronts.");

        // Appendix A: protect three essential territories and one chosen Tower;
        // randomly remove from the other fourteen, then shuffle the combined set.
        var protectedTiles = fronts.Keys.Where(n => n.StartsWith("Territory_Magic_1_")
            || n.StartsWith("Territory_Magic_5_") || n.StartsWith("Territory_Magic_6_")
            || n.StartsWith("Territory_Magic_" + tower + "_")).ToList();
        var other = fronts.Keys.Except(protectedTiles).ToList();
        Shuffle(other);
        deck.AddRange(other.Skip(removed));
        deck.AddRange(protectedTiles);
        Shuffle(deck);

        Text = "Wizards — Solitaire Territory setup (development preview)";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(800, 550);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(6) };
        tools.Controls.Add(MakeButton("Fit board", () => { zoom = 1; ResizeCanvas(); }));
        tools.Controls.Add(MakeButton("Zoom +", () => { zoom = Math.Min(5f, zoom * 1.3f); ResizeCanvas(); }));
        tools.Controls.Add(MakeButton("Zoom −", () => { zoom = Math.Max(1f, zoom / 1.3f); ResizeCanvas(); }));
        rotate.Click += (_, _) => { rotation = (rotation + 1) % 6; canvas.Invalidate(); UpdateStatus(); };
        undo.Click += (_, _) => { if (!revealed && placed.Count > 0) { var last = placed[^1]; deck.Insert(0, last.Name); placed.RemoveAt(placed.Count - 1); canvas.Invalidate(); UpdateStatus(); } };
        reveal.Click += (_, _) =>
        {
            if (deck.Count != 0 || revealed) return;
            revealed = true;
            canvas.Invalidate();
            UpdateStatus();
        };
        tools.Controls.Add(rotate);
        tools.Controls.Add(undo);
        tools.Controls.Add(reveal);
        completeSetup.Click += (_, _) => CompleteInitialSetup();
        viewRecord.Click += (_, _) => ShowInitialRecord();
        tools.Controls.Add(completeSetup);
        tools.Controls.Add(viewRecord);
        AddMovementControls(tools);
        tools.Controls.Add(MakeButton("Return to menu", Close));
        tools.Controls.Add(status);
        canvas = new BoardCanvas(this);
        scroll.Controls.Add(canvas);
        Controls.Add(scroll);
        Controls.Add(tools);
        scroll.Resize += (_, _) => ResizeCanvas();
        Shown += (_, _) => ResizeCanvas();
        UpdateStatus();
    }

    private static void Shuffle<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static Button MakeButton(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += (_, _) => action();
        return b;
    }

    private void ResizeCanvas()
    {
        if (scroll.ClientSize.Width <= 25 || scroll.ClientSize.Height <= 25) return;
        float fit = Math.Min((scroll.ClientSize.Width - 22f) / board.Width, (scroll.ClientSize.Height - 22f) / board.Height);
        canvas.ScaleFactor = Math.Max(.05f, fit * zoom);
        canvas.Size = new Size((int)Math.Ceiling(board.Width * canvas.ScaleFactor), (int)Math.Ceiling(board.Height * canvas.ScaleFactor));
        canvas.Invalidate();
    }

    private void UpdateStatus()
    {
        reveal.Enabled = deck.Count == 0 && !revealed;
        rotate.Enabled = deck.Count > 0 && !revealed;
        undo.Enabled = placed.Count > 0 && !revealed;
        completeSetup.Enabled = revealed && initialState == null;
        RefreshMovement();
        status.Text = initialState != null ? MovementStatus()
            : revealed ? "Territories revealed — click Complete setup to initialize the game."
            : $"Placed: {placed.Count}   Remaining: {deck.Count}   Rotation: {rotation * 60}°   |   Click a valid hex to place.";
    }

    private static PointF Center(Hex h) => new((float)(X0 + Dx * h.Q), (float)(Y0 + Dy * (h.R + h.Q / 2.0)));
    private static Hex Nearest(float x, float y)
    {
        double q = (x - X0) / Dx, r = (y - Y0) / Dy - q / 2;
        Hex best = default;
        double distance = double.PositiveInfinity;
        for (int a = (int)Math.Floor(q) - 1; a <= (int)Math.Ceiling(q) + 1; a++)
        for (int b = (int)Math.Floor(r) - 1; b <= (int)Math.Ceiling(r) + 1; b++)
        {
            var h = new Hex(a, b);
            var p = Center(h);
            double d = Math.Pow(p.X - x, 2) + Math.Pow(p.Y - y, 2);
            if (d < distance) { distance = d; best = h; }
        }
        return best;
    }

    private static IEnumerable<Hex> Footprint(Hex h)
    {
        for (int q = -2; q <= 2; q++)
        for (int r = -2; r <= 2; r++)
            if (Math.Abs(q + r) <= 2) yield return new Hex(h.Q + q, h.R + r);
    }

    private bool Valid(Hex h)
    {
        var occupied = placed.SelectMany(p => Footprint(p.Center)).ToHashSet();
        foreach (var cell in Footprint(h))
        {
            var p = Center(cell);
            // Each complete hex must lie inside the 50-pixel water-board border.
            if (p.X - 2 * Dx / 3 < 49 || p.X + 2 * Dx / 3 > board.Width - 49
                || p.Y - Dy / 2 < 49 || p.Y + Dy / 2 > board.Height - 49
                || occupied.Contains(cell)) return false;
        }
        return true;
    }

    private static PointF[] HexPolygon(Hex h)
    {
        PointF c = Center(h);
        float w = (float)(2 * Dx / 3), t = (float)(Dy / 2);
        return new PointF[] { new(c.X - w, c.Y), new(c.X - w/2,c.Y-t), new(c.X+w/2,c.Y-t),
            new(c.X+w,c.Y),new(c.X+w/2,c.Y+t),new(c.X-w/2,c.Y+t) };
    }

    private void DrawTile(Graphics g, Image im, Hex h, int turns)
    {
        PointF c = Center(h);
        double angle = turns * Math.PI / 3;
        double stretch = Dx / (Dy * Math.Sqrt(3) / 2);
        // Normalize the scan's slightly compressed horizontal axis before rotation.
        PointF Transform(double x, double y)
        {
            x /= stretch;
            return new PointF(c.X + (float)((x * Math.Cos(angle) - y * Math.Sin(angle)) * stretch),
                c.Y + (float)(x * Math.Sin(angle) + y * Math.Cos(angle)));
        }
        double w = 8 * Dx / 3, t = 2.5 * Dy;
        g.DrawImage(im, new[] { Transform(-w,-t), Transform(w,-t), Transform(-w,t) });
    }

    private void Place(Hex h)
    {
        if (revealed || deck.Count == 0 || !Valid(h)) return;
        placed.Add(new Placement(deck[0], h, rotation));
        deck.RemoveAt(0);
        UpdateStatus();
        canvas.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            board.Dispose();
            back.Dispose();
            foreach (var image in fronts.Values) image.Dispose();
            foreach (var image in setupImages.Values) image.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class BoardCanvas : Control
    {
        private readonly TerritorySetup owner;
        private Hex? hover;
        public float ScaleFactor { get; set; } = 1f;
        public BoardCanvas(TerritorySetup owner)
        {
            this.owner = owner;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Cursor = Cursors.Cross;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var h = Nearest(e.X / ScaleFactor, e.Y / ScaleFactor);
            if (hover != h) { hover = h; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { hover = null; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                var h = Nearest(e.X / ScaleFactor, e.Y / ScaleFactor);
                if (owner.initialState != null) owner.MoveClick(h);
                else owner.Place(h);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.ScaleTransform(ScaleFactor, ScaleFactor);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(owner.board, new Rectangle(0, 0, owner.board.Width, owner.board.Height));
            foreach (var p in owner.placed)
                owner.DrawTile(g, owner.revealed ? owner.fronts[p.Name] : owner.back, p.Center, p.Rotation);
            owner.DrawMovementHints(g);
            owner.DrawInitialPieces(g);
            owner.DrawEncounterMarkers(g);
            if (hover is Hex h && !owner.revealed && owner.deck.Count > 0)
            {
                bool valid = owner.Valid(h);
                using var pen = new Pen(valid ? Color.LimeGreen : Color.OrangeRed, 5);
                using var brush = new SolidBrush(Color.FromArgb(55, valid ? Color.LimeGreen : Color.OrangeRed));
                foreach (var cell in Footprint(h))
                {
                    var polygon = HexPolygon(cell);
                    g.FillPolygon(brush, polygon);
                    g.DrawPolygon(pen, polygon);
                }
            }
        }
    }

    private sealed class SetupOptions : Form
    {
        private readonly ComboBox difficulty = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 345 };
        private readonly ComboBox tower = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 345 };
        public int Removed { get; private set; }
        public int Tower => tower.SelectedIndex + 2;
        public SetupOptions()
        {
            Text = "Solitaire setup — Appendix A";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(460, 280);
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
            panel.Controls.Add(new Label { Text = "Protect one Sorcerer's Tower:", AutoSize = true });
            tower.Items.AddRange(new object[] { "Megmoran's Tower", "Meligar's Tower", "Melekok's Tower" });
            tower.SelectedIndex = 0;
            panel.Controls.Add(tower);
            panel.Controls.Add(new Label { Text = "Territories to remove randomly:", AutoSize = true, Margin = new Padding(3, 15, 3, 3) });
            difficulty.Items.Add("Roll one die (1–6)");
            for (int i = 0; i <= 6; i++) difficulty.Items.Add($"Remove {i} randomly");
            difficulty.SelectedIndex = 0;
            panel.Controls.Add(difficulty);
            panel.Controls.Add(new Label { Text = "Sacred Circle, Hamdrel's Cottage, Dragon's Lair\nand the chosen Tower are protected.", AutoSize = true, Margin = new Padding(3, 12, 3, 3) });
            var start = new Button { Text = "Begin face-down placement", AutoSize = true };
            start.Click += (_, _) => { Removed = difficulty.SelectedIndex == 0 ? Random.Shared.Next(1, 7) : difficulty.SelectedIndex - 1; DialogResult = DialogResult.OK; };
            panel.Controls.Add(start);
            AcceptButton = start;
            Controls.Add(panel);
        }
    }
}
