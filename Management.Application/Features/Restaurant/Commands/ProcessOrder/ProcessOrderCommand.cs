using Management.Domain.Primitives;
using Management.Domain.Models.Restaurant;
using MediatR;
using System.Collections.Generic;
using Management.Application.DTOs;

namespace Management.Application.Features.Restaurant.Commands.ProcessOrder
{
    public class ProcessOrderCommand : IRequest<Result<Guid>>
    {
        public required string TableNumber { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public required string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
