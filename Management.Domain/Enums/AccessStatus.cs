using System;
using System.Collections.Generic;
using System.Text;

namespace Management.Domain.Enums
{
    /// <summary>
    /// Represents the outcome of an attempted entry at a turnstile or door.
    /// </summary>
    public enum AccessStatus
    {
        Unknown = 0,
        Granted = 1,
        Denied = 2,
        Locked = 3,
        Warning = 4
    }
}
