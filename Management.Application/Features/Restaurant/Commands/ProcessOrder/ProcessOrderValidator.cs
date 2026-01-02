using FluentValidation;

namespace Management.Application.Features.Restaurant.Commands.ProcessOrder
{
    public class ProcessOrderValidator : AbstractValidator<ProcessOrderCommand>
    {
        public ProcessOrderValidator()
        {
            RuleFor(x => x.TableNumber).NotEmpty();
            
            // Requirement: verify that an order is invalid if the item list is empty.
            RuleFor(x => x.Items).NotEmpty().WithMessage("Order must contain at least one item.");
            
            RuleForEach(x => x.Items).ChildRules(item => {
                item.RuleFor(i => i.Name).NotEmpty();
                item.RuleFor(i => i.Price).GreaterThan(0);
                item.RuleFor(i => i.Quantity).GreaterThan(0);
            });
        }
    }
}
