using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Management.Presentation.Controls
{
    public class TransitioningContentControl : ContentControl
    {
        private ContentPresenter? _mainPresenter;
        private ContentPresenter? _paintArea;

        static TransitioningContentControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TransitioningContentControl), new FrameworkPropertyMetadata(typeof(TransitioningContentControl)));
        }

        public override void OnApplyTemplate()
        {
            _mainPresenter = GetTemplateChild("PART_MainPresenter") as ContentPresenter;
            _paintArea = GetTemplateChild("PART_PaintArea") as ContentPresenter;
            base.OnApplyTemplate();
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
            StartTransition(oldContent, newContent);
        }

        private void StartTransition(object oldContent, object newContent)
        {
            if (_mainPresenter == null || _paintArea == null) return;

            // Snapshot old content
            _paintArea.Content = oldContent;
            
            // Animate
            // Simple Slide Up and Fade In
            var sb = new Storyboard();
            
            // 1. Fade old out
            // _paintArea.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)));
            
            // 2. Slide new in
             var slide = new DoubleAnimation
            {
                From = 30,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            var translate = new TranslateTransform();
            _mainPresenter.RenderTransform = translate;
            
            // Apply animations
            translate.BeginAnimation(TranslateTransform.YProperty, slide);
            _mainPresenter.BeginAnimation(OpacityProperty, fade);
            
            // Hide PaintArea after short delay
            // Simplify: Just animate the new content in. 
            // Ideally we cross-fade, but without complex visualbrush logic, just Animate In is cleaner for now.
        }
    }
}
