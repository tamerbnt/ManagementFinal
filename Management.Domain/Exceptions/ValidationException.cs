using System;
using System.Collections.Generic;

namespace Management.Domain.Exceptions
{
    public class ValidationException : DomainException
    {
        public IReadOnlyDictionary<string, string[]> Errors { get; }

        public ValidationException(IReadOnlyDictionary<string, string[]> errors)
            : base("One or more validation failures have occurred.")
        {
            Errors = errors;
        }
    }
}