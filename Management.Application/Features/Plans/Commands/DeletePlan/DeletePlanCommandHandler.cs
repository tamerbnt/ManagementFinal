using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Plans.Commands.DeletePlan
{
    public class DeletePlanCommandHandler : IRequestHandler<DeletePlanCommand, Result>
    {
        private readonly IMembershipPlanRepository _planRepository;

        public DeletePlanCommandHandler(IMembershipPlanRepository planRepository)
        {
            _planRepository = planRepository;
        }

        public async Task<Result> Handle(DeletePlanCommand request, CancellationToken cancellationToken)
        {
            var plan = await _planRepository.GetByIdAsync(request.PlanId);
            if (plan == null) return Result.Failure(new Error("Plan.NotFound", "Plan not found"));

            // Soft delete preference? Or Hard delete if not used?
            // Assuming Hard Delete for now as per generic Repo, or Deactivate.
            // If repository DeleteAsync exists:
            // await _planRepository.DeleteAsync(request.PlanId); 
            // Or just deactivate?
            // Let's assum Hard Delete via Repo.
            // Wait, standard IRepository has DeleteAsync(Guid id) from Step ~300?
            // "IRepository<T>" usually has DeleteAsync.
            // If IRepository only has void return, I need to check.
            // Step 655 build error showed `DeleteAsync` return void.
            // Step 681 fixed DeleteMemberCommandHandler: `await _memberRepo.DeleteAsync`.
            
            await _planRepository.DeleteAsync(request.PlanId);
            return Result.Success();
        }
    }
}
