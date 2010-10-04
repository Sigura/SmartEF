using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

namespace SmartEF
{
    public interface IRepository : IDisposable, ICloneable
    {
        IList<T> Get<T>();
        IList<T> Get<T>(DataLoadOptions dlo);
        IList<T> Get<T>(Expression<Func<T, bool>> predicate, DataLoadOptions dlo);

        IList<T> Get<T>(DataLoadOptions dlo, params Expression<Func<T, bool>>[] predicate);
        IList<T> Get<T>(params Expression<Func<T, bool>>[] predicate);

        void Add<T>(T entity);
        void Remove<T>(T entity);
        void Save();

        void Detach<T>(T entity);
        event CollectionChangeEventHandler ObjectStateManagerChanged;
    }
}