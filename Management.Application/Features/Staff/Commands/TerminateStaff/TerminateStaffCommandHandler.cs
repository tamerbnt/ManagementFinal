using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Staff.Commands.TerminateStaff
{
    public class TerminateStaffCommandHandler : IRequestHandler<TerminateStaffCommand, Result>
    {
        private readonly IStaffRepository _staffRepository;

        public TerminateStaffCommandHandler(IStaffRepository staffRepository)
        {
            _staffRepository = staffRepository;
        }

        public async Task<Result> Handle(TerminateStaffCommand request, CancellationToken cancellationToken)
        {
            var staff = await _staffRepository.GetByIdAsync(request.StaffId);
            if (staff == null)
            {
                return Result.Failure(new Error("Staff.NotFound", "Staff member not found."));
            }

            staff.Delete();
            await _staffRepository.UpdateAsync(staff);

            return Result.Success();
        }
    }
}
