using FluentValidation;
using System.Linq;

namespace Management.Application.Features.Sales.Commands.ProcessCheckout
{
    public class ProcessCheckoutCommandValidator : AbstractValidator<ProcessCheckoutCommand>
    {
        public ProcessCheckoutCommandValidator()
        {
            RuleFor(x => x.Request).NotNull();
            RuleFor(x => x.Request.Items)
                .NotEmpty().WithMessage("Cart items are required.")
                .Must(items => items.All(i => i.Value > 0)).WithMessage("Item quantities must be greater than zero.");
        }
    }
}
