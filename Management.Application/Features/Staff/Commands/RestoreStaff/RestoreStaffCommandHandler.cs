using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Staff.Commands.RestoreStaff
{
    public class RestoreStaffCommandHandler : IRequestHandler<RestoreStaffCommand, Result>
    {
        private readonly IStaffRepository _staffRepository;

        public RestoreStaffCommandHandler(IStaffRepository staffRepository)
        {
            _staffRepository = staffRepository;
        }

        public async Task<Result> Handle(RestoreStaffCommand request, CancellationToken cancellationToken)
        {
            await _staffRepository.RestoreAsync(request.StaffId);
            return Result.Success();
        }
    }
}
