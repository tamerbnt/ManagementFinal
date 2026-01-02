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

        public UpdateProductStockCommandHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<Result> Handle(UpdateProductStockCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdAsync(request.ProductId);
            
            if (product == null)
            {
                return Result.Failure(new Error("Product.NotFound", $"Product with ID {request.ProductId} was not found."));
            }

            // Update Stock
            product.UpdateStock(request.QuantityChange, request.Reason);

            await _productRepository.UpdateAsync(product);
            
            return Result.Success();
        }
    }
}
