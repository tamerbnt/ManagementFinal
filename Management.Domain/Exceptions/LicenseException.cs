using System;

namespace Management.Domain.Exceptions
{
    public class LicenseException : Exception
    {
        public LicenseException(string message) : base(message)
        {
        }

        public LicenseException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
