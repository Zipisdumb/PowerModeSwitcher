namespace PowerModeSwitcher;

public record PlanItem(string Guid, string Name)
{
    public override string ToString() => Name;
}

public record KeyItem(int Vk, string Label)
{
    public override string ToString() => Label;
}

public class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Action _onSaved;
    private readonly TextBox _abPathBox;
    private readonly List<(ComboBox PlanBox, ComboBox AbBox, ComboBox KeyBox, TextBox NameBox)> _rows = new();

    public SettingsForm(AppConfig config, Action onSaved)
    {
        _config = config;
        _onSaved = onSaved;

        Text = "PowerModeSwitcher — Settings";
        Size = new Size(640, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 24, 27);
        ForeColor = Color.WhiteSmoke;

        var plans = LoadPlanItems();

        var keys = new List<KeyItem>
        {
            new(0, "None"),
            new(0x31, "Ctrl + Alt + 1"),
            new(0x32, "Ctrl + Alt + 2"),
            new(0x33, "Ctrl + Alt + 3"),
            new(0x78, "Ctrl + Alt + F9"),
            new(0x79, "Ctrl + Alt + F10"),
            new(0x7A, "Ctrl + Alt + F11")
        };

        int y = 18;
        Controls.Add(new Label
        {
            Text = "Mode Name                Windows Power Plan                       Afterburner        Global Hotkey",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Color.Silver
        });
        y += 28;

        foreach (var mode in _config.Modes)
        {
            var nameBox = new TextBox
            {
                Text = mode.Name,
                Location = new Point(20, y),
                Width = 120,
                BackColor = Color.FromArgb(39, 39, 42),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var planBox = new ComboBox
            {
                Location = new Point(150, y),
                Width = 210,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(39, 39, 42),
                ForeColor = Color.White
            };
            PopulatePlanBox(planBox, plans, mode.PowerPlanGuid);

            var abBox = new ComboBox
            {
                Location = new Point(370, y),
                Width = 95,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(39, 39, 42),
                ForeColor = Color.White
            };
            abBox.Items.AddRange(new object[] { "Disabled", "Profile 1", "Profile 2", "Profile 3", "Profile 4", "Profile 5" });
            abBox.SelectedIndex = Math.Clamp(mode.AfterburnerProfile, 0, 5);

            var keyBox = new ComboBox
            {
                Location = new Point(475, y),
                Width = 125,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(39, 39, 42),
                ForeColor = Color.White
            };
            foreach (var k in keys) keyBox.Items.Add(k);
            int keyIdx = keys.FindIndex(k => k.Vk == mode.HotkeyVirtualKey);
            keyBox.SelectedIndex = keyIdx >= 0 ? keyIdx : 0;

            Controls.AddRange(new Control[] { nameBox, planBox, abBox, keyBox });
            _rows.Add((planBox, abBox, keyBox, nameBox));
            y += 38;
        }

        y += 12;
        Controls.Add(new Label
        {
            Text = "MSI Afterburner .exe Path:",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Color.Silver
        });
        y += 22;

        _abPathBox = new TextBox
        {
            Text = _config.AfterburnerPath,
            Location = new Point(20, y),
            Width = 490,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var browseBtn = new Button
        {
            Text = "Browse",
            Location = new Point(520, y - 2),
            Width = 80,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(63, 63, 70)
        };
        browseBtn.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe", FileName = "MSIAfterburner.exe" };
            if (dlg.ShowDialog() == DialogResult.OK) _abPathBox.Text = dlg.FileName;
        };
        Controls.AddRange(new Control[] { _abPathBox, browseBtn });

        y += 45;
        var createPlansBtn = new Button
        {
            Text = "Create Plans && Profiles",
            Location = new Point(20, y),
            Width = 185,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(63, 63, 70),
            ForeColor = Color.White
        };
        createPlansBtn.Click += (_, _) =>
        {
            var (afkGuid, casualGuid, perfGuid) = PowerManager.CreateRecommendedPlans();
            var updatedPlans = LoadPlanItems();
            string[] generated = { afkGuid, casualGuid, perfGuid };

            for (int i = 0; i < _rows.Count; i++)
            {
                string targetGuid = i < generated.Length ? generated[i] : "";
                PopulatePlanBox(_rows[i].PlanBox, updatedPlans, targetGuid);
            }

            string abStatus;
            try
            {
                bool abOk = PowerManager.CreateAfterburnerProfiles(_abPathBox.Text.Trim());
                abStatus = abOk
                    ? "MSI Afterburner Profiles 1 (100%), 2 (60%), and 3 (100%) created."
                    : "MSI Afterburner GPU profile file not found (check path).";
            }
            catch (UnauthorizedAccessException)
            {
                abStatus = "Run app as Administrator to auto-create MSI Afterburner profiles in Program Files.";
            }

            MessageBox.Show(
                $"Windows Power Plans created & assigned:\n• PMS - AFK / Macro\n• PMS - Casual\n• PMS - Performance\n\n{abStatus}",
                "Setup Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        Controls.Add(createPlansBtn);

        var saveBtn = new Button
        {
            Text = "Save && Apply",
            Location = new Point(480, y),
            Width = 120,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White
        };
        saveBtn.Click += (_, _) =>
        {
            for (int i = 0; i < _config.Modes.Count; i++)
            {
                _config.Modes[i].Name = _rows[i].NameBox.Text.Trim();
                _config.Modes[i].PowerPlanGuid = (_rows[i].PlanBox.SelectedItem as PlanItem)?.Guid ?? "";
                _config.Modes[i].AfterburnerProfile = _rows[i].AbBox.SelectedIndex;
                _config.Modes[i].HotkeyVirtualKey = (_rows[i].KeyBox.SelectedItem as KeyItem)?.Vk ?? 0;
            }
            _config.AfterburnerPath = _abPathBox.Text.Trim();
            _config.Save();
            _onSaved();
            Close();
        };
        Controls.Add(saveBtn);
    }

    private static List<PlanItem> LoadPlanItems()
    {
        var plans = PowerManager.GetAllPowerPlans()
            .Select(kv => new PlanItem(kv.Key.ToString(), kv.Value))
            .ToList();
        plans.Insert(0, new PlanItem("", "(Do not change)"));
        return plans;
    }

    private static void PopulatePlanBox(ComboBox box, List<PlanItem> plans, string selectedGuid)
    {
        box.Items.Clear();
        foreach (var p in plans) box.Items.Add(p);
        int idx = plans.FindIndex(p => string.Equals(p.Guid, selectedGuid, StringComparison.OrdinalIgnoreCase));
        box.SelectedIndex = idx >= 0 ? idx : 0;
    }
}