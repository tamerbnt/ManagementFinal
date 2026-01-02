using Management.Application.Abstractions.Messaging;
using Management.Domain.Services;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Behaviors
{
    /// <summary>
    /// MediatR pipeline behavior that enforces RBAC authorization rules.
    /// </summary>
    public class AuthorizationBehavior<TRequest, TResponse> 
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IAuthenticationService _authenticationService;

        public AuthorizationBehavior(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        public async Task<TResponse> Handle(
            TRequest request, 
            RequestHandlerDelegate<TResponse> next, 
            CancellationToken cancellationToken)
        {
            if (request is not IAuthorizeableRequest authorizeableRequest)
            {
                return await next();
            }

            var userResult = await _authenticationService.GetCurrentUserAsync();

            if (userResult.IsFailure)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var user = userResult.Value;
            var requiredPermissions = authorizeableRequest.RequiredPermissions;

            if (requiredPermissions != null && requiredPermissions.Any())
            {
                foreach (var permission in requiredPermissions)
                {
                    if (!user.Permissions.Any(p => p.Name == permission && p.IsGranted))
                    {
                        throw new UnauthorizedAccessException($"User does not have the required permission: {permission}");
                    }
                }
            }

            return await next();
        }
    }
}
