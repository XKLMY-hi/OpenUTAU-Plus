using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Vst;

namespace OpenUtau.App.Views {
    public partial class VstRackWindow : Window {
        private readonly UTrack track;

        public VstRackWindow() : this(new UTrack()) { }
        public VstRackWindow(UTrack track) {
            InitializeComponent();
            this.track = track;
            TitleLabel.Text = $"{track.TrackName} — VST Rack";
            if (track.VstSlots == null || track.VstSlots.Count == 0)
                track.VstSlots = VstPluginManager.CreateDefaultSlots(3);
            BuildSlotList();
        }

        private void BuildSlotList() {
            SlotList.Children.Clear();

            foreach (var slot in track.VstSlots) {
                var row = new Border { Classes = { "slotRow" } };
                var grid = new Grid { ColumnDefinitions = new("32,Auto,*,Auto") };

                var num = new TextBlock { Classes = { "slotNum" }, Text = $"{slot.SlotIndex + 1}" };
                grid.Children.Add(num); Grid.SetColumn(num, 0);

                if (slot.IsLoaded) {
                    var typeBadge = new TextBlock {
                        Text = "[VST]", FontSize = 9, VerticalAlignment = VerticalAlignment.Center,
                        Opacity = 0.4, Margin = new Thickness(0, 0, 6, 0),
                    };
                    grid.Children.Add(typeBadge); Grid.SetColumn(typeBadge, 1);

                    var name = new TextBlock { Classes = { "pluginName" }, Text = slot.DisplayName };
                    grid.Children.Add(name); Grid.SetColumn(name, 2);

                    var remove = new Button { Classes = { "removeBtn" } };
                    var shot = slot;
                    remove.Click += (s, e) => { shot.Clear(); BuildSlotList(); };
                    grid.Children.Add(remove); Grid.SetColumn(remove, 3);
                } else {
                    var empty = new TextBlock { Classes = { "emptyName" }, Text = "Empty slot" };
                    grid.Children.Add(empty); Grid.SetColumn(empty, 1);
                    Grid.SetColumnSpan(empty, 2);

                    var add = new Button { Classes = { "addBtn" } };
                    var shot = slot;
                    add.Click += (s, e) => AddPluginToSlot(shot);
                    grid.Children.Add(add); Grid.SetColumn(add, 3);
                }

                row.Child = grid;
                SlotList.Children.Add(row);
            }

            if (track.VstSlots.Count < 8) {
                var addSlot = new Button {
                    Classes = { "addSlotBtn" },
                    Content = "+ Add slot",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 4, 0, 0),
                };
                addSlot.Click += (s, e) => {
                    track.VstSlots.Add(new VstPluginSlot(track.VstSlots.Count));
                    BuildSlotList();
                };
                SlotList.Children.Add(addSlot);
            }
        }

        private void AddPluginToSlot(VstPluginSlot slot) {
            VstPluginManager.Inst.ScanPlugins();
            var plugins = VstPluginManager.Inst.KnownPlugins.Values.OrderBy(p => p.PluginName).ToList();

            if (plugins.Count == 0) {
                var msg = new Window { Title = "No VST Plugins Found", Width = 380, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                var sp = new StackPanel { Margin = new Thickness(16) };
                sp.Children.Add(new TextBlock {
                    Text = "No VST/VST3 plugins found.\n\nAdd scan paths in:\nTools → Settings → OpenUTAU Plus",
                    TextWrapping = TextWrapping.Wrap, FontSize = 12,
                });
                var ok = new Button { Content = "OK", Width = 60, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
                ok.Click += (s, e) => msg.Close();
                sp.Children.Add(ok);
                msg.Content = new Border { Child = sp };
                msg.ShowDialog(this);
                return;
            }

            var picker = new Window {
                Title = "Select Plugin", Width = 440, Height = 340,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = this.Background,
            };
            var layout = new StackPanel { Margin = new Thickness(12) };
            var header = new TextBlock {
                Text = $"{plugins.Count} plugin(s) available", FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8), Opacity = 0.7,
            };
            layout.Children.Add(header);
            var listBox = new ListBox { ItemsSource = plugins, Height = 220 };
            layout.Children.Add(listBox);

            var buttons = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0), Spacing = 8,
            };
            var load = new Button { Content = "Load", Width = 64 };
            var cancel = new Button { Content = "Cancel", Width = 64 };
            buttons.Children.Add(load); buttons.Children.Add(cancel);
            layout.Children.Add(buttons);
            picker.Content = new Border { Child = layout };

            load.Click += (s, e) => {
                if (listBox.SelectedItem is VstPluginInfo sel) {
                    slot.PluginUid = sel.PluginUid;
                    VstPluginManager.Inst.LoadPlugin(slot);
                    BuildSlotList();
                }
                picker.Close();
            };
            cancel.Click += (s, e) => picker.Close();
            picker.ShowDialog(this);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
        }
    }
}
