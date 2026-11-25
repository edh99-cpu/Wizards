namespace Wizards;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Launcher());
    }
}

internal sealed class Launcher : Form
{
    public Launcher()
    {
        Text = "Wizards — The Enchanted Islands";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(540, 570);
        ClientSize = new Size(640, 600);
        BackColor = Color.FromArgb(25, 39, 35);
        ForeColor = Color.FromArgb(239, 232, 207);
        Font = new Font("Segoe UI", 11);
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
            Padding = new Padding(36), AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = "WIZARDS", Font = new Font("Georgia", 36, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top,
            Height = 80
        });
        panel.Controls.Add(new Label
        {
            Text = "The Enchanted Islands\nWindows conversion — development preview",
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top,
            Height = 70
        });
        AddMode(panel, "Solitaire", "Initial game setup (Appendix A)");
        AddMode(panel, "Local hot-seat", "2–6 human players on one computer");
        AddMode(panel, "Play by email", "2–6 human players exchanging saved turns");
        AddMode(panel, "Resume saved game", "Continue a previously saved game");
        var quit = new Button { Text = "Exit", Dock = DockStyle.Top, Height = 42 };
        quit.Click += (_, _) => Close();
        panel.Controls.Add(quit);
        panel.Controls.Add(new Label
        {
            Text = "Development build 0.3 — solitaire initial game setup.",
            Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Height = 36
        });
        Controls.Add(panel);
    }

    private void AddMode(TableLayoutPanel panel, string title, string description)
    {
        var button = new Button
        {
            Text = title + "\n" + description, Dock = DockStyle.Top,
            Height = 66, Margin = new Padding(0, 5, 0, 5)
        };
        button.Click += (_, _) =>
        {
            if (title == "Solitaire") TerritorySetup.Open(this);
            else MessageBox.Show(this, title + " is not yet available.", "Wizards",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        panel.Controls.Add(button);
    }
}
