namespace SSP.ExplorerKit.UI;

/// <summary>
/// Lets the user choose the Explorer window in which the new tab is opened,
/// and close Explorer windows that are no longer needed.
/// </summary>
internal sealed class WindowPickerDialog : Form
{
    private readonly ListView _windowList;
    private readonly ListView _tabList;
    private readonly Label _tabHeader;
    private readonly Button _openButton;
    private readonly Button _closeWindowsButton;
    private ExplorerWindow? _selected;

    private WindowPickerDialog(IReadOnlyList<ExplorerWindow> windows, string path)
    {
        Text = "Explorer-Fenster wählen";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont ?? Font;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        KeyPreview = true;
        ClientSize = new Size(900, 540);
        MinimumSize = new Size(700, 400);

        var header = new Label
        {
            Text = $"In welchem Explorer-Fenster soll \"{path}\" als neuer Tab geöffnet werden?",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 6),
        };

        // Upper part: one row per Explorer window.
        _windowList = CreateListView(multiSelect: true);
        _windowList.Columns.Add("Nr", 45);
        _windowList.Columns.Add("Fenster", 330);
        _windowList.Columns.Add("Geöffnet", 125);
        _windowList.Columns.Add("Läuft seit", 115);
        _windowList.Columns.Add("Tabs", 50, HorizontalAlignment.Right);
        _windowList.Columns.Add("Handle", 95);

