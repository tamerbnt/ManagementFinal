using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Products.Commands.UpdateProduct
{
    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
    {
        private readonly IProductRepository _productRepository;

        public UpdateProductCommandHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Product;
            var product = await _productRepository.GetByIdAsync(dto.Id);

            if (product == null)
            {
                return Result.Failure(new Error("Product.NotFound", $"Product {dto.Id} not found."));
            }

            if (!Enum.TryParse<ProductCategory>(dto.Category, out var category))
            {
                category = ProductCategory.Other;
            }

            var price = new Money(dto.Price, dto.Currency ?? "USD");
            var cost = new Money(dto.Cost, dto.Currency ?? "USD"); 

            product.UpdateDetails(
                dto.Name,
                dto.Description,
                dto.SKU,
                price,
                cost,
                dto.ReorderLevel,
                category,
                dto.ImageUrl);
            
            int diff = dto.StockQuantity - product.StockQuantity;
            if (diff != 0)
            {
                product.UpdateStock(diff, "Manual Adjustment via Edit");
            }

            await _productRepository.UpdateAsync(product);

            return Result.Success();
        }
    }
}
