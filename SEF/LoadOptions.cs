using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SEF
{
    using Contracts;

    public class LoadOptions<T> : ILoadOptions<T>
        where T : class
    {
        private readonly Dictionary<MetaPosition, MemberInfo> _includes;
        private readonly List<Expression<Func<T, bool>>> _predicates;
        private Limit _limit;
        private readonly IList<string> _includePaths;
        private string _order;
        private Direction _dir;

        public LoadOptions()
        {
            _includes = new Dictionary<MetaPosition, MemberInfo>();
            _limit = new Limit(0, int.MaxValue);
            _includePaths = new List<string>();
            _predicates = new List<Expression<Func<T, bool>>>();
        }

        public ILoadOptions<TNew> Copy<TNew>(ILoadOptions<TNew> newLoadOptions) where TNew : class
        {
            newLoadOptions.Limit(GetLimit());
            newLoadOptions.SetOrderBy(GetOrderBy());
            newLoadOptions.Where(GetPredicates().Select(p => p.ChangeType<T, TNew>()).ToArray());
            newLoadOptions.SetPreloadedMembers(GetPreloadedMembers());

            return newLoadOptions;
        }

        private Tuple<string, Direction> GetOrderBy()
        {
            return new Tuple<string, Direction>(_order, _dir);
        }

        public string[] GetPreloadedMembers()
        {
            var type = typeof(T);

            return _includes.Values.OfType<PropertyInfo>()
                .Where(prop => prop.DeclaringType == type)
                .Cast<MemberInfo>()
                .Select(ResolvePath<T>)
                .Concat(_includePaths)
                .Distinct()
                .ToArray();
        }

        public void SetPreloadedMembers(params string[] members)
        {
            members.ForEach(_includePaths.Add);
        }

        public Expression<Func<T, bool>>[] GetPredicates()
        {
            return _predicates.ToArray();
        }

        public ILoadOptions<T> OrderByDescending<TR>(Expression<Func<T, TR>> func)
        {
            _order = Reflect<T>.GetProperty(func).Name;
            _dir = Direction.Desc;
            return this;
        }

        public ILoadOptions<T> OrderBy<TR>(Expression<Func<T, TR>> func)
        {
            _order = Reflect<T>.GetProperty(func).Name;
            _dir = Direction.Asc;
            return this;
        }

        public IQueryable<T> SetOrderBy(IQueryable<T> query)
        {
            return !string.IsNullOrWhiteSpace(_order)
                       ? query.ApplyOrder(_order, _dir == Direction.Asc ? @"OrderBy" : @"OrderByDescending")
                       : query;
        }

        public void SetOrderBy(Tuple<string, Direction> getOrderBy)
        {
            _order = getOrderBy.Item1;
            _dir = getOrderBy.Item2;
        }

        public Direction GetDirection()
        {
            return _dir;
        }

        public Limit GetLimit()
        {
            return _limit;
        }

        public ILoadOptions<T> Create()
        {
            return new LoadOptions<T>();
        }

        private void PreloadLoadWith(MemberInfo association)
        {
            _includes.Add(new MetaPosition(association), association);
        }

        private static MemberInfo GetLoadWithMemberInfo(LambdaExpression lambda)
        {
            var body = lambda.Body;
            if ((body.NodeType == ExpressionType.Convert) || (body.NodeType == ExpressionType.ConvertChecked))
            {
                body = ((UnaryExpression)body).Operand;
            }
            var expression = body as MemberExpression;

            if (expression != null) return expression.Member;

            throw new InvalidOperationException("The expression specified must be of the form p.A, where p is the parameter and A is a property member.");
        }

        internal bool IsLoadWithPreloaded(MemberInfo member)
        {
            return _includes.ContainsKey(new MetaPosition(member));
        }

        public ILoadOptions<T> LoadWith(Expression<Func<T, object>> expression)
        {
            LoadWith(GetLoadWithMemberInfo(expression));

            return this;
        }

        private static string GetPath(Type type, MemberInfo member)
        {
            var prop = type.GetProperties()
                .FirstOrDefault(
                    p => p.PropertyType == member.DeclaringType ||
                        p.PropertyType.GetGenericArguments().FirstOrDefault() == member.DeclaringType
                );

            return prop != null ? string.Format("{0}.{1}", prop.Name, member.Name) : null;
        }

        private static string ResolvePath(Type type, MemberInfo member)
        {
            if (member.DeclaringType == type)
                return member.Name;

            var path = GetPath(type, member);

            return path ?? member.Name;

            //foreach (var prop2 in type.GetProperties())
            //{
            //    path = GetPath(prop2.PropertyType, member);

            //    if (path != null)
            //        return string.Format("{0}.{1}", prop2.Name, path);
            //}
        }

        private static string ResolvePath<TEntity>(MemberInfo member)
        {
            return ResolvePath(typeof(TEntity), member);
        }

        public ILoadOptions<T> LoadWith(string path)
        {
            if (_includePaths.Contains(path))
            {
                return this;
                //throw new InvalidOperationException(string.Format("Association \"{0}\" is already added", path));
            }

            _includePaths.Add(path);

            return this;
        }


        public ILoadOptions<T> LoadWith(MemberInfo memberInfo)
        {
            if (IsLoadWithPreloaded(memberInfo))
            {
                return this;
                //throw new InvalidOperationException(string.Format("Association \"{0}.{1}\" is already added", memberInfo.DeclaringType.Name, memberInfo.Name));
            }

            PreloadLoadWith(memberInfo);

            return this;
        }

        public ILoadOptions<T> Where(params Expression<Func<T, bool>>[] expression)
        {
            expression.ForEach(_predicates.Add);

            return this;
        }

        public ILoadOptions<T> Limit(Limit limit)
        {
            _limit = limit;

            return this;
        }

        public ILoadOptions<T> Limit(int top, int length)
        {
            _limit.Skip = top;
            _limit.Take = length;

            return this;
        }
    }
}