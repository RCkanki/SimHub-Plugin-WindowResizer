using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SimHub.Plugins.Styles;

namespace WindowResizer
{
    /// <summary>
    /// Simple WPF control shown in SimHub's settings panel (profile list and editor).
    /// Font sizes are inherited from SimHub (no local FontSize on the root). Plain <see cref="TextBox"/> / <see cref="ComboBox"/>
    /// pick up the host app's implicit styles; SimHub.Plugins does not ship SHTextBox-style wrappers.
    /// </summary>
    public class SettingsControl : UserControl
    {
        private readonly ProfileManager _profiles;
        private readonly SHListBox _listBox;
        private readonly TextBox _nameBox;
        private readonly TextBox _processNameBox;
        private readonly TextBox _titleContainsBox;
        // private readonly TextBox _classContainsBox; // WindowClassContains unused
        private readonly TextBox _xBox;
        private readonly TextBox _yBox;
        private readonly TextBox _widthBox;
        private readonly TextBox _heightBox;
        private readonly CheckBox _removeBordersCheck;
        private readonly CheckBox _bringToFrontCheck;
        private readonly CheckBox _bringToFrontFocusCheck;
        private readonly CheckBox _includeInCycleCheck;
        private readonly CheckBox _autoApplyCheck;
        private readonly TextBox _autoDelayMsBox;
        private readonly SHButtonPrimary _newProfileButton;
        private readonly SHButtonSecondary _deleteButton;
        private readonly SHButtonPrimary _saveButton;
        private readonly SHButtonSecondary _captureButton;
        private readonly ComboBox _windowPicker;
        private readonly SHButtonSecondary _windowRefreshButton;
        private WindowManager.WindowInfo[] _currentWindows = Array.Empty<WindowManager.WindowInfo>();
        /// <summary>Avoid overwriting profile Name when the combo is filled or selection is set from <see cref="RefreshWindowPicker"/>.</summary>
        private bool _suppressProfileNameFromWindowPicker;

        public SettingsControl(ProfileManager profiles)
        {
            _profiles = profiles;

            // Root layout: no outer SHSection — SimHub already shows the page title; SHSection kept empty space below it.
            var rootGrid = new Grid
            {
                Margin = new Thickness(8, 0, 8, 8)
            };

            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left: profile list with NEW PROFILE directly underneath; panel top-aligned in the section.
            var leftPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top
            };

            _listBox = new SHListBox
            {
                MinWidth = 120,
                MinHeight = 120,
                MaxHeight = 480
            };
            _listBox.SelectionChanged += ListBox_SelectionChanged;

            // Row template: name on the left, Apply (▶) on the right (DockPanel).
            var itemRoot = new FrameworkElementFactory(typeof(DockPanel));
            itemRoot.SetValue(DockPanel.LastChildFillProperty, true);

            var applyButtonFactory = new FrameworkElementFactory(typeof(SHButtonSecondary));
            applyButtonFactory.SetValue(Button.ContentProperty, "▶");
            applyButtonFactory.SetValue(Button.PaddingProperty, new Thickness(4, 0, 4, 0));
            applyButtonFactory.SetValue(Button.ToolTipProperty, "Apply this profile");
            applyButtonFactory.SetValue(DockPanel.DockProperty, Dock.Right);
            applyButtonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(ProfileRowApplyButton_Click));
            itemRoot.AppendChild(applyButtonFactory);

