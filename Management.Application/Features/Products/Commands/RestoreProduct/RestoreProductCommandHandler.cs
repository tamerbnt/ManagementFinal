using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Products.Commands.RestoreProduct
{
    public class RestoreProductCommandHandler : IRequestHandler<RestoreProductCommand, Result>
    {
        private readonly IProductRepository _productRepository;

        public RestoreProductCommandHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<Result> Handle(RestoreProductCommand request, CancellationToken cancellationToken)
        {
            await _productRepository.RestoreAsync(request.Id, request.FacilityId);
            return Result.Success();
        }
    }
}
