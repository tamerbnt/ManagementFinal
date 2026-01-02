using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Restaurant.Commands.ProcessOrder
{
    public class ProcessOrderCommandHandler : IRequestHandler<ProcessOrderCommand, Result<Guid>>
    {
        private readonly ISaleRepository _saleRepository; // Sharing sale infra
        private readonly ITenantService _tenantService;

        public ProcessOrderCommandHandler(ISaleRepository saleRepository, ITenantService tenantService)
        {
            _saleRepository = saleRepository;
            _tenantService = tenantService;
        }

        public async Task<Result<Guid>> Handle(ProcessOrderCommand request, CancellationToken cancellationToken)
        {
            var order = new RestaurantOrder
            {
                TableNumber = request.TableNumber,
                TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                Items = request.Items.Select(i => new OrderItem
                {
                    Name = i.Name,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    TenantId = _tenantService.GetTenantId() ?? Guid.Empty
                }).ToList()
            };

            order.CalculateTotal();

            // Simulation of saving
            // await _saleRepository.AddOrderAsync(order);

            return Result.Success(order.Id);
        }
    }
}
