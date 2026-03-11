using System;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Models;
using Management.Domain.Enums;
using Management.Infrastructure.Integrations.Supabase;
using Management.Presentation.Services.Infrastructure;
using Management.Presentation.Services.State;
using Management.Application.Services;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Management.Application.DTOs;

namespace Management.Presentation.Services.Repositories
{
    public class AuthResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class LoginSuccessMessage { }

    [Table("profiles")]
    public class ProfileModel : BaseModel
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("role")]
        public int RoleValue { get; set; }
    }

    public interface IAuthRepository
    {
        Task<AuthResult> LoginAsync(string email, string password);
        Task LogoutAsync();
        Task<bool> RestoreSessionAsync();
    }

    public class AuthRepository : IAuthRepository
    {
        private readonly ISupabaseProvider _supabase;
        private readonly ISecureStorageService _secureStorage;
        private readonly SessionManager _sessionManager;

        public AuthRepository(
            ISupabaseProvider supabase, 
            ISecureStorageService secureStorage,
            SessionManager sessionManager)
        {
            _supabase = supabase;
            _secureStorage = secureStorage;
            _sessionManager = sessionManager;
        }

        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            try
            {
                var session = await _supabase.Client.Auth.SignIn(email, password);
                if (session?.User != null)
                {
                    await _secureStorage.SetAsync("access_token", session.AccessToken);
                    await _secureStorage.SetAsync("refresh_token", session.RefreshToken);
                    
                    // Guid Parsing Requirement
                    Guid userId = Guid.Parse(session.User.Id);
                    
                    var response = await _supabase.Client.From<ProfileModel>()
                        .Where(x => x.Id == userId)
                        .Get();

                    var profile = response.Models.FirstOrDefault();
                    if (profile != null)
                    {
                        var staffRole = (StaffRole)profile.RoleValue;
                        
                        // Map to DTO for Session (Presentation Layer)
                        var staffDto = new StaffDto
                        {
                            Id = profile.Id,
                            FullName = profile.FullName,
                            Email = profile.Email,
                            Role = staffRole,
                            Status = "Active"
                        };

                        _sessionManager.SetUser(staffDto);
                        
                        // Logic: Assign facility based on Role (Mocked mapping)
                        _sessionManager.CurrentFacility = FacilityType.Gym;
                    }
                    
                    return new AuthResult { IsSuccess = true };
                }
                return new AuthResult { IsSuccess = false, ErrorMessage = "Failed to retrieve user session." };
            }
            catch (GotrueException ex)
            {
                return new AuthResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                return new AuthResult { IsSuccess = false, ErrorMessage = "Unexpected error: " + ex.Message };
            }
        }

        public async Task LogoutAsync()
        {
            try { await _supabase.Client.Auth.SignOut(); } catch { }
            await _secureStorage.ClearAsync();
            _sessionManager.Clear();
        }

        public async Task<bool> RestoreSessionAsync()
        {
            string? accessToken = await _secureStorage.GetAsync("access_token");
            string? refreshToken = await _secureStorage.GetAsync("refresh_token");
            
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken)) return false;

            try
            {
                var session = await _supabase.Client.Auth.SetSession(accessToken, refreshToken);
                if (session?.User != null)
                {
                    Guid userId = Guid.Parse(session.User.Id);
                    var response = await _supabase.Client.From<ProfileModel>()
                        .Where(x => x.Id == userId)
                        .Get();

                    var profile = response.Models.FirstOrDefault();
                    if (profile != null)
                    {
                        var staffRole = (StaffRole)profile.RoleValue;
                        
                        var staffDto = new StaffDto
                        {
                            Id = profile.Id,
                            FullName = profile.FullName,
                            Email = profile.Email,
                            Role = staffRole,
                            Status = "Active"
                        };

                        _sessionManager.SetUser(staffDto);
                        
                        // Sync facility state
                        _sessionManager.CurrentFacility = FacilityType.Gym;
                    }

                    return true;
                }
                return false;
            }
            catch
            {
                await _secureStorage.ClearAsync();
                return false;
            }
        }
    }
}
