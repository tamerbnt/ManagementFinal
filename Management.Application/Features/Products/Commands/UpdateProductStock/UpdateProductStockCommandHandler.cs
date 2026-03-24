using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Products.Commands.UpdateProductStock
{
    public class UpdateProductStockCommandHandler : IRequestHandler<UpdateProductStockCommand, Result>
    {
        private readonly IProductRepository _productRepository;
        private readonly Stores.ProductStore _productStore;
        private readonly IMediator _mediator;
        private readonly Microsoft.Extensions.Logging.ILogger<UpdateProductStockCommandHandler> _logger;

        public UpdateProductStockCommandHandler(
            IProductRepository productRepository, 
            Stores.ProductStore productStore,
            IMediator mediator,
            Microsoft.Extensions.Logging.ILogger<UpdateProductStockCommandHandler> logger)
        {
            _productRepository = productRepository;
            _productStore = productStore;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(UpdateProductStockCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdAsync(request.ProductId, request.FacilityId);
            
            if (product == null)
            {
                return Result.Failure(new Error("Product.NotFound", $"Product with ID {request.ProductId} was not found."));
            }

            // Update Stock
            product.UpdateStock(request.QuantityChange, request.Reason);

            await _productRepository.UpdateAsync(product);

            // Notify UI
            _productStore.TriggerStockUpdated(new DTOs.ProductDto 
            { 
                Id = product.Id, 
                StockQuantity = product.StockQuantity,
                Name = product.Name,
                Price = product.Price.Amount,
                SKU = product.SKU
            });

            // Notify activity stream
            // Notify activity stream: Decoupled to prevent UI failure from stalling adjustment.
            _ = Task.Run(async () => 
            {
                try 
                {
                    await _mediator.Publish(new Notifications.FacilityActionCompletedNotification(
                        product.FacilityId,
                        "Inventory",
                        product.Name,
                        $"Stock adjusted: {request.QuantityChange} ({request.Reason})"));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to publish stock adjustment notification for product {ProductId}", product.Id);
                }
            });
            
            return Result.Success();
        }
    }
}
