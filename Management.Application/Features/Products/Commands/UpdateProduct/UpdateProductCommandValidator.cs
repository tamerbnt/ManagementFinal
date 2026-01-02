using FluentValidation;

namespace Management.Application.Features.Products.Commands.UpdateProduct
{
    public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
    {
        public UpdateProductCommandValidator()
        {
            RuleFor(x => x.Product).NotNull();
            RuleFor(x => x.Product.Id).NotEmpty();
            RuleFor(x => x.Product.Name).NotEmpty().WithMessage("Product Name is required.");
            RuleFor(x => x.Product.SKU).NotEmpty().WithMessage("SKU is required.");
            RuleFor(x => x.Product.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
        }
    }
}
