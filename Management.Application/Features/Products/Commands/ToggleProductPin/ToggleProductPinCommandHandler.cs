using MediatR;
using Management.Domain.Interfaces;
using Management.Domain.Common;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.Stores;
using Management.Application.DTOs;
using Management.Domain.Models;

namespace Management.Application.Features.Products.Commands.ToggleProductPin
{
    public class ToggleProductPinCommandHandler : IRequestHandler<ToggleProductPinCommand, Result>
    {
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ProductStore _productStore;

        public ToggleProductPinCommandHandler(
            IProductRepository productRepository,
            IUnitOfWork unitOfWork,
            ProductStore productStore)
        {
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _productStore = productStore;
        }

        public async Task<Result> Handle(ToggleProductPinCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdAsync(request.Id);
            if (product == null)
            {
                return Result.Failure(new Error("Product.NotFound", "Product not found."));
            }

            product.TogglePin();

            await _productRepository.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Notify UI
            _productStore.TriggerProductUpdated(MapToDto(product));

            return Result.Success();
        }

        private ProductDto MapToDto(Product entity)
        {
            return new ProductDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Price = entity.Price.Amount,
                Cost = entity.Cost.Amount,
                Currency = entity.Price.Currency,
                StockQuantity = entity.StockQuantity,
                SKU = entity.SKU,
                Category = entity.Category.ToString(),
                ImageUrl = entity.ImageUrl,
                ReorderLevel = entity.ReorderLevel,
                IsPinned = entity.IsPinned
            };
        }
    }
}
