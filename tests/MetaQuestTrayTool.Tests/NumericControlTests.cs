using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using MetaQuestTrayTool.Views;

namespace MetaQuestTrayTool.Tests;

public class NumericControlTests
{
    [Fact]
    public void CustomAndInheritedValuesSurviveSelectionWithoutDuplicateEntries()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var box = new ComboBox();
                box.Items.Add(new ComboBoxItem { Content = "Inherit", Tag = null });
                box.Items.Add(new ComboBoxItem { Content = "Default", Tag = 0 });
                foreach (var value in new object[] { 450, 3456, -25, 1.25 })
                {
                    NumericControlSelection.Select(box, value);
                    Assert.Equal(value, ((ComboBoxItem)box.SelectedItem).Tag);
                    var count = box.Items.Count;
                    NumericControlSelection.Select(box, value);
                    Assert.Equal(count, box.Items.Count);
                }
                NumericControlSelection.Select(box, null);
                Assert.Equal(0, box.SelectedIndex);
                NumericControlSelection.Select(box, 0);
                Assert.Equal(1, box.SelectedIndex);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
