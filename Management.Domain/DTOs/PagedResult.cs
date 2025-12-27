using System;
using System.Collections.Generic;
using System.Linq;

namespace Management.Domain.DTOs
{
    /// <summary>
    /// A generic wrapper for paginated data responses.
    /// Used by Services to return data + metadata to the ViewModels.
    /// </summary>
    /// <typeparam name="T">The type of DTO being paginated (e.g., MemberDto).</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// The collection of items for the current page.
        /// </summary>
        public IEnumerable<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// The total number of items available across all pages.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// The current page number (1-based).
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// The number of items requested per page.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Calculated property: Total number of pages based on Count and Size.
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

        /// <summary>
        /// Helper to check if there is a next page.
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;

        /// <summary>
        /// Helper to check if there is a previous page.
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        // Constructor for empty results
        public PagedResult() { }

        // Constructor for populated results
        public PagedResult(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
        {
            Items = items ?? Enumerable.Empty<T>();
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }
    }
}