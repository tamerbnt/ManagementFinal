using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Plans.Commands.RestorePlan
{
    public class RestorePlanCommandHandler : IRequestHandler<RestorePlanCommand, Result>
    {
        private readonly IMembershipPlanRepository _planRepository;

        public RestorePlanCommandHandler(IMembershipPlanRepository planRepository)
        {
            _planRepository = planRepository;
        }

        public async Task<Result> Handle(RestorePlanCommand request, CancellationToken cancellationToken)
        {
            // RestoreAsync is now implemented in the repository using ExecuteUpdateAsync
            await _planRepository.RestoreAsync(request.PlanId);
            return Result.Success();
        }
    }
}
