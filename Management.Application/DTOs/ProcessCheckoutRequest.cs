using System;
using System.Collections.Generic;

namespace Management.Application.DTOs
{
    public class ProcessCheckoutRequest
    {
        public Guid? MemberId { get; set; }
        public string Method { get; set; } = "Cash";
        public Dictionary<Guid, int> Items { get; set; } = new();
    }
}
