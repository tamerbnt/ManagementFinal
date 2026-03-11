using System;

namespace Management.Application.DTOs
{
    public class SearchResultDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string IconGeometry { get; set; } = string.Empty;
        
        // This key allows the ViewModel to map the result to a concrete command/action
        public string ActionKey { get; set; } = string.Empty;
        public object? ActionParameter { get; set; }
    }
}
