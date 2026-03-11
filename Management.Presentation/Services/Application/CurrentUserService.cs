using System;
using Management.Application.Interfaces;
using Management.Presentation.Services.State;

namespace Management.Presentation.Services.Application
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly SessionManager _sessionManager;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        public CurrentUserService(SessionManager sessionManager, Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _sessionManager = sessionManager;
            _facilityContext = facilityContext;
        }

        public Guid? CurrentFacilityId => _facilityContext.CurrentFacilityId;
        public Guid? UserId => _sessionManager.CurrentUser?.Id;
    }
}
