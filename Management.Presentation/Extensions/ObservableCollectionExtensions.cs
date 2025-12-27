using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Management.Presentation.Extensions
{
    /// <summary>
    /// Helper methods for managing ObservableCollections without breaking UI bindings.
    /// </summary>
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// Clears the collection and repopulates it with the new items.
        /// Useful for Search/Filter operations where the entire list changes.
        /// </summary>
        public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> newItems)
        {
            if (collection == null) return;

            collection.Clear();

            if (newItems != null)
            {
                foreach (var item in newItems)
                {
                    collection.Add(item);
                }
            }
        }

        /// <summary>
        /// Adds a set of items to the end of the collection.
        /// Useful for Pagination (Infinite Scroll) to append new data.
        /// </summary>
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> newItems)
        {
            if (collection == null || newItems == null) return;

            foreach (var item in newItems)
            {
                collection.Add(item);
            }
        }
    }
}