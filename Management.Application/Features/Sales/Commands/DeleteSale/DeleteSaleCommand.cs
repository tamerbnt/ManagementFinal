using MediatR;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Sales.Commands.DeleteSale
{
    public record DeleteSaleCommand(Guid SaleId) : IRequest<Result>;

    public class DeleteSaleCommandHandler : IRequestHandler<DeleteSaleCommand, Result>
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteSaleCommandHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork)
        {
            _saleRepository = saleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeleteSaleCommand request, CancellationToken cancellationToken)
        {
            var sale = await _saleRepository.GetByIdAsync(request.SaleId);
            if (sale == null)
            {
                return Result.Failure(new Error("Sale.NotFound", "The sale was not found."));
            }

            // Perform Soft Delete (or Hard Delete depending on Domain logic)
            // For now, we follow the common pattern in this project
            await _saleRepository.DeleteAsync(sale.Id);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
