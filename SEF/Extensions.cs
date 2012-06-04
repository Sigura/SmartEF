using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;

namespace SEF
{
    static class Extensions
    {
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> array, Action<T> action)
        {
            if (array == null)
                return null;

            foreach (var item in array)
            {
                action.Invoke(item);
            }

            return array;
        }

        internal static void ForEach<T>(this T[] array, Action<T> action)
        {
            if (array == null)
                return;

            Array.ForEach(array, action);
        }

        public static IQueryable<T> And<T>(this IQueryable<T> query, params Expression<Func<T, bool>>[] predicates)
        {

            return predicates
                .Aggregate(query, (result, predicate) => result.Where(predicate));

        }

        public static IQueryable<T> ApplyOrder<T>(this IQueryable<T> query, string name, string methodName)
        {
            Contract.Requires<ArgumentNullException>(query != null);
            Contract.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Contract.Ensures(Contract.Result<IQueryable<T>>() != null);

            var type = typeof(T);
            var param = Expression.Parameter(type, @"item");
            var propertyInfo = type.GetProperty(name);

            Contract.Assume(propertyInfo != null);

            var expr = Expression.Property(param, propertyInfo);
            var propertyType = propertyInfo.PropertyType;

            var typeDelegate = typeof(Func<,>);

            Contract.Assume(typeDelegate.IsGenericTypeDefinition);
            Contract.Assume(typeDelegate.GetGenericArguments().Length == 2);

            var delegateType = typeDelegate.MakeGenericType(typeof(T), propertyType);

            var lambda = Expression.Lambda(delegateType, expr, param);

            var method = typeof(Queryable).GetMethods().Single(
                m => m.Name == methodName
                          && m.IsGenericMethodDefinition
                          && m.GetGenericArguments().Length == 2
                          && m.GetParameters().Length == 2);

            var gm = method.MakeGenericMethod(typeof(T), propertyType);

            var q = gm.Invoke(null, new object[] { query, lambda }) as IQueryable<T>;

            Contract.Assume(q != null);

            return q;
        }

        public static Expression<Func<T, bool>> And<T>(this IEnumerable<Expression<Func<T, bool>>> predicates)
        {
            return And((Expression<Func<T, bool>>)null, predicates.ToArray());
        }

        public static Expression<Func<T, bool>> Or<T>(this IEnumerable<Expression<Func<T, bool>>> predicates)
        {
            return Or(null, predicates.ToArray());
        }

        public static Expression<T> Compose<T>(this Expression<T> first, Expression<T> second, Func<Expression, Expression, Expression> merge)
        {
            if (first == null)
                return second;

            var map = first.Parameters
                .Select((f, i) => new { f, s = second.Parameters[i] })
                .ToDictionary(p => p.s, p => p.f);

            var secondBody = ParameterRebinderVisitor.ReplaceParameters(map, second.Body);

            return Expression.Lambda<T>(merge(first.Body, secondBody), first.Parameters);
        }

        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> first, params Expression<Func<T, bool>>[] second)
        {
            second.ForEach(p => first = first.Compose(p, Expression.And));

            return first;
        }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, params Expression<Func<T, bool>>[] second)
        {
            second.ForEach(p => first = first.Compose(p, Expression.Or));

            return first;
        }

        //http://blogs.msdn.com/b/meek/archive/2008/05/02/linq-to-entities-combining-predicates.aspx
        class ParameterRebinderVisitor : ExpressionVisitor
        {
            private readonly Dictionary<ParameterExpression, ParameterExpression> _map;

            ParameterRebinderVisitor(Dictionary<ParameterExpression, ParameterExpression> map)
            {
                _map = map ?? new Dictionary<ParameterExpression, ParameterExpression>();
            }

            public static Expression ReplaceParameters(Dictionary<ParameterExpression, ParameterExpression> map, Expression exp)
            {
                return new ParameterRebinderVisitor(map).Visit(exp);
            }

            protected override Expression VisitParameter(ParameterExpression p)
            {
                ParameterExpression replacement;
                if (_map.TryGetValue(p, out replacement))
                {
                    p = replacement;
                }
                return base.VisitParameter(p);
            }
        }

        class ConversionVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ConversionVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                //if (node != _oldParameter) // if instance is not old parameter - do nothing 
                //    return base.VisitParameter(node);

                return _newParameter; // replace all old param references with new ones 
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression != _oldParameter) // if instance is not old parameter - do nothing 
                    return base.VisitMember(node);

                var newObj = Visit(node.Expression);
                var newMember = _newParameter.Type.GetMember(node.Member.Name).FirstOrDefault();

                if (newMember == null)
                    throw new FieldAccessException(string.Format(@"not found '{0}'", node.Member.Name));

                return Expression.MakeMemberAccess(newObj, newMember);
            }
        }

        public static Expression<Func<TTo, bool>> ChangeType<TFrom, TTo>(
            this Expression<Func<TFrom, bool>> expression
            )
        {
            return expression.ChangeType<TFrom, TTo, bool>();
        }

        public static Expression<Func<T, bool>> ReplaceType<T, TFrom, TTo>(
            this Expression<Func<T, bool>> expression
            )
        {
            return expression.ReplaceType<T, TFrom, TTo, bool>();
        }

        public static Expression<Func<T, TR>> ReplaceType<T, TFrom, TTo, TR>(
            this Expression<Func<T, TR>> expression
            )
        {
            var param = expression.Parameters[0];
            var newParam = Expression.Parameter(typeof(TTo), param.Name);
            var converter = new ConversionVisitor(param, newParam);
            var newBody = converter.Visit(expression.Body);
            try
            {
                return Expression.Lambda<Func<T, TR>>(newBody, typeof(TFrom) == param.Type ? newParam : param);
            }
            catch (FieldAccessException exceptoin)
            {
                throw new FieldAccessException(
                    string.Format(@"can not convert {0} to repository predicate", expression),
                    exceptoin);
            }
        }

        public static Expression<Func<TTo, TR>> ChangeType<TFrom, TTo, TR>(
            this Expression<Func<TFrom, TR>> expression
            )
        {
            var param = expression.Parameters[0];
            var newParam = Expression.Parameter(typeof(TTo), param.Name);
            var converter = new ConversionVisitor(param, newParam);
            var newBody = converter.Visit(expression.Body);
            try
            {
                return Expression.Lambda<Func<TTo, TR>>(newBody, /*typeof(TFrom) == param.Type ? */newParam/* : param*/);
            }
            catch (FieldAccessException exceptoin)
            {
                throw new FieldAccessException(
                    string.Format(@"can not convert {0} to repository predicate", expression),
                    exceptoin);
            }
        }
    }
}