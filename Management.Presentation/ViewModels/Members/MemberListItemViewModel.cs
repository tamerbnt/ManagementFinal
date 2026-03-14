using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Domain.Models;
using Management.Domain.Enums;

namespace Management.Presentation.ViewModels.Members
{
    public partial class MemberListItemViewModel : ObservableObject
    {
        private readonly Member _member;

        [ObservableProperty]
        private ImageSource? _avatarImage;

        [ObservableProperty]
        private bool _isImageLoading;

        public MemberListItemViewModel(Member member)
        {
            _member = member;
            _ = LoadAvatarAsync();
        }

        public Member Member => _member;
        
        public Guid Id => _member.Id;
        public string Name => _member.FullName;
        public string Email => _member.Email.Value;
        public string Phone => _member.PhoneNumber?.Value ?? "No Phone";
        public MemberStatus Status => _member.Status;
        public DateTime JoinDate => _member.CreatedAt;

        public string DisplayInitials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name)) return "?";
                var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
                return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
            }
        }

        private async Task LoadAvatarAsync()
        {
            // Note: ProfileImageUrl is currently not in the SQL schema provided.
            // We use the property from the domain model if it exists, otherwise skip.
            // For now, mapping to ProfileImageUrl if present in the model.
            // (I added it to the AggregateRoot Member, let's see if Entities has it)
            return; 
        }
    }
}
