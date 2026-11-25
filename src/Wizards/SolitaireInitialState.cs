namespace Wizards;

internal sealed partial class TerritorySetup
{
    private string artworkFolder = "";
    private InitialState? initialState;
    private readonly Dictionary<string, Image> setupImages = new();
    private readonly Button completeSetup = new() { Text = "Complete setup", AutoSize = true, Enabled = false };
    private readonly Button viewRecord = new() { Text = "Game record", AutoSize = true, Enabled = false };
    private static readonly Hex[] Clockwise = { new(0,-1), new(1,-1), new(1,0), new(0,1), new(-1,1), new(-1,0) };
    // Printed local coordinates, with A1 at the top of an unrotated tile.
    private static readonly Hex[,] NumberedSpaces =
    {
        { new(0,-2), new(1,-2), new(2,-2), new(0,-1), new(1,-1), new(2,-1) },
        { new(2,0), new(1,1), new(0,2), new(1,0), new(0,1), new(-1,2) },
        { new(-2,2), new(-2,1), new(-2,0), new(-1,1), new(-1,0), new(-1,-1) }
    };

    // Section 3.3: only towns, dwellings and Shrineheart Abbey qualify.
    // Interior settlements are included here and filtered by actual water adjacency.
    private static readonly Dictionary<string, string[]> Settlements = new()
    {
        ["Common_1"] = new[] { "Center" },
        ["Common_2"] = new[] { "A1", "B3" },
        ["Common_3"] = new[] { "C6", "B1" },
        ["Common_4"] = new[] { "C2", "A4" },
        ["Common_5"] = new[] { "A2", "B5" },
        ["Common_6"] = new[] { "C6" },
        ["Elven_1"] = new[] { "C3" },
        ["Elven_2"] = new[] { "B2" },
        ["Elven_3"] = new[] { "A1" },
        ["Elven_4"] = new[] { "A4" },
        ["Elven_5"] = new[] { "B5" },
        ["Elven_6"] = new[] { "Center" }
    };

    private sealed class InitialState
    {
        public int Day = 1;
        public int Completion = 0;
        public string ThwartPosition = "Takeover";
        public int Rank = 0;
        public int Knowledge = 0, Perception = 0, Power = 0;
        public string Order = "None";
        public string Color = "Blue";
        public Hex Player, Dragon;
        public string PlayerLocation = "", DragonLocation = "";
        public List<Hex> Boats = new();
        public List<string> TaskDeck = new(), EventDeck = new(), WizardDeck = new();
        public Dictionary<string, string> HiddenGems = new();
    }

    private static bool SetupAsset(string name)
    {
        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;
        return name == "Board_2917x4276.png" || name == "Tile_Back.png" ||
            name.StartsWith("Territory_", StringComparison.Ordinal) ||
            name.StartsWith("Task_Card_", StringComparison.Ordinal) ||
            name.StartsWith("Event_Card_", StringComparison.Ordinal) ||
            name.StartsWith("Wizard_Card_", StringComparison.Ordinal) ||
            name.StartsWith("Gem_Card_", StringComparison.Ordinal) ||
            name.StartsWith("Player_", StringComparison.Ordinal) ||
            name.StartsWith("Wizard_", StringComparison.Ordinal) ||
            name == "Boat_Small.png" || name == "Dragon_Small.png" ||
            name == "Dashboard.png" || name == "Day_Marker.png" ||
            name == "Thwart_Marker.png" || name == "Task_Completion_Marker.png";
    }

    private static Hex Add(Hex a, Hex b) => new(a.Q+b.Q,a.R+b.R);
    private static Hex Global(Placement p, Hex local)
    {
        for (int i=0; i<p.Rotation; i++) local = new Hex(-local.R, local.Q+local.R);
        return Add(p.Center, local);
    }
    private static Hex LocalSpace(string code) => code == "Center" ? new Hex(0,0)
        : NumberedSpaces[code[0]-'A', code[1]-'1'];
    private static string TileKey(Placement p) => string.Join("_", p.Name.Split('_').Skip(1).Take(2));
    private static string TileLabel(Placement p) => string.Join(" ",p.Name.Split('_').Skip(3));
    private static int Die() => Random.Shared.Next(1,7);

    private static (Hex Hex, string Label) RandomSpace(Placement p)
    {
        // Section 4.2: white die selects A/B/C and red die selects 1–6.
        int row = (Die()-1)/2, col = Die()-1;
        return (Global(p, NumberedSpaces[row,col]), TileLabel(p) + " " + (char)('A'+row) + (col+1));
    }

    private List<string> ShuffledCards(string prefix, int expected)
    {
        var list = Directory.GetFiles(artworkFolder, prefix + "*.png")
            .Select(p => Path.GetFileNameWithoutExtension(p)!)
            .Where(n => n != prefix + "Back").OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (list.Count != expected) throw new InvalidDataException($"Expected {expected} {prefix} faces, found {list.Count}.");
        Shuffle(list);
        return list;
    }

