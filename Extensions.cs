using System;
using System.Linq;
using System.Linq.Expressions;

namespace SmartEF
{
    static class Extensions
    {
        public static IQueryable<T> And<T>(this IQueryable<T> query, params Expression<Func<T, bool>>[] predicates)
        {

            return predicates
                .Aggregate(query, (result, predicate) => result.Where(predicate));

        }
    }
}