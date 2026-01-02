using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Salon.Commands.BookAppointment
{
    public class BookAppointmentCommand : IRequest<Result<Guid>>
    {
        public Guid ClientId { get; set; }
        public required string ClientName { get; set; }
        public Guid StaffId { get; set; }
        public Guid ServiceId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
