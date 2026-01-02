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
                    { "TerminologyAddMemberLabel", "Add Member" },
                    { "TerminologyAddPaymentLabel", "Add Payment" },
                    { "TerminologyAddProductLabel", "Add Product" },
                    { "TerminologyNewOrderLabel", "New Order" },
                    { "TerminologyAddNewLabel", "Add New" }
                }
            },
            {
                FacilityType.Salon, new Dictionary<string, string>
                {
                    { "Guest", "Client" },
                    { "Guests", "Clients" },
                    { "Booking", "Appointment" },
                    { "TerminologyAddMemberLabel", "Add Client" },
                    { "TerminologyAddPaymentLabel", "Add Payment" },
                    { "TerminologyAddProductLabel", "Add Retail Item" },
                    { "TerminologyNewOrderLabel", "New Booking" },
                    { "TerminologyAddNewLabel", "Add New" }
                }
            },
            {
                FacilityType.Restaurant, new Dictionary<string, string>
                {
                    { "Guest", "Guest" },
                    { "Guests", "Guests" },
                    { "Booking", "Reservation" },
                    { "Table", "Table" },
                    { "TerminologyAddMemberLabel", "Add Guest" },
                    { "TerminologyAddPaymentLabel", "Add Bill" },
                    { "TerminologyAddProductLabel", "Add Inventory" },
                    { "TerminologyNewOrderLabel", "New Table Order" },
                    { "TerminologyAddNewLabel", "Add New" }
                }
            }
        };

        public TerminologyService(IFacilityContextService facilityContext)
        {
            _facilityContext = facilityContext;
        }

        public string GetTerm(string key)
        {
            var facility = _facilityContext.CurrentFacility;
            if (_map.TryGetValue(facility, out var terms) && terms.TryGetValue(key, out var term))
            {
                return term;
            }
            return key; // Fallback to raw key
        }
    }
}
