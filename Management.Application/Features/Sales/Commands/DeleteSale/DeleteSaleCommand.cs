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
        private readonly IAccessEventRepository _accessRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteSaleCommandHandler(
            ISaleRepository saleRepository, 
            IAccessEventRepository accessRepository,
            IAppointmentRepository appointmentRepository,
            IUnitOfWork unitOfWork)
        {
            _saleRepository = saleRepository;
            _accessRepository = accessRepository;
            _appointmentRepository = appointmentRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeleteSaleCommand request, CancellationToken cancellationToken)
        {
            var sale = await _saleRepository.GetByIdAsync(request.SaleId);
            if (sale == null)
            {
                return Result.Failure(new Error("Sale.NotFound", "The sale was not found."));
            }

            // Clean up associated Walk-In Access Event if it exists
            // Format is WI-{SaleId} as defined in GymOperationService
            var walkInTransactionId = $"WI-{request.SaleId}";
            var accessEvent = await _accessRepository.GetByTransactionIdAsync(walkInTransactionId);
            if (accessEvent != null)
            {
                await _accessRepository.DeleteAsync(accessEvent.Id);
            }
            
            // Revert Appointment status if sale is linked to a Salon appointment
            if (sale.TransactionType.StartsWith("SalonAppt-") && Guid.TryParse(sale.TransactionType.Replace("SalonAppt-", ""), out var apptId))
            {
                var appt = await _appointmentRepository.GetByIdAsync(apptId);
                if (appt != null)
                {
                    appt.Status = Management.Domain.Models.Salon.AppointmentStatus.InProgress;
                    await _appointmentRepository.UpdateAsync(appt);
                }
            }

            // Perform Soft Delete
            await _saleRepository.DeleteAsync(sale.Id);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
