using System;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data;
using Management.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Repositories
{
    public class FacilityZoneRepository : Repository<FacilityZone>, IFacilityZoneRepository
    {
        public FacilityZoneRepository(
            AppDbContext context) 
            : base(context)
        {
        }
    }
}
