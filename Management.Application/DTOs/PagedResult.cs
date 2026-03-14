using System;
using System.Collections.Generic;
using System.Linq;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        
        public PagedResult() { }

        public PagedResult(IEnumerable<T> items, int totalCount, int pageNumber = 1, int pageSize = 20)
        {
            Items = items;
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }

        public static PagedResult<T> Empty() => new PagedResult<T>();
    }
}
