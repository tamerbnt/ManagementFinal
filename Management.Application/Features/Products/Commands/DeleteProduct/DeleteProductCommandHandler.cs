using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Products.Commands.DeleteProduct
{
    public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
    {
        private readonly IProductRepository _productRepository;

        public DeleteProductCommandHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdAsync(request.Id, request.FacilityId);
            if (product == null)
            {
                return Result.Failure(new Error("Product.NotFound", "Product not found"));
            }

            await _productRepository.DeleteAsync(product.Id);
            return Result.Success();
        }
    }
}
