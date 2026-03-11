using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record FailedPaymentDto(
        Guid Id,
        string MemberName,
        decimal Amount,
        string Reason, // e.g. "Insufficient Funds"

        // FIX: Renamed 'Date' to 'AttemptDate' to match the Service code
        DateTime AttemptDate
    );
}
