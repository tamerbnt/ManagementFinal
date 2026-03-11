using System;
using System.Windows;
using System.Windows.Media;

namespace Management.Presentation.Services.Infrastructure
{
    public interface IPerformanceService
    {
        bool IsLowFxMode { get; }
        bool IsEcoMode => IsLowFxMode;
        event EventHandler ModeChanged;
    }

    public class PerformanceService : IPerformanceService
    {
        public bool IsLowFxMode { get; private set; }
        public bool IsEcoMode => IsLowFxMode;
        public event EventHandler? ModeChanged;

        public PerformanceService()
        {
            DetectCapabilities();
            SystemParameters.StaticPropertyChanged += (s, e) => DetectCapabilities();
        }

        private void DetectCapabilities()
        {
            // 1. Check Hardware Acceleration Tier (0, 1, or 2)
            // Tier 0: No graphics hardware acceleration.
            // Tier 1: Partial hardware acceleration.
            // Tier 2: Acceleration that matches or exceeds the Direct3D 9.0 version.
            int renderingTier = RenderCapability.Tier >> 16;
            bool isSoftwareRendering = renderingTier < 2;

            // 2. Check High Contrast (Accessibility)
            bool isHighContrast = SystemParameters.HighContrast;

            // 3. Check Remote Desktop (RDP)
            bool isRemoteSession = SystemParameters.IsRemoteSession;

            bool newMode = isSoftwareRendering || isHighContrast || isRemoteSession;

            if (IsLowFxMode != newMode)
            {
                IsLowFxMode = newMode;
                UpdateApplicationResources();
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateApplicationResources()
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            if (IsLowFxMode)
            {
                // ECO MODE: Titan Solid
                app.Resources["Brush.Surface.Glass"] = app.Resources["Brush.Surface.Glass.Eco"];
                app.Resources["Effect.Shadow.Dialog"] = null;
                app.Resources["Effect.Blur.Background"] = null;
            }
            else
            {
                // STANDARD MODE: Titan Glass
                app.Resources["Brush.Surface.Glass"] = app.Resources["Brush.Surface.Glass.Standard"];
                // Note: Shadows/Blur are traditionally reapplied via specialized effect resources if they exist
            }
        }
    }
}
