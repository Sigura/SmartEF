using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SEF.Contracts
{
    public interface ILoadOptions<T>
        where T : class
    {
        Limit GetLimit();
        ILoadOptions<T> Create();
        ILoadOptions<T> LoadWith(Expression<Func<T, object>> propExp);
        ILoadOptions<T> LoadWith(MemberInfo memberInfo);
        ILoadOptions<T> LoadWith(string path);
        //bool HasOrderBy();
        //Expression<Func<T, TR>> GetOrderBy<TR>();
        //Direction GetOrderDirection();
        //ILoadOptions<T> OrderBy(Expression<Func<T, object>> propExp, Direction dir);
        ILoadOptions<T> Where(params Expression<Func<T, bool>>[] propExp);
        ILoadOptions<T> Limit(int top, int length);
        ILoadOptions<T> Limit(Limit limit);
        ILoadOptions<TNew> Copy<TNew>(ILoadOptions<TNew> newLoadOptions)
            where TNew : class;

        string[] GetPreloadedMembers();
        void SetPreloadedMembers(params string[] members);
        Expression<Func<T, bool>>[] GetPredicates();
        //void OrderBy<TT>(Tuple<Expression<Func<TT, object>>, Direction> getOrderBy);
        ILoadOptions<T> OrderByDescending<TR>(Expression<Func<T, TR>> func);
        ILoadOptions<T> OrderBy<TR>(Expression<Func<T, TR>> func);
        Direction GetDirection();
        IQueryable<T> SetOrderBy(IQueryable<T> query);
        void SetOrderBy(Tuple<string, Direction> getOrderBy);
    }
}