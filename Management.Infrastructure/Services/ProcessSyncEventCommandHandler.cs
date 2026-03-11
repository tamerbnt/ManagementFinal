using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Primitives;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System;
using Management.Domain.Models;
using Management.Application.Stores;
using Management.Application.Features.Sync.Commands.ProcessSyncEvent;

namespace Management.Infrastructure.Services
{
    public class ProcessSyncEventCommandHandler : IRequestHandler<ProcessSyncEventCommand, Result>
    {
        private readonly AppDbContext _context;
        private readonly MemberStore _memberStore;
        private readonly RegistrationStore _registrationStore;

        public ProcessSyncEventCommandHandler(
            AppDbContext context, 
            MemberStore memberStore, 
            RegistrationStore registrationStore)
        {
            _context = context;
            _memberStore = memberStore;
            _registrationStore = registrationStore;
        }

        public async Task<Result> Handle(ProcessSyncEventCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.EntityType.EndsWith("members", StringComparison.OrdinalIgnoreCase))
                {
                    return await ProcessMemberAsync(request.Action, request.ContentJson);
                }
                else if (request.EntityType.EndsWith("registrations", StringComparison.OrdinalIgnoreCase))
                {
                    return await ProcessRegistrationAsync(request.Action, request.ContentJson);
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("Sync.ProcessingError", ex.Message));
            }
        }

        private async Task<Result> ProcessMemberAsync(string action, string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var remoteMember = JsonSerializer.Deserialize<Member>(json, options);
            if (remoteMember == null) return Result.Failure(new Error("Sync.InvalidPayload", "Could not deserialize Member"));

            var localMember = await _context.Members.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == remoteMember.Id);

            if (action == "INSERT" || action == "UPDATE")
            {
                if (localMember == null)
                {
                    _context.Members.Add(remoteMember);
                }
                else
                {
                    _context.Entry(localMember).CurrentValues.SetValues(remoteMember);
                }
                await _context.SaveChangesAsync();
            }
            
            return Result.Success();
        }

        private async Task<Result> ProcessRegistrationAsync(string action, string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var remoteReg = JsonSerializer.Deserialize<Registration>(json, options);
            if (remoteReg == null) return Result.Failure(new Error("Sync.InvalidPayload", "Could not deserialize Registration"));

            var localReg = await _context.Registrations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == remoteReg.Id);

            if (action == "INSERT" || action == "UPDATE")
            {
                if (localReg == null)
                {
                    _context.Registrations.Add(remoteReg);
                }
                else
                {
                    _context.Entry(localReg).CurrentValues.SetValues(remoteReg);
                }
                await _context.SaveChangesAsync();
            }

            return Result.Success();
        }
    }
}
