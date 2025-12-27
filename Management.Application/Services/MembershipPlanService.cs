using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Application.Services
{
    public class MembershipPlanService : IMembershipPlanService
    {
        private readonly IMembershipPlanRepository _repository;

        public MembershipPlanService(IMembershipPlanRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<MembershipPlanDto>> GetAllPlansAsync()
        {
            // Return all, including archived (UI can filter if needed, or repo does)
            // Ideally we want all so Settings can see archived ones.
            var entities = await _repository.GetAllAsync();
            return entities.Select(e => new MembershipPlanDto
            {
                Id = e.Id,
                Name = e.Name,
                Price = e.Price,
                DurationMonths = e.DurationMonths,
                IsActive = e.IsActive
            }).ToList();
        }

        public async Task CreatePlanAsync(MembershipPlanDto dto)
        {
            var entity = new MembershipPlan
            {
                Name = dto.Name,
                Price = dto.Price,
                DurationMonths = dto.DurationMonths,
                IsActive = true
            };
            await _repository.AddAsync(entity);
        }

        public async Task UpdatePlanAsync(MembershipPlanDto dto)
        {
            var entity = await _repository.GetByIdAsync(dto.Id);
            entity.Name = dto.Name;
            entity.Price = dto.Price;
            entity.DurationMonths = dto.DurationMonths;
            entity.IsActive = dto.IsActive;

            await _repository.UpdateAsync(entity);
        }

        public async Task DeletePlanAsync(Guid id)
        {
            // Soft Delete / Archive
            var entity = await _repository.GetByIdAsync(id);
            entity.IsActive = false; // Archive
            await _repository.UpdateAsync(entity);
        }
    }
}