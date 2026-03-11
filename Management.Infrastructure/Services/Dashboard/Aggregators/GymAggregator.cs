using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class GymAggregator : BaseAggregator
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IAccessEventRepository _accessEventRepository;
        private readonly IRegistrationRepository _registrationRepository;

        public GymAggregator(
            IMemberRepository memberRepository,
            IAccessEventRepository accessEventRepository,
            IRegistrationRepository registrationRepository)
        {
            _memberRepository = memberRepository;
            _accessEventRepository = accessEventRepository;
            _registrationRepository = registrationRepository;
        }

        public override int Priority => 20;

        public override bool CanHandle(DashboardContext context) => context.IsGym;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;


            dto.TotalMembers = await _memberRepository.GetTotalCountAsync(facilityId);
            dto.ActiveMembers = await _memberRepository.GetActiveCountAsync(facilityId);
            dto.PendingRegistrationsCount = await _registrationRepository.GetCountByStatusAsync(Management.Domain.Enums.RegistrationStatus.Pending, facilityId);
            dto.ExpiringSoonCount = await _memberRepository.GetExpiringCountAsync(context.LocalToday.AddDays(7), facilityId);
            dto.CheckInsToday = await _accessEventRepository.GetCurrentOccupancyCountAsync(facilityId);


            // Last hour trend
            var utcOneHourAgo = context.UtcNow.AddHours(-1);
            var utcTwoHoursAgo = context.UtcNow.AddHours(-2);
            var lastHourEvents = await _accessEventRepository.GetByDateRangeAsync(facilityId, utcTwoHoursAgo, utcOneHourAgo);

            dto.PeopleInsideLastHour = lastHourEvents.Count(e => e.IsAccessGranted);
        }
    }
}
