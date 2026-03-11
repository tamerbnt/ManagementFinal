using System.Threading;
using System.Threading.Tasks;
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

        public UpdateProductStockCommandHandler(
            IProductRepository productRepository, 
            Stores.ProductStore productStore,
            IMediator mediator)
        {
            _productRepository = productRepository;
            _productStore = productStore;
            _mediator = mediator;
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
            await _mediator.Publish(new Notifications.FacilityActionCompletedNotification(
                product.FacilityId,
                "Inventory",
                product.Name,
                $"Stock adjusted: {request.QuantityChange} ({request.Reason})"), cancellationToken);
            
            return Result.Success();
        }
    }
}