        // Lower part: the tabs of the selected window(s), one per row.
        _tabHeader = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };
        _tabList = CreateListView(multiSelect: false);
        _tabList.Columns.Add("Fenster", 60, HorizontalAlignment.Right);
        _tabList.Columns.Add("#", 35, HorizontalAlignment.Right);
        _tabList.Columns.Add("Name", 200);
        _tabList.Columns.Add("Pfad", 560);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
        };
        split.Panel1.Controls.Add(_windowList);
        split.Panel2.Controls.Add(_tabList);
        split.Panel2.Controls.Add(_tabHeader);

        _openButton = new Button { Text = "&Öffnen", AutoSize = true, DialogResult = DialogResult.OK };
        var newWindowButton = new Button { Text = "In &neuem Fenster", AutoSize = true, DialogResult = DialogResult.Yes };
        var cancelButton = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        _closeWindowsButton = new Button { Text = "Fenster &schließen", AutoSize = true };
        var refreshButton = new Button { Text = "&Aktualisieren", AutoSize = true };

        var leftButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        leftButtons.Controls.AddRange([_closeWindowsButton, refreshButton]);
        var rightButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
        };
        rightButtons.Controls.AddRange([cancelButton, newWindowButton, _openButton]);

        var buttons = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 6, 0, 0),
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.Controls.Add(leftButtons, 0, 0);
        buttons.Controls.Add(rightButtons, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(split, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        AcceptButton = _openButton;
        CancelButton = cancelButton;

        _windowList.SelectedIndexChanged += (_, _) => UpdateSelectionState();
        _windowList.ItemActivate += (_, _) => AcceptIfSingleSelection();
        _tabList.ItemActivate += (_, _) => AcceptIfSingleSelection();
        _closeWindowsButton.Click += (_, _) => CloseSelectedWindows();
        refreshButton.Click += (_, _) => Reload();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && _windowList.Focused)
            {
                CloseSelectedWindows();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                Reload();
                e.Handled = true;
            }
        };

        FormClosing += (_, _) =>
        {
            if (DialogResult == DialogResult.OK)
            {
                _selected = SelectedWindows.Count == 1 ? SelectedWindows[0] : null;
            }
        };
        Load += (_, _) => split.SplitterDistance = split.Height * 45 / 100;
        Shown += (_, _) => { Activate(); _windowList.Focus(); };

        PopulateWindows(windows, selectHandles: []);
    }

    /// <summary>Shows the dialog.</summary>
    /// <param name="windows">The open Explorer windows.</param>
    /// <param name="path">The directory to be opened (shown in the header).</param>
    /// <returns>The chosen window, or <c>null</c> to open a new Explorer window.</returns>
    /// <exception cref="OperationCanceledException">The user cancelled the dialog.</exception>
    public static ExplorerWindow? Choose(IReadOnlyList<ExplorerWindow> windows, string path)
    {
        // Only effective if the host application has not created any window yet; harmless otherwise.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();

        using var dialog = new WindowPickerDialog(windows, path);
        return dialog.ShowDialog() switch
        {
            DialogResult.OK when dialog._selected is not null => dialog._selected,
            DialogResult.Yes => null,
            _ => throw new OperationCanceledException("Window selection cancelled."),
        };
    }

    private List<ExplorerWindow> SelectedWindows =>
        _windowList.SelectedItems.Cast<ListViewItem>().Select(i => (ExplorerWindow)i.Tag!).ToList();

    private static ListView CreateListView(bool multiSelect) => new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = multiSelect,
        HideSelection = false,
        ShowItemToolTips = true,
        Dock = DockStyle.Fill,
    };

    private void PopulateWindows(IReadOnlyList<ExplorerWindow> windows, IReadOnlyCollection<nint> selectHandles)
    {
        _windowList.BeginUpdate();
        _windowList.Items.Clear();
        foreach (ExplorerWindow window in windows)
        {
            var item = new ListViewItem(window.Index.ToString()) { Tag = window };
            item.SubItems.Add(window.Title);
            item.SubItems.Add(ExplorerFormat.OpenedAt(window.OpenedAt));
            item.SubItems.Add(ExplorerFormat.Age(window.Age));
            item.SubItems.Add(window.Tabs.Count.ToString());
            item.SubItems.Add($"0x{window.Handle:X}");
            item.Selected = selectHandles.Contains(window.Handle);
            _windowList.Items.Add(item);
        }

        if (_windowList.SelectedItems.Count == 0 && _windowList.Items.Count > 0)
        {
            _windowList.Items[0].Selected = true;
        }
        if (_windowList.SelectedItems.Count > 0)
        {
            _windowList.SelectedItems[0].Focused = true;
            _windowList.SelectedItems[0].EnsureVisible();
        }
        _windowList.EndUpdate();

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        List<ExplorerWindow> selected = SelectedWindows;
        _openButton.Enabled = selected.Count == 1;
        _closeWindowsButton.Enabled = selected.Count > 0;
        _closeWindowsButton.Text = selected.Count > 1 ? $"{selected.Count} Fenster &schließen" : "Fenster &schließen";

        _tabList.BeginUpdate();
        _tabList.Items.Clear();
        _tabHeader.Text = selected.Count switch
        {
            0 => _windowList.Items.Count == 0 ? "Kein Explorer-Fenster mehr offen." : "Tabs: kein Fenster ausgewählt",
            1 => $"Tabs in Fenster {selected[0].Index}  ({selected[0].Tabs.Count}):",
            _ => $"Tabs in {selected.Count} ausgewählten Fenstern  ({selected.Sum(w => w.Tabs.Count)}) – zum Öffnen genau ein Fenster wählen:",
        };
        foreach (ExplorerWindow window in selected)
        {
            for (int i = 0; i < window.Tabs.Count; i++)
            {
                ExplorerTab tab = window.Tabs[i];
                var item = new ListViewItem(window.Index.ToString()) { ToolTipText = tab.Location };
                item.SubItems.Add((i + 1).ToString());
                item.SubItems.Add(tab.Name);
                item.SubItems.Add(tab.Location);
                _tabList.Items.Add(item);
            }
        }
        _tabList.EndUpdate();
    }

    private void AcceptIfSingleSelection()
    {
        if (SelectedWindows.Count == 1)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void CloseSelectedWindows()
    {
        List<ExplorerWindow> selected = SelectedWindows;
        if (selected.Count == 0)
        {
            return;
        }

        int tabCount = selected.Sum(w => w.Tabs.Count);
        string question = selected.Count == 1
            ? $"Explorer-Fenster {selected[0].Index} \"{selected[0].Title}\" mit {tabCount} Tab(s) schließen?"
            : $"{selected.Count} Explorer-Fenster mit insgesamt {tabCount} Tabs schließen?"
              + Environment.NewLine + Environment.NewLine
              + string.Join(Environment.NewLine, selected.Select(w => $"  {w.Index}: {w.Title}"));
        if (MessageBox.Show(this, question, "Explorer-Fenster schließen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        var failed = new List<string>();
        UseWaitCursor = true;
        try
        {
            foreach (ExplorerWindow window in selected)
            {
                try
                {
                    if (!Explorer.CloseWindow(window))
                    {
                        failed.Add($"  {window.Index}: {window.Title} (reagiert nicht)");
                    }
                }
                catch (ExplorerWindowNotFoundException)
                {
                    // Already closed in the meantime.
                }
            }
        }
        finally
        {
            UseWaitCursor = false;
        }

        Reload();

        if (failed.Count > 0)
        {
            MessageBox.Show(this,
                "Folgende Fenster konnten nicht geschlossen werden:" + Environment.NewLine + string.Join(Environment.NewLine, failed),
                "Explorer-Fenster schließen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Reload()
    {
        var selectedHandles = SelectedWindows.Select(w => w.Handle).ToHashSet();
        PopulateWindows(Explorer.GetWindows(), selectedHandles);
        Activate();
    }
}