            var nameTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameTextFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            nameTextFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 4, 0));
            itemRoot.AppendChild(nameTextFactory);

            var itemTemplate = new DataTemplate
            {
                VisualTree = itemRoot
            };
            _listBox.ItemTemplate = itemTemplate;

            _newProfileButton = new SHButtonPrimary
            {
                Content = "NEW PROFILE",
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };
            _newProfileButton.Click += NewProfileButton_Click;

            leftPanel.Children.Add(_listBox);
            leftPanel.Children.Add(_newProfileButton);

            var leftSubSection = new SHSubSection
            {
                Title = "Profiles",
                Content = leftPanel
            };
            Grid.SetColumn(leftSubSection, 0);

            // Right: profile detail form
            var rightGrid = new Grid
            {
                Margin = new Thickness(12, 0, 0, 0)
            };

            for (int i = 0; i < 8; i++)
            {
                rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Shared helper: label, description, and control in a two-column row.
            FrameworkElement CreateLabeledControl(string label, string description, UIElement control, int row)
            {
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var labelPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                var text = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                labelPanel.Children.Add(text);

                if (!string.IsNullOrEmpty(description))
                {
                    labelPanel.Children.Add(new TextBlock
                    {
                        Text = description,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = SystemColors.GrayTextBrush
                    });
                }

                Grid.SetColumn(labelPanel, 0);
                Grid.SetColumn(control, 1);

                grid.Children.Add(labelPanel);
                grid.Children.Add(control);

                Grid.SetRow(grid, row);
                rightGrid.Children.Add(grid);

                return grid;
            }

            _nameBox = new TextBox();
            _processNameBox = new TextBox();
            _titleContainsBox = new TextBox();
            // _classContainsBox = new TextBox(); // WindowClassContains unused
            _xBox = new TextBox();
            _yBox = new TextBox();
            _widthBox = new TextBox();
            _heightBox = new TextBox();

            _removeBordersCheck = new SHToggleCheckbox { Content = "Remove borders" };
            _bringToFrontCheck = new SHToggleCheckbox { Content = "Bring to front" };
            _bringToFrontFocusCheck = new SHToggleCheckbox { Content = "Bring to front (take focus)" };
            _includeInCycleCheck = new SHToggleCheckbox { Content = "Include in NEXT/PREV cycle" };
            _autoApplyCheck = new SHToggleCheckbox { Content = "Enable auto resize for this window" };
            _autoDelayMsBox = new TextBox { Width = 80, Margin = new Thickness(8, 0, 0, 0) };

            CreateLabeledControl(
                "Name",
                "Display name of this window profile.",
                _nameBox,
                0);
            // Stack process name field and window picker row vertically.
            var procStack = new StackPanel { Orientation = Orientation.Vertical };
            procStack.Children.Add(_processNameBox);

            var pickerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _windowPicker = new ComboBox
            {
                MinWidth = 200
            };
            _windowPicker.SelectionChanged += WindowPicker_SelectionChanged;
            _windowRefreshButton = new SHButtonSecondary
            {
                Content = "Refresh / Use",
                Margin = new Thickness(8, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _windowRefreshButton.Click += WindowRefreshButton_Click;

            pickerRow.Children.Add(_windowPicker);
            pickerRow.Children.Add(_windowRefreshButton);
            procStack.Children.Add(pickerRow);

            CreateLabeledControl(
                "Process name",
                "Executable process name without .exe (e.g. 'discord', 'ac2'). You can also pick from the window list.",
                procStack,
                1);
            CreateLabeledControl(
                "Window title contains",
                "Optional substring filter for the window title.",
                _titleContainsBox,
                2);
            // WindowClassContains unused — former UI:
            // CreateLabeledControl(
            //     "Window class contains",
            //     "Optional substring filter for the native window class name.",
            //     _classContainsBox,
            //     3);

            var posGrid = new Grid();
            posGrid.ColumnDefinitions.Add(new ColumnDefinition());
            posGrid.ColumnDefinitions.Add(new ColumnDefinition());
            posGrid.Margin = new Thickness(0, 2, 0, 2);
            posGrid.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "X:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0)},
                    _xBox
                }
            });
            var yPanel = new StackPanel { Orientation = Orientation.Horizontal };
            yPanel.Children.Add(new TextBlock { Text = "Y:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            yPanel.Children.Add(_yBox);
            Grid.SetColumn(yPanel, 1);
            posGrid.Children.Add(yPanel);
            CreateLabeledControl(
                "Position",
                "Target top-left position in pixels on the primary screen.",
                posGrid,
                3);

            var sizeGrid = new Grid();
            sizeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            sizeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sizeGrid.Margin = new Thickness(0, 2, 0, 2);
            sizeGrid.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "Width:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0)},
                    _widthBox
                }
            });
            var hPanel = new StackPanel { Orientation = Orientation.Horizontal };
            hPanel.Children.Add(new TextBlock { Text = "Height:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            hPanel.Children.Add(_heightBox);
            Grid.SetColumn(hPanel, 1);
            sizeGrid.Children.Add(hPanel);

            var sizeButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _captureButton = new SHButtonSecondary
            {
                Content = "Read",
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _captureButton.Click += CaptureButton_Click;

            sizeButtonsPanel.Children.Add(_captureButton);
            // Right-side Apply removed (row ▶ and list selection are enough).

            Grid.SetColumn(sizeButtonsPanel, 2);
            sizeGrid.Children.Add(sizeButtonsPanel);
            CreateLabeledControl(
                "Size",
                "Target window size in pixels. Use Apply to send current settings.",
                sizeGrid,
                4);

            var flagsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 4, 0, 4)
            };
            flagsPanel.Children.Add(_removeBordersCheck);
            flagsPanel.Children.Add(_bringToFrontCheck);
            flagsPanel.Children.Add(_bringToFrontFocusCheck);
            flagsPanel.Children.Add(_includeInCycleCheck);
            CreateLabeledControl(
                "Flags",
                "Additional behaviors: remove borders, bring window to front, and include in NEXT/PREV cycling.",
                flagsPanel,
                5);

            var autoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            autoPanel.Children.Add(_autoApplyCheck);
            autoPanel.Children.Add(new TextBlock
            {
                Text = "Delay (ms):",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            });
            autoPanel.Children.Add(_autoDelayMsBox);

            CreateLabeledControl(
                "Auto resize",
                "Automatically apply this profile when the target window appears. Delay is the wait time after detection.",
                autoPanel,
                6);

            // Button row
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            _deleteButton = new SHButtonSecondary
            {
                Content = "Delete",
                Margin = new Thickness(0, 0, 8, 0)
            };
            _deleteButton.Click += DeleteButton_Click;

            _saveButton = new SHButtonPrimary
            {
                Content = "Save"
            };
            _saveButton.Click += SaveButton_Click;

            // SHButtonPrimary / SHButtonSecondary use different default templates (padding, min height);
            // align chrome so the row looks consistent.
            UnifySimHubPairButtonHeight(_deleteButton, _saveButton);

            buttonsPanel.Children.Add(_deleteButton);
            buttonsPanel.Children.Add(_saveButton);

            Grid.SetRow(buttonsPanel, 7);
            rightGrid.Children.Add(buttonsPanel);

            var rightSubSection = new SHSubSection
            {
                Title = "Settings",
                Content = rightGrid
            };
            Grid.SetColumn(rightSubSection, 1);

            rootGrid.Children.Add(leftSubSection);
            rootGrid.Children.Add(rightSubSection);

            // SimHub already shows the plugin name (LeftMenuTitle); avoid duplicating it here.
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0),
                Content = rootGrid
            };

            ReloadList();
            if (_listBox.Items.Count > 0)
            {
                _listBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// SimHub styles give primary and secondary buttons different default padding/min heights; set shared values so they line up in one row.
        /// </summary>
        private static void UnifySimHubPairButtonHeight(Button a, Button b)
        {
            const double minH = 34;
            var pad = new Thickness(14, 6, 14, 6);
            a.MinHeight = minH;
            b.MinHeight = minH;
            a.Padding = pad;
            b.Padding = pad;
            a.VerticalAlignment = VerticalAlignment.Center;
            b.VerticalAlignment = VerticalAlignment.Center;
        }

        private void ReloadList()
        {
            _listBox.ItemsSource = null;
            _listBox.ItemsSource = _profiles.Profiles;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = _listBox.SelectedIndex;
            if (index < 0 || index >= _profiles.Profiles.Count) return;

            var p = _profiles.Profiles[index];
            _nameBox.Text = p.Name;
            _processNameBox.Text = p.ProcessName;
            _titleContainsBox.Text = p.WindowTitleContains ?? string.Empty;
            // _classContainsBox.Text = p.WindowClassContains ?? string.Empty;
            _xBox.Text = p.X.ToString();
            _yBox.Text = p.Y.ToString();
            _widthBox.Text = p.Width.ToString();
            _heightBox.Text = p.Height.ToString();
            _removeBordersCheck.IsChecked = p.RemoveBorders;
            _bringToFrontCheck.IsChecked = p.BringToFront;
            _bringToFrontFocusCheck.IsChecked = p.BringToFrontTakeFocus;
            _includeInCycleCheck.IsChecked = p.IncludeInCycle;
            _autoApplyCheck.IsChecked = p.AutoApply;
            _autoDelayMsBox.Text = p.AutoDelayMs.ToString();
        }

        private void NewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = new Profile
            {
                Name = "New Profile",
                ProcessName = string.Empty
            };
            _profiles.Profiles.Add(profile);
            _profiles.Save();
            ReloadList();
            _listBox.SelectedIndex = _profiles.Profiles.Count - 1;
            RefreshWindowPicker();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var index = _listBox.SelectedIndex;
            if (index < 0 || index >= _profiles.Profiles.Count) return;

            _profiles.Profiles.RemoveAt(index);
            _profiles.Save();
            ReloadList();

            if (_listBox.Items.Count > 0)
            {
                _listBox.SelectedIndex = Math.Min(index, _listBox.Items.Count - 1);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var index = _listBox.SelectedIndex;
            if (index < 0 || index >= _profiles.Profiles.Count) return;

            var p = _profiles.Profiles[index];

            p.Name = _nameBox.Text;
            p.ProcessName = _processNameBox.Text;
            p.WindowTitleContains = string.IsNullOrWhiteSpace(_titleContainsBox.Text) ? null : _titleContainsBox.Text;
            // p.WindowClassContains = string.IsNullOrWhiteSpace(_classContainsBox.Text) ? null : _classContainsBox.Text;

            if (!int.TryParse(_xBox.Text, out var x)) x = p.X;
            if (!int.TryParse(_yBox.Text, out var y)) y = p.Y;
            if (!int.TryParse(_widthBox.Text, out var w)) w = p.Width;
            if (!int.TryParse(_heightBox.Text, out var h)) h = p.Height;

            p.X = x;
            p.Y = y;
            p.Width = w;
            p.Height = h;

            p.RemoveBorders = _removeBordersCheck.IsChecked == true;
            p.BringToFront = _bringToFrontCheck.IsChecked == true;
            p.BringToFrontTakeFocus = _bringToFrontFocusCheck.IsChecked == true;
            p.IncludeInCycle = _includeInCycleCheck.IsChecked == true;

            p.AutoApply = _autoApplyCheck.IsChecked == true;
            if (!int.TryParse(_autoDelayMsBox.Text, out var delayMs)) delayMs = p.AutoDelayMs;
            if (delayMs < 0) delayMs = 0;
            p.AutoDelayMs = delayMs;

            _profiles.Save();
            ReloadList();
            _listBox.SelectedIndex = index;
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            // Resolve HWND from the editor fields (not only the saved Profile) so Read works before Save.
            var index = _listBox.SelectedIndex;
            if (index < 0 || index >= _profiles.Profiles.Count) return;

            var stored = _profiles.Profiles[index];
            var procText = (_processNameBox.Text ?? string.Empty).Trim();

            IntPtr hWnd = IntPtr.Zero;
            var pickerIdx = _windowPicker.SelectedIndex;
            if (pickerIdx >= 0 && pickerIdx < _currentWindows.Length &&
                string.Equals(_currentWindows[pickerIdx].ProcessName, procText, StringComparison.OrdinalIgnoreCase) &&
                WindowManager.IsWindowAlive(_currentWindows[pickerIdx].Handle))
            {
                hWnd = _currentWindows[pickerIdx].Handle;
            }
            else
            {
                var lookup = new Profile
                {
                    ProcessName = procText,
                    WindowTitleContains = string.IsNullOrWhiteSpace(_titleContainsBox.Text) ? null : _titleContainsBox.Text
                };
                if (!int.TryParse(_xBox.Text, out var lx)) lx = stored.X;
                if (!int.TryParse(_yBox.Text, out var ly)) ly = stored.Y;
                if (!int.TryParse(_widthBox.Text, out var lw)) lw = stored.Width;
                if (!int.TryParse(_heightBox.Text, out var lh)) lh = stored.Height;
                lookup.X = lx;
                lookup.Y = ly;
                lookup.Width = lw;
                lookup.Height = lh;
                hWnd = WindowManager.FindWindowForProfile(lookup);
            }

            if (!WindowManager.TryGetWindowRect(hWnd, out var x, out var y, out var w, out var h))
            {
                return;
            }

            _xBox.Text = x.ToString();
            _yBox.Text = y.ToString();
            _widthBox.Text = w.ToString();
            _heightBox.Text = h.ToString();
        }

        private void ProfileRowApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var p = btn.DataContext as Profile;
            if (p == null) return;

            var index = _profiles.Profiles.IndexOf(p);
            if (index < 0) return;

            _listBox.SelectedIndex = index;

            // Push current form values into the profile, then apply.
            p.Name = _nameBox.Text;
            p.ProcessName = _processNameBox.Text;
            p.WindowTitleContains = string.IsNullOrWhiteSpace(_titleContainsBox.Text) ? null : _titleContainsBox.Text;
            // p.WindowClassContains = string.IsNullOrWhiteSpace(_classContainsBox.Text) ? null : _classContainsBox.Text;

            if (!int.TryParse(_xBox.Text, out var x)) x = p.X;
            if (!int.TryParse(_yBox.Text, out var y)) y = p.Y;
            if (!int.TryParse(_widthBox.Text, out var w)) w = p.Width;
            if (!int.TryParse(_heightBox.Text, out var h)) h = p.Height;

            p.X = x;
            p.Y = y;
            p.Width = w;
            p.Height = h;

            p.RemoveBorders = _removeBordersCheck.IsChecked == true;
            p.BringToFront = _bringToFrontCheck.IsChecked == true;
            p.BringToFrontTakeFocus = _bringToFrontFocusCheck.IsChecked == true;
            p.IncludeInCycle = _includeInCycleCheck.IsChecked == true;

            p.AutoApply = _autoApplyCheck.IsChecked == true;
            if (!int.TryParse(_autoDelayMsBox.Text, out var delayMs)) delayMs = p.AutoDelayMs;
            if (delayMs < 0) delayMs = 0;
            p.AutoDelayMs = delayMs;

            _profiles.TouchProfilesRevision();
            // Same apply path as runtime (process-name based lookup).
            _profiles.ApplyProfile(p);
        }

        /// <summary>
        /// Refreshes the top-level window list; selection is applied to the profile in <see cref="WindowPicker_SelectionChanged"/>.
        /// Similar to Resize Raccoon's dropdown picker.
        /// </summary>
        private void WindowRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindowPicker();
        }

        /// <summary>
        /// Refills the window combo from <see cref="WindowManager.EnumerateWindows"/>; first item selects and updates process/title fields.
        /// </summary>
        private void RefreshWindowPicker()
        {
            _suppressProfileNameFromWindowPicker = true;
            try
            {
                _currentWindows = WindowManager.EnumerateWindows();
                _windowPicker.Items.Clear();
                foreach (var w in _currentWindows)
                {
                    _windowPicker.Items.Add(w.ToString());
                }

                if (_windowPicker.Items.Count == 0)
                {
                    return;
                }

                if (_windowPicker.SelectedIndex < 0)
                {
                    _windowPicker.SelectedIndex = 0;
                }
            }
            finally
            {
                _suppressProfileNameFromWindowPicker = false;
            }
        }

        /// <summary>
        /// Copies the picked window into the current profile editor fields.
        /// </summary>
        private void WindowPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var idx = _windowPicker.SelectedIndex;
            if (idx < 0 || idx >= _currentWindows.Length) return;

            var win = _currentWindows[idx];
            _processNameBox.Text = win.ProcessName;
            _titleContainsBox.Text = win.Title;
            if (!_suppressProfileNameFromWindowPicker)
            {
                _nameBox.Text = win.ProcessName;
            }
            // _classContainsBox.Text = win.ClassName; // WindowClassContains unused
        }
    }
}