    private void CompleteInitialSetup()
    {
        if (!revealed || initialState != null) return;
        var common = placed.Where(p => TileKey(p).StartsWith("Common_")).ToList();
        if (common.Count == 0)
        {
            MessageBox.Show(this, "All Common Territories were removed, so section 3.71 has no valid starting space. Return to the menu and begin a new setup.", "No Common starting space");
            return;
        }

        using var choose = new Form { Text = "Select your playing piece", StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, ClientSize = new Size(330,125), MaximizeBox = false, MinimizeBox = false };
        var colors = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 20, Top = 15, Width = 290 };
        colors.Items.AddRange(new object[] { "Blue", "Green", "Purple", "Red", "White", "Yellow" });
        colors.SelectedIndex = 0;
        var ok = new Button { Text = "Begin game setup", Left = 20, Top = 55, Width = 290, Height = 38, DialogResult = DialogResult.OK };
        choose.Controls.Add(colors); choose.Controls.Add(ok); choose.AcceptButton = ok;
        if (choose.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var state = new InitialState { Color = (string)colors.SelectedItem! };
            state.TaskDeck = ShuffledCards("Task_Card_",98);
            state.EventDeck = ShuffledCards("Event_Card_",49);
            state.WizardDeck = ShuffledCards("Wizard_Card_",14);
            var gems = ShuffledCards("Gem_Card_",7);
            var wizards = new[] { "Ishkatar","Aevarex","Elekov","Terek","Tolmitar","Veldor","Zegoral" };
            for (int i=0; i<wizards.Length; i++) state.HiddenGems.Add(wizards[i],gems[i]);

            var land = placed.SelectMany(p => Footprint(p.Center)).ToHashSet();
            foreach (var p in placed)
            {
                if (!Settlements.TryGetValue(TileKey(p), out var settlements)) continue;
                foreach (string code in settlements)
                {
                    var town = Global(p,LocalSpace(code));
                    var neighbors = Clockwise.Select(d => Add(town,d)).ToArray();
                    // Select the clockwise end of a contiguous run of water.
                    // In a disconnected tie, use the first clockwise candidate from north.
                    for (int i=0; i<6; i++)
                        if (!land.Contains(neighbors[i]) && land.Contains(neighbors[(i+1)%6]))
                        {
                            state.Boats.Add(neighbors[i]);
                            break;
                        }
                }
            }
            var lair = placed.Single(p => TileKey(p) == "Magic_6");
            (state.Dragon,state.DragonLocation) = RandomSpace(lair);
            // Section 4.1: red die chooses Common territory number. Reroll removed tiles.
            int rolled;
            do { rolled = Die(); } while (!common.Any(p => TileKey(p) == "Common_"+rolled));
            var start = common.Single(p => TileKey(p) == "Common_"+rolled);
            (state.Player,state.PlayerLocation) = RandomSpace(start);

            foreach (string name in new[] { "Boat_Small","Dragon_Small","Player_"+state.Color })
                if (!setupImages.ContainsKey(name))
                    setupImages.Add(name,LoadImage(Path.Combine(artworkFolder,name+".png")));
            initialState = state;
            completeSetup.Enabled = false;
            viewRecord.Enabled = true;
            UpdateStatus();
            canvas.Invalidate();
            ShowInitialRecord();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Unable to complete setup: "+ex.Message,"Wizards",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void DrawInitialPieces(Graphics g)
    {
        if (initialState is not InitialState s) return;
        void Draw(string name, Hex h, float offset = 0)
        {
            var im = setupImages[name];
            var p = Center(h);
            float scale = Math.Min(70f/im.Width,78f/im.Height);
            float w=im.Width*scale, height=im.Height*scale;
            g.DrawImage(im,p.X-w/2+offset,p.Y-height/2,w,height);
        }
        foreach(var group in s.Boats.GroupBy(h=>h))
        {
            int i=0, count=group.Count();
            foreach(var boat in group) Draw("Boat_Small",boat,(i++-(count-1)/2f)*22);
        }
        Draw("Dragon_Small",s.Dragon);
        Draw("Player_"+s.Color,s.Player);
    }

    private void ShowInitialRecord()
    {
        if (initialState is not InitialState s) return;
        using var form = new Form { Text = "Wizards — Initial game record", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(510,480), MinimumSize = new Size(460,450) };
        var text = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI",11), BackColor = Color.White,
            Text = $"DAY {s.Day}    |    Completion: {s.Completion}    |    Thwart: {s.ThwartPosition}\r\n\r\n"+
            $"Player: {s.Color}\r\nStarting space: {s.PlayerLocation}\r\nMagical Order: {s.Order} (Rank {s.Rank})\r\n"+
            $"Knowledge: {s.Knowledge}    Perception: {s.Perception}    Power: {s.Power}\r\n"+
            "Tasks: None    Events: None    Gems: None    Animal: None\r\n\r\n"+
            $"Dragon: {s.DragonLocation}\r\nBoats placed: {s.Boats.Count}\r\nTerritories in play: {placed.Count}\r\n\r\n"+
            $"Task deck: {s.TaskDeck.Count}\r\nEvent deck: {s.EventDeck.Count}\r\nWizard deck: {s.WizardDeck.Count}\r\n"+
            $"Gem cards: {s.HiddenGems.Count} assigned secretly to the seven High Wizards\r\n\r\n"+
            "Setup preview complete. Movement, encounters and saving are not yet enabled." };
        form.Controls.Add(text);
        form.ShowDialog(this);
    }
}
