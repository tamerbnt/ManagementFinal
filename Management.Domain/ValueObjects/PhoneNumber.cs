using System.Text.RegularExpressions;
using Management.Domain.Primitives;

namespace Management.Domain.ValueObjects
{
    public record PhoneNumber : ValueObject
    {
        public string Value { get; }
        public static PhoneNumber None => new PhoneNumber("+10000000000");

        private PhoneNumber(string value)
        {
            Value = value;
        }

        public static Result<PhoneNumber> Create(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return Result.Failure<PhoneNumber>(new Error("PhoneNumber.Empty", "Phone number is empty"));
            }

            if (!Regex.IsMatch(phoneNumber, @".+")) // Relaxed: Just check for at least one character
            {
                return Result.Failure<PhoneNumber>(new Error("PhoneNumber.Invalid", "Phone format is invalid"));
            }

            return Result.Success(new PhoneNumber(phoneNumber));
        }

        public override string ToString() => Value;
    }
}
