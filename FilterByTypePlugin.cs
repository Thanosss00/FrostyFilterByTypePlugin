using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Managers;
using HarmonyLib;

namespace FilterByTypePlugin
{
    public sealed class FilterByTypePlugin : ExecutionAction
    {
        private const string HarmonyId = "thanos.frosty.filterbytypeplugin";
        private static bool s_initialized;

        public FilterByTypePlugin()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(Initialize));
        }

        private static void Initialize()
        {
            if (s_initialized) return;
            try
            {
                new Harmony(HarmonyId).PatchAll(typeof(FilterByTypePlugin).Assembly);
                s_initialized = true;

                foreach (Window window in Application.Current.Windows)
                {
                    if (window.FindName("dataExplorer") is FrostyDataExplorer explorer)
                        DataExplorerFilterPatch.Attach(explorer);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log("Filter By Type initialization failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(FrostyDataExplorer), "OnApplyTemplate")]
    internal static class DataExplorerFilterPatch
    {
        private const string ButtonName = "FilterByTypeButton";
        private const string AnyType = "(Any Type)"; // See every file by pressing this or just delete the type:Asset from the search bar

        [HarmonyPostfix]
        private static void AfterApplyTemplate(FrostyDataExplorer __instance)
        {
            Attach(__instance);
        }

        internal static void Attach(FrostyDataExplorer explorer)
        {
            if (explorer?.Template == null) return;
            var modified = explorer.Template.FindName("PART_ShowOnlyModifiedCheckBox", explorer) as CheckBox;
            var search = explorer.Template.FindName("PART_FilterTextBox", explorer) as TextBox;
            if (modified == null || search == null || !(modified.Parent is Panel toolbar)) return;
            if (toolbar.Children.OfType<Button>().Any(existingButton => existingButton.Name == ButtonName)) return;
            var button = new Button
            {
                Name = ButtonName,
                Content = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(0, 0), new Point(16, 0),
                        new Point(9.5, 8), new Point(9.5, 15),
                        new Point(6.5, 13), new Point(6.5, 8)
                    },
                    Fill = Brushes.White,
                    Width = 16,
                    Height = 15,
                    Stretch = Stretch.Uniform
                },
                Width = 26,
                Height = 20,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Filter By Type"
            };

            var list = new ListBox
            {
                MaxHeight = 420,
                Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            var popup = new Popup
            {
                PlacementTarget = button,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                Child = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Child = list
                }
            };

            button.Click += (s, e) =>
            {
                if (!popup.IsOpen)
                    list.ItemsSource = GetAvailableTypes(explorer.ItemsSource);

                popup.IsOpen = !popup.IsOpen;
            };

            list.SelectionChanged += (s, e) =>
            {
                if (!(list.SelectedItem is string type)) return;
                bool anyType = type == AnyType;
                search.Text = anyType ? "" : "type:" + type;
                search.CaretIndex = search.Text.Length;
                search.Focus();
                Keyboard.ClearFocus();
                button.ToolTip = anyType ? "Filter By Type" : "Filter: " + type;
                popup.IsOpen = false;
            };
            toolbar.Children.Insert(toolbar.Children.IndexOf(modified) + 1, button);
        }

        // Get every type from the loaded assets instead of making a big ass list with every type -_-
        private static List<string> GetAvailableTypes(IEnumerable items)
        {
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (items != null)
            {
                foreach (AssetEntry entry in items)
                {
                    if (!string.IsNullOrWhiteSpace(entry?.Type))
                        types.Add(entry.Type);
                }
            }

            var result = types.OrderBy(type => type, StringComparer.CurrentCultureIgnoreCase).ToList();
            result.Insert(0, AnyType);
            return result;
        }
    }
}