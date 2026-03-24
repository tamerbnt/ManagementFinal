using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Sales.Commands.CancelSalesByMember
{
    public class CancelSalesByMemberCommandHandler : IRequestHandler<CancelSalesByMemberCommand, Result>
    {
        private readonly ISaleRepository _saleRepository;

        public CancelSalesByMemberCommandHandler(ISaleRepository saleRepository)
        {
            _saleRepository = saleRepository;
        }

        public async Task<Result> Handle(CancelSalesByMemberCommand request, CancellationToken cancellationToken)
        {
            var sales = await _saleRepository.GetSalesByMemberAsync(request.MemberId, request.FacilityId);
            
            foreach (var sale in sales)
            {
                await _saleRepository.DeleteAsync(sale.Id);
            }

            return Result.Success();
        }
    }
}
