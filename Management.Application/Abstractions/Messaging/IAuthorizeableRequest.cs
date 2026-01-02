using System.Collections.Generic;

namespace Management.Application.Abstractions.Messaging
{
    /// <summary>
    /// Interface for MediatR requests that require authorization.
    /// </summary>
    public interface IAuthorizeableRequest
    {
        /// <summary>
        /// List of permission strings required to execute this request.
        /// Empty means no specific permission, but authentication is required.
        /// </summary>
        IEnumerable<string> RequiredPermissions { get; }
    }
}
