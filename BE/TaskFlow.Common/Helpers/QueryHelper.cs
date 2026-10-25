using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Common.Helpers
{
    public static class QueryHelper
    {
        public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string sortColumn, bool ascending) where T : class
        {
            var propertyInfo = typeof(T).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, sortColumn, StringComparison.OrdinalIgnoreCase));

            if (propertyInfo != null)
            {
                return ascending
                    ? query.OrderBy(entity => EF.Property<object>(entity, propertyInfo.Name))
                    : query.OrderByDescending(entity => EF.Property<object>(entity, propertyInfo.Name));
            }
            else
            {
                throw new InvalidFilterCriteriaException($"The property '{sortColumn}' is not supported for sorting.");
            }
        }

        public static IEnumerable<T> ApplySorting<T>(this IEnumerable<T> query, string sortColumn, bool ascending) where T : class
        {
            var propertyInfo = typeof(T).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, sortColumn, StringComparison.OrdinalIgnoreCase));

            if (propertyInfo != null)
            {
                return ascending
                    ? query.OrderBy(entity => EF.Property<object>(entity, propertyInfo.Name))
                    : query.OrderByDescending(entity => EF.Property<object>(entity, propertyInfo.Name));
            }
            else
            {
                throw new InvalidFilterCriteriaException($"The property '{sortColumn}' is not supported for sorting.");
            }
        }
    }
}
