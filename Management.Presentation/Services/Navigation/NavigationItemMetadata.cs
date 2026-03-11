using System;

namespace Management.Presentation.Services.Navigation
{
    /// <summary>
    /// Metadata for a single sidebar navigation item.
    /// Used by providers to define items.
    /// </summary>
    public record NavigationItemMetadata(
        string DisplayName,
        string ResourceKey,
        string IconKey,
        Type TargetViewModelType,
        int Order = 0
    );
}
