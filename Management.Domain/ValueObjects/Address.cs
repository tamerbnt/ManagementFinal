using Management.Domain.Primitives;

namespace Management.Domain.ValueObjects
{
    public record Address(string Street, string City, string State, string ZipCode, string Country);
}
