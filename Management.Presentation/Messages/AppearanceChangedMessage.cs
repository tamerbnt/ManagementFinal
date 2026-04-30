using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Management.Presentation.Messages
{
    /// <summary>
    /// Broadcasts that the appearance (theme or palette) has changed.
    /// This allows different ViewModels (TopBar, Settings) to stay in sync
    /// without redundant database reloads.
    /// </summary>
    public class AppearanceChangedMessage : ValueChangedMessage<AppearanceChangeInfo>
    {
        public AppearanceChangedMessage(AppearanceChangeInfo value) : base(value)
        {
        }
    }

    public record AppearanceChangeInfo(
        bool IsLightMode,
        string LightPalette,
        bool IsThemeChange = false,
        bool IsPaletteChange = false
    );
}
