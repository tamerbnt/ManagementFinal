using FluentValidation;

namespace Management.Application.Features.Products.Commands.CreateProduct
{
    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Product).NotNull();
            RuleFor(x => x.Product.Name).NotEmpty().WithMessage("Product Name is required.");
            RuleFor(x => x.Product.SKU).NotEmpty().WithMessage("SKU is required.");
            RuleFor(x => x.Product.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
            RuleFor(x => x.Product.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");
        }
    }
}
