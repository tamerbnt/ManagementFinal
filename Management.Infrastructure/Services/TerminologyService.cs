using Management.Domain.Services;
using Management.Domain.Enums;

namespace Management.Infrastructure.Services
{
    public class TerminologyService : ITerminologyService
    {
        private readonly IFacilityContextService _facilityContext;

        private readonly Dictionary<FacilityType, Dictionary<string, string>> _map = new()
        {
            {
                FacilityType.Gym, new Dictionary<string, string>
                {
                    { "Guest", "Member" },
                    { "Guests", "Members" },
                    { "Booking", "Check-In" },
                    { "Terminology.Dashboard.Title", "Gym Performance" },
                    { "Terminology.Sidebar.Dashboard", "Dashboard" },
                    { "Terminology.Sidebar.Members", "Members" },
                    { "Terminology.Sidebar.Shop", "Pro Shop" },
                    { "TerminologyAddMemberLabel", "Add Member" },
                    { "TerminologyAddNewLabel", "Add New" },
                    { "Terminology.Global.Unknown", "Unknown" }
                }
            },
            {
                FacilityType.Salon, new Dictionary<string, string>
                {
                    { "Guest", "Client" },
                    { "Guests", "Clients" },
                    { "Booking", "Appointment" },
                    { "Terminology.Dashboard.Title", "Salon Revenue" },
                    { "Terminology.Sidebar.Dashboard", "Overview" },
                    { "Terminology.Sidebar.Members", "Clients" },
                    { "Terminology.Sidebar.Shop", "Retail" },
                    { "TerminologyAddMemberLabel", "Add Client" },
                    { "TerminologyAddNewLabel", "Add New" },
                    { "Terminology.Global.Unknown", "Unknown" }
                }
            },
            {
                FacilityType.Restaurant, new Dictionary<string, string>
                {
                    { "Guest", "Guest" },
                    { "Guests", "Guests" },
                    { "Booking", "Reservation" },
                    { "Table", "Table" },
                    { "Terminology.Dashboard.Title", "Kitchen Status" },
                    { "Terminology.Sidebar.Dashboard", "Live Map" },
                    { "Terminology.Sidebar.Members", "Guests" },
                    { "Terminology.Sidebar.Shop", "Menu" },
                    { "TerminologyAddMemberLabel", "Add Guest" },
                    { "TerminologyAddNewLabel", "Add New" },
                    { "Terminology.Global.Unknown", "Unknown" }
                }
            }
        };

        public TerminologyService(IFacilityContextService facilityContext)
        {
            _facilityContext = facilityContext;
        }

        public string GetTerm(string key)
        {
            // 1. Try WPF resources first (dynamically loaded by FacilityContextService with localization support)
            try 
            {
                // Use reflection to avoid forcing Management.Infrastructure to be a WPF project
                var presentationFramework = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "PresentationFramework");

                if (presentationFramework != null)
                {
                    var appType = presentationFramework.GetType("System.Windows.Application");
                    var currentProp = appType?.GetProperty("Current");
                    var app = currentProp?.GetValue(null);

                    if (app != null)
                    {
                        var tryFindResourceMethod = app.GetType().GetMethod("TryFindResource", new[] { typeof(object) });
                        var resource = tryFindResourceMethod?.Invoke(app, new object[] { key });
                        if (resource is string localizedTerm) return localizedTerm;
                    }
                }
            }
            catch { /* Fallback if not in WPF context or reflection fails */ }

            // 2. Fallback to hardcoded map (Legacy/Non-UI support)
            var facility = _facilityContext.CurrentFacility;
            if (_map.TryGetValue(facility, out var terms) && terms.TryGetValue(key, out var term))
            {
                return term;
            }
            return key; // Final fallback to key
        }
    }
}
