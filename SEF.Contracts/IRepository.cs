using System;
using System.Collections.Generic;
using System.Data.Objects;
using System.Linq.Expressions;

namespace SEF.Contracts
{
    public interface IRepository : IDisposable, ICloneable
    {
        IList<T> Get<T>(ILoadOptions<T> dlo) where T : class;
        IList<T> Get<T>(params Expression<Func<T, bool>>[] predicate) where T : class;

        int Count<T>(ILoadOptions<T> dlo) where T : class;
        int Count<T>(params Expression<Func<T, bool>>[] predicate) where T : class;

        ObjectStateManager GetObjectStateManager();

        void Add<T>(T entity) where T : class;
        void Remove<T>(T entity) where T : class;
        void Save();

        void Detach<T>(T entity) where T : class;

        T Create<T>() where T : class, new();

        //event CollectionChangeEventHandler ObjectStateManagerChanged;
        void Refresh<T>(T entity) where T : class;
        void Attach<T>(T entity) where T : class;
    }
}