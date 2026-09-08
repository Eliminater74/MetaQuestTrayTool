using System.Globalization;
using System.Windows.Controls;

namespace MetaQuestTrayTool.Views;

/// <summary>Preserve saved/imported numeric values even when they are outside the preset catalog.</summary>
public static class NumericControlSelection
{
    public static void Select(System.Windows.Controls.ComboBox box, object? value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, value)
                || (item.Tag is double a && value is double b && Math.Abs(a - b) < 0.001)
                || (item.Tag is string s && value is string t && string.Equals(s, t, StringComparison.OrdinalIgnoreCase)))
            {
                box.SelectedItem = item;
                return;
            }
        }

        if (value is int || value is double number && double.IsFinite(number))
        {
            var item = new ComboBoxItem { Content = Convert.ToString(value, CultureInfo.InvariantCulture), Tag = value };
            box.Items.Add(item);
            box.SelectedItem = item;
        }
        else if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }
}
