using System.Reflection;
using FluentAssertions;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Xunit;

namespace Management.Tests.Unit
{
    public class CoreTests
    {
        [Fact]
        public void AllModelsInDomainNamespace_ShouldImplementITenantEntity()
        {
            // Arrange
            var domainAssembly = typeof(Entity).Assembly;
            var modelNamespace = "Management.Domain.Models";

            // Act
            var modelTypes = domainAssembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && t.Namespace.StartsWith(modelNamespace))
                .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true))
                .ToList();

            // Assert
            foreach (var type in modelTypes)
            {
                type.Should().Implement<ITenantEntity>(
                    $"Type {type.Name} must implement ITenantEntity to ensure multi-tenancy integrity.");
            }
        }
    }
}
