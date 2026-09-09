using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;

namespace MetaQuestTrayTool.Tests;

public class HotKeysLayoutTests
{
    [Theory]
    [InlineData(600, 390, 13)]
    [InlineData(720, 630, 60)]
    public void BindingEditorRemainsReachableWithDefaultAndLongBindingLists(double width, double height, int count)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Exercise the production layout without starting App, registering global
                // shortcuts, or reading/writing the user's settings. Omit event handlers
                // and theme resources; retain all layout properties and named controls.
                using var source = typeof(HotKeysLayoutTests).Assembly.GetManifestResourceStream("HotKeysLayout.xaml")!;
                var xml = XDocument.Load(source);
                var root = xml.Root!;
                root.Name = root.Name.Namespace + "UserControl";
                foreach (var attr in root.Attributes().Where(a => !a.IsNamespaceDeclaration).ToList())
                    attr.Remove();
                foreach (var attr in root.Descendants().Attributes().Where(a =>
                             a.Name.LocalName is "Click" or "Checked" or "Unchecked" or "SelectionChanged"
                             || a.Value.StartsWith("{StaticResource", StringComparison.Ordinal)).ToList())
                    attr.Remove();

                var control = (UserControl)XamlReader.Parse(xml.ToString());
                var panel = (Border)control.FindName("RecordPanel");
                panel.Visibility = Visibility.Visible;
                var list = (ListView)control.FindName("BindingsList");
                list.ItemsSource = Enumerable.Range(0, count).Select(i => new { ActionLabel = $"Action {i}", ChordLabel = "Ctrl+Num 1" });
                control.Measure(new Size(width, height));
                control.Arrange(new Rect(0, 0, width, height));
                control.UpdateLayout();

                var dock = (DockPanel)control.Content;
                var scroller = dock.Children.OfType<ScrollViewer>().SingleOrDefault();
                scroller?.ScrollToBottom();
                control.UpdateLayout();
                var record = (Button)control.FindName("RecordButton");
                FrameworkElement viewport = scroller is null ? control : scroller;
                var bounds = record.TransformToAncestor(viewport)
                    .TransformBounds(new Rect(record.RenderSize));
                Assert.True(record.ActualHeight > 0);
                Assert.True(bounds.Top >= 0 && bounds.Bottom <= viewport.ActualHeight,
                    $"Record button {bounds} is outside viewport height {viewport.ActualHeight}.");
                if (scroller is not null)
                {
                    Assert.True(list.ActualHeight < scroller.ViewportHeight);
                    if (height < 400) Assert.True(scroller.ScrollableHeight > 0);
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
