using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

var path = @"Management.Presentation\Views\Shell\DashboardView.xaml";
var lines = File.ReadAllLines(path).ToList();

int opsStart = -1, opsEnd = -1, bizStart = -1, bizEnd = -1;

for (int i=0; i<lines.Count; i++) {
    if (lines[i].Contains("<!-- OPERATIONS VIEW (Original) -->")) opsStart = i + 1;
    if (lines[i].Contains("<!-- BUSINESS VIEW -->")) { opsEnd = i - 1; }
    if (lines[i].Contains("<ScrollViewer Grid.Row=\"1\" Grid.RowSpan=\"2\" VerticalScrollBarVisibility=\"Auto\">")) bizStart = i;
}
bizEnd = lines.Count - 4; // Assuming last 3 lines are </Grid> </Border> </UserControl> etc.

Console.WriteLine($"Ops: {opsStart} to {opsEnd}");
Console.WriteLine($"Biz: {bizStart} to {bizEnd}");

var opsLines = lines.Skip(opsStart).Take(opsEnd - opsStart + 1).ToList();
var bizLines = lines.Skip(bizStart).Take(bizEnd - bizStart + 1).ToList();

var namespaces = @"<UserControl x:Class=""Management.Presentation.Views.Dashboard.{0}""
             xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
             xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
             xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006"" 
             xmlns:d=""http://schemas.microsoft.com/expression/blend/2008"" 
             xmlns:lvc=""clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF""
             xmlns:converters=""clr-namespace:Management.Presentation.Converters""
             xmlns:dash=""clr-namespace:Management.Presentation.Views.Dashboard.Components""
             mc:Ignorable=""d"" 
             d:DesignHeight=""800"" d:DesignWidth=""1200"">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key=""BoolToVis""/>
    </UserControl.Resources>";

File.WriteAllText(@"Management.Presentation\Views\Dashboard\OperationsDashboardView.xaml", namespaces.Replace("{0}", "OperationsDashboardView") + "\n" + string.Join("\n", opsLines) + "\n</UserControl>");
File.WriteAllText(@"Management.Presentation\Views\Dashboard\BusinessDashboardView.xaml", namespaces.Replace("{0}", "BusinessDashboardView") + "\n" + string.Join("\n", bizLines) + "\n</UserControl>");

var codeBehind = @"using System.Windows.Controls;

namespace Management.Presentation.Views.Dashboard
{
    public partial class {0} : UserControl
    {
        public {0}()
        {
            InitializeComponent();
        }
    }
}";

File.WriteAllText(@"Management.Presentation\Views\Dashboard\OperationsDashboardView.xaml.cs", codeBehind.Replace("{0}", "OperationsDashboardView"));
File.WriteAllText(@"Management.Presentation\Views\Dashboard\BusinessDashboardView.xaml.cs", codeBehind.Replace("{0}", "BusinessDashboardView"));

var mainLines = lines.Take(opsStart).ToList();
mainLines.Add(@"        <ContentControl Grid.Row=""1"" Grid.RowSpan=""2"" Focusable=""False"">
            <ContentControl.Style>
                <Style TargetType=""ContentControl"">
                    <Setter Property=""Content"">
                        <Setter.Value>
                            <dash:OperationsDashboardView />
                        </Setter.Value>
                    </Setter>
                    <Style.Triggers>
                        <DataTrigger Binding=""{Binding IsBusinessMode}"" Value=""True"">
                            <Setter Property=""Content"">
                                <Setter.Value>
                                    <dash:BusinessDashboardView />
                                </Setter.Value>
                            </Setter>
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </ContentControl.Style>
        </ContentControl>");
mainLines.AddRange(lines.Skip(bizEnd + 1));
File.WriteAllText(path, string.Join("\n", mainLines));

Console.WriteLine("Done!");
