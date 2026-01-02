using System;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Management.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Management.Tests.Integration
{
    public class FacilityIsolationTests : IDisposable
    {
        private readonly ManagementDbContext _context;
        private readonly MemberRepository _memberRepository;
        private Guid _gymFacilityId;
        private Guid _salonFacilityId;

        public FacilityIsolationTests()
        {
            // Setup InMemory database
            var options = new DbContextOptionsBuilder<ManagementDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ManagementDbContext(options);
            _memberRepository = new MemberRepository(_context);

            // Create test facilities
            _gymFacilityId = Guid.NewGuid();
            _salonFacilityId = Guid.NewGuid();
        }

        [Fact]
        public async Task GetAll_WithFacilityContext_ReturnsOnlyFacilityMembers()
        {
            // Arrange: Add member to Gym facility
            var gymMember = new Domain.Models.Member
            {
                Id = Guid.NewGuid(),
                FacilityId = _gymFacilityId,
                FullName = "John Gym Member",
                Email = "john@gym.com",
                Phone = "1234567890",
                Status = "Active",
                ExpirationDate = DateTime.Now.AddMonths(1),
                CreatedAt = DateTime.Now
            };

            await _context.Members.AddAsync(gymMember);
            await _context.SaveChangesAsync();

            // Act: Query with Salon facility context
            var salonMembers = await _memberRepository.GetAllAsync(_salonFacilityId);

            // Assert: Should return 0 members (facility isolation)
            Assert.Empty(salonMembers);
        }

        [Fact]
        public async Task GetAll_WithCorrectFacilityContext_ReturnsFacilityMembers()
        {
            // Arrange: Add members to both facilities
            var gymMember = new Domain.Models.Member
            {
                Id = Guid.NewGuid(),
                FacilityId = _gymFacilityId,
                FullName = "John Gym",
                Email = "john@gym.com",
                Phone = "1111111111",
                Status = "Active",
                ExpirationDate = DateTime.Now.AddMonths(1),
                CreatedAt = DateTime.Now
            };

            var salonMember = new Domain.Models.Member
            {
                Id = Guid.NewGuid(),
                FacilityId = _salonFacilityId,
                FullName = "Jane Salon",
                Email = "jane@salon.com",
                Phone = "2222222222",
                Status = "Active",
                ExpirationDate = DateTime.Now.AddMonths(1),
                CreatedAt = DateTime.Now
            };

            await _context.Members.AddAsync(gymMember);
            await _context.Members.AddAsync(salonMember);
            await _context.SaveChangesAsync();

            // Act: Query each facility
            var gymMembers = await _memberRepository.GetAllAsync(_gymFacilityId);
            var salonMembers = await _memberRepository.GetAllAsync(_salonFacilityId);

            // Assert: Each facility sees only its own members
            Assert.Single(gymMembers);
            Assert.Single(salonMembers);
            Assert.Equal("John Gym", gymMembers[0].FullName);
            Assert.Equal("Jane Salon", salonMembers[0].FullName);
        }

        [Fact]
        public async Task CreateMember_WithFacilityId_IsolatesData()
        {
            // Arrange & Act: Create member in Gym
            var newMember = new Domain.Models.Member
            {
                Id = Guid.NewGuid(),
                FacilityId = _gymFacilityId,
                FullName = "New Gym Member",
                Email = "new@gym.com",
                Phone = "3333333333",
                Status = "Active",
                ExpirationDate = DateTime.Now.AddMonths(1),
                CreatedAt = DateTime.Now
            };

            await _memberRepository.CreateAsync(newMember);

            // Assert: Verify isolation
            var gymMembers = await _memberRepository.GetAllAsync(_gymFacilityId);
            var salonMembers = await _memberRepository.GetAllAsync(_salonFacilityId);

            Assert.Single(gymMembers);
            Assert.Empty(salonMembers);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
