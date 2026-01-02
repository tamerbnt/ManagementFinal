using System.Collections.Generic;
using System.Text.RegularExpressions;
using Management.Domain.Primitives;

namespace Management.Domain.ValueObjects
{
    public record Email : ValueObject
    {
        public string Value { get; }

        private Email(string value)
        {
            Value = value;
        }

        public static Result<Email> Create(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Result.Failure<Email>(new Error("Email.Empty", "Email is empty"));
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return Result.Failure<Email>(new Error("Email.Invalid", "Email format is invalid"));
            }

            return Result.Success(new Email(email));
        }

        public override string ToString() => Value;
    }
}
