using Management.Application.Stores;
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

namespace Management.Application.Features.Products.Commands.CreateProduct
{
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
    {
        private readonly IProductRepository _productRepository;

        public CreateProductCommandHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Product;

            if (!Enum.TryParse<ProductCategory>(dto.Category, out var category))
            {
                category = ProductCategory.Other; 
            }

            var price = new Money(dto.Price, dto.Currency ?? "USD");
            var cost = new Money(dto.Cost, dto.Currency ?? "USD"); 
            
            var result = Product.Create(
                dto.Name,
                dto.Description,
                price,
                cost,
                dto.StockQuantity,
                dto.SKU,
                category,
                dto.ImageUrl,
                dto.ReorderLevel);

            if (result.IsFailure)
            {
                return Result.Failure<Guid>(result.Error);
            }

            var product = result.Value;
            
            await _productRepository.AddAsync(product);

            return Result.Success(product.Id);
        }
    }
}
