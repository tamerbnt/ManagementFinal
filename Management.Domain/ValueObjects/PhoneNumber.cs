using System.Text.RegularExpressions;
using Management.Domain.Primitives;

namespace Management.Domain.ValueObjects
{
    public record PhoneNumber : ValueObject
    {
        public string Value { get; }

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

            if (!Regex.IsMatch(phoneNumber, @"^\+?[1-9]\d{1,14}$"))
            {
                return Result.Failure<PhoneNumber>(new Error("PhoneNumber.Invalid", "Phone format is invalid"));
            }

            return Result.Success(new PhoneNumber(phoneNumber));
        }

        public override string ToString() => Value;
    }
}
