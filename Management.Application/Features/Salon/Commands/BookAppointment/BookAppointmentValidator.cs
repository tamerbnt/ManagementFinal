using FluentValidation;

namespace Management.Application.Features.Salon.Commands.BookAppointment
{
    public class BookAppointmentValidator : AbstractValidator<BookAppointmentCommand>
    {
        public BookAppointmentValidator()
        {
            RuleFor(x => x.ClientId).NotEmpty();
            RuleFor(x => x.StaffId).NotEmpty();
            RuleFor(x => x.StartTime).NotEmpty();
            RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("End time must be after start time.");
            
            // Requirement: verify that an appointment duration cannot be zero.
            RuleFor(x => x).Must(x => (x.EndTime - x.StartTime).TotalMinutes > 0)
                .WithMessage("Appointment duration cannot be zero.");
        }
    }
}
