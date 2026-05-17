using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Management.Presentation.Controls.Premium
{
    public class StaggeredTextRevealControl : ContentControl
    {
        private WrapPanel _wrapPanel;
        private List<TextBlock> _wordBlocks = new List<TextBlock>();

        public StaggeredTextRevealControl()
        {
            _wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            this.Content = _wrapPanel;
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(StaggeredTextRevealControl), new PropertyMetadata(string.Empty, OnTextChanged));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(StaggeredTextRevealControl), new PropertyMetadata(false, OnIsSelectedChanged));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        // OnApplyTemplate removed since we build it in constructor.

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StaggeredTextRevealControl control)
            {
                control.RebuildText();
            }
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StaggeredTextRevealControl control)
            {
                bool isSelected = (bool)e.NewValue;
                if (isSelected)
                {
                    control.AnimateEntrance();
                }
                else
                {
                    control.AnimateExit();
                }
            }
        }

        private void RebuildText()
        {
            if (_wrapPanel == null) return;
            _wrapPanel.Children.Clear();
            _wordBlocks.Clear();

            if (string.IsNullOrEmpty(Text)) return;

            string[] words = Text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var tb = new TextBlock
                {
                    Text = word + " ",
                    Opacity = IsSelected ? 1 : 0,
                    RenderTransform = new TranslateTransform { Y = IsSelected ? 0 : 16 },
                    FontFamily = this.FontFamily,
                    FontSize = this.FontSize,
                    FontWeight = this.FontWeight,
                    FontStyle = this.FontStyle,
                    Foreground = this.Foreground,
                    TextWrapping = TextWrapping.NoWrap
                };
                
                _wrapPanel.Children.Add(tb);
                _wordBlocks.Add(tb);
            }
        }

        private async void AnimateEntrance()
        {
            if (_wordBlocks.Count == 0) return;

            // Wait for exit to clear if this was recently triggered
            await Task.Delay(80);

            var sb = new Storyboard();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            for (int i = 0; i < _wordBlocks.Count; i++)
            {
                var tb = _wordBlocks[i];
                if (tb.RenderTransform is not TranslateTransform)
                    tb.RenderTransform = new TranslateTransform();

                var opAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
                {
                    BeginTime = TimeSpan.FromMilliseconds(i * 55),
                    EasingFunction = easeOut
                };

                var transAnim = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(350))
                {
                    BeginTime = TimeSpan.FromMilliseconds(i * 55),
                    EasingFunction = easeOut
                };

                Storyboard.SetTarget(opAnim, tb);
                Storyboard.SetTargetProperty(opAnim, new PropertyPath("Opacity"));

                Storyboard.SetTarget(transAnim, tb);
                Storyboard.SetTargetProperty(transAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                sb.Children.Add(opAnim);
                sb.Children.Add(transAnim);
            }

            sb.Begin();
        }

        private void AnimateExit()
        {
            if (_wordBlocks.Count == 0) return;

            // Calculate lines based on layout Y positions
            // We force a measure/arrange just in case, but usually they are already laid out.
            _wrapPanel.UpdateLayout();

            var lineGroups = _wordBlocks
                .GroupBy(tb => 
                {
                    var point = tb.TransformToAncestor(_wrapPanel).Transform(new Point(0, 0));
                    return Math.Round(point.Y);
                })
                .OrderBy(g => g.Key)
                .ToList();

            var sb = new Storyboard();
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            for (int lineIndex = 0; lineIndex < lineGroups.Count; lineIndex++)
            {
                var group = lineGroups[lineIndex];
                foreach (var tb in group)
                {
                    if (tb.RenderTransform is not TranslateTransform)
                        tb.RenderTransform = new TranslateTransform();

                    var opAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(lineIndex * 40),
                        EasingFunction = easeIn
                    };

                    var transAnim = new DoubleAnimation(0, -16, TimeSpan.FromMilliseconds(220))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(lineIndex * 40),
                        EasingFunction = easeIn
                    };

                    Storyboard.SetTarget(opAnim, tb);
                    Storyboard.SetTargetProperty(opAnim, new PropertyPath("Opacity"));

                    Storyboard.SetTarget(transAnim, tb);
                    Storyboard.SetTargetProperty(transAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                    sb.Children.Add(opAnim);
                    sb.Children.Add(transAnim);
                }
            }

            sb.Begin();
        }
    }
}
