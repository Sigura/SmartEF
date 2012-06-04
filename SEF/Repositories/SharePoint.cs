using System;
using System.Collections.Generic;
using System.Data.Objects;
using System.Data.Services.Client;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using SEF.Contracts;

namespace SEF.Repositories
{
    public class SharePoint : IRepository
    {
        private readonly DataServiceContext _objectContext;

        public SharePoint(DataServiceContext objectContext)
        {
            _objectContext = objectContext;
        }

        public void Dispose()
        {
        }

        public object Clone()
        {
            throw new NotImplementedException();
        }

        public IList<T> Get<T>(ILoadOptions<T> dlo) where T : class
        {
            if (dlo != null && dlo.GetPreloadedMembers().Any())
                throw new NotImplementedException();

            var query = _objectContext.CreateQuery<T>(typeof(T).Name);

            if (dlo != null)
                query = query.And(dlo.GetPredicates()) as DataServiceQuery<T>;

            return query.ToList();
        }

        public IList<T> Get<T>(params Expression<Func<T, bool>>[] predicate) where T : class
        {
            return _objectContext.CreateQuery<T>(typeof(T).Name).And(predicate).ToList();
        }

        public int Count<T>(ILoadOptions<T> dlo) where T : class
        {
            throw new NotImplementedException();
        }

        public int Count<T>(params Expression<Func<T, bool>>[] predicate) where T : class
        {
            throw new NotImplementedException();
        }

        public ObjectStateManager GetObjectStateManager()
        {
            throw new InvalidOperationException();
        }

        public void Add<T>(T entity) where T : class
        {
            if (entity.Equals(default(T)))
                throw new ArgumentNullException(@"entity");

            var entitySetType = typeof(T);

            var addMethod = _objectContext.GetType().GetMethods()
                .FirstOrDefault(m => m.Name.StartsWith(@"Add") && m.GetParameters().Count() == 1 && m.GetParameters().FirstOrDefault().ParameterType == entitySetType);

            Contract.Assume(addMethod != null, "Add method not found");

            addMethod.Invoke(_objectContext, new object[] { entity });
        }

        public void Remove<T>(T entity) where T : class
        {
            _objectContext.DeleteObject(entity);
        }

        public void Save()
        {
            _objectContext.SaveChanges(SaveChangesOptions.ReplaceOnUpdate);
        }

        public void Detach<T>(T entity) where T : class
        {
            _objectContext.Detach(entity);
        }

        public T Create<T>() where T : class, new()
        {
            return new T();
        }

        //public event CollectionChangeEventHandler ObjectStateManagerChanged;

        public void Refresh<T>(T entity) where T : class
        {

        }

        public void Attach<T>(T entity) where T : class
        {
            _objectContext.AttachTo(typeof(T).Name, entity);
        }
    }
}