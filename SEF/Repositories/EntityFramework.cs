using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Objects;
using System.Data.Objects.DataClasses;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;

namespace SEF.Repositories
{
    using Contracts;

    public class EntityFramework : IRepository
    {
        private ObjectContext _db;
        private readonly bool _isStoreWin;
        public bool AutoSaveOnDispose { get; private set; }
        public TrackingOption TrackingOption { get; private set; }

        public EntityFramework(ObjectContext objectContext, TrackingOption trackingOption = TrackingOption.RefreshAfterSave | TrackingOption.RefreshAfterSave)
            : this(objectContext, trackingOption, true)
        {
            Contract.Requires<ArgumentNullException>(objectContext != null, @"empty context");
        }

        public EntityFramework(ObjectContext objectContext, TrackingOption trackingOption, bool isStoreWin = true)
        {
            Contract.Requires<ArgumentNullException>(objectContext != null, @"objectContext");

            AutoSaveOnDispose = false;
            TrackingOption = trackingOption;
            _db = objectContext;
            _isStoreWin = isStoreWin;
            _db.ContextOptions.ProxyCreationEnabled = false;
            //_db. AutoDetectChangesEnabled 
        }

        #region Implementation of IRepository

        public int Count<T>(params Expression<Func<T, bool>>[] predicate) where T : class
        {
            var query = GetQuery<T>(null);

            query = query.And(predicate);

            Contract.Assume(query != null, @"must be withPredicate");

            return query.Count();
        }

        public ObjectStateManager GetObjectStateManager()
        {
            return _db.ObjectStateManager;
        }

        public IList<T> Get<T>(params Expression<Func<T, bool>>[] predicate) where T : class
        {
            Contract.Ensures(Contract.Result<IList<T>>() != null);

            Contract.Assume(!_disposed, "disposed");

            var query = Queryable(new LoadOptions<T>().Where(predicate));

            //query = query.And(predicate);

            Contract.Assume(query != null, @"must be withPredicate");

            return query.ToList();
        }

        public int Count<T>(ILoadOptions<T> dlo) where T : class
        {
            var query = Queryable(dlo);

            return query.Count();
        }

        //private readonly object _locker = new object();

        public IList<T> Get<T>(ILoadOptions<T> dlo) where T : class
        {
            Contract.Ensures(Contract.Result<IList<T>>() != null);
            //lock (_locker)
            //{

            var query = Queryable(dlo);

            return query.ToList();
            //}
        }

        private IQueryable<T> Queryable<T>(ILoadOptions<T> dlo) where T : class
        {
            Contract.Assume(!_disposed, "disposed");

            var query = GetQuery(dlo);

            if (dlo != null)
                query = query.And(dlo.GetPredicates());

            Contract.Assume(query != null, @"must be withPredicate");

            var limit = dlo != null ? dlo.GetLimit() : new Limit(0, int.MaxValue);

            if (dlo != null)
                query = dlo.SetOrderBy(query);

            if (limit.Skip != 0)
                query = query.Skip(limit.Skip);
            if (limit.Take != int.MaxValue)
                query = query.Take(limit.Take);

            if (IsNoTraking)
            {
                var objectQuery = query as ObjectQuery;

                if (objectQuery != null)
                    objectQuery.MergeOption = MergeOption.NoTracking;
            }
            return query;
        }

        private IQueryable<T> GetQuery<T>(ILoadOptions<T> dlo) where T : class
        {
            //looking for entityset type
            var entitySetType = ResolveEntitySetType<T>();

            var objectQueryType = typeof(ObjectQuery<>);

            Contract.Assume(objectQueryType.IsGenericTypeDefinition);
            Contract.Assume(objectQueryType.GetGenericArguments().Length == 1);

            var propertyType = objectQueryType.MakeGenericType(new[] { entitySetType });

            var pi = _db.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == propertyType);

            if (pi == null)
            {
                var objectSetType = typeof(ObjectSet<>);

                Contract.Assume(objectSetType.IsGenericTypeDefinition);
                Contract.Assume(objectSetType.GetGenericArguments().Length == 1);

                propertyType = objectSetType.MakeGenericType(new[] { entitySetType });

                pi = _db.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == propertyType);
            }

            Contract.Assume(pi != null, string.Format(@"EntitySet {0} not found", propertyType.FullName));

            var entitySet = pi.GetValue(_db, null);

            if (dlo != null)
            {
                foreach (var member in dlo.GetPreloadedMembers())
                {
                    var includeMethod = propertyType
                        .GetMethod(@"Include");

                    entitySet = includeMethod.Invoke(
                        entitySet, new[] { member });
                }
            }

            //get the exact child class
            var ofTypeMethod = propertyType
                .GetMethod(@"OfType")
                .MakeGenericMethod(new[] { typeof(T) });

            return ((IQueryable<T>)ofTypeMethod.Invoke(entitySet, null));
        }

        public void Add<T>(T entity) where T : class
        {
            Contract.Assume(!_disposed, @"disposed");

            if (entity.Equals(default(T)))
                throw new ArgumentNullException(@"entity");

            var entitySetType = ResolveEntitySetType<T>();

            var addMethod = _db.GetType().GetMethods()
                .FirstOrDefault(m => m.Name.StartsWith(@"Add") && m.GetParameters().Count() == 1 && m.GetParameters().First().ParameterType == entitySetType);

            if (addMethod == null)
            {
                addMethod = _db.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == @"AddObject" && m.GetParameters().Count() == 2);

                if (addMethod != null)
                {
                    addMethod.Invoke(_db, new object[] { entitySetType.Name, entity });

                    return;
                }
            }
            else
            {
                addMethod.Invoke(_db, new object[] { entity });

                return;
            }

            Contract.Assume(addMethod != null, @"Add method not found");
        }

        public void Remove<T>(T entity) where T : class
        {
            Contract.Assume(!_disposed, @"disposed");

            if (IsNoTraking)
            {
                Attach(entity);
            }

            _db.DeleteObject(entity);
        }

        private bool IsNoTraking
        {
            get { return (TrackingOption & TrackingOption.NoTracking) == TrackingOption.NoTracking; }
        }

        public void Save()
        {
            Contract.Assume(!_disposed, @"disposed");

            if ((TrackingOption & TrackingOption.RefreshAfterSave) == TrackingOption.RefreshAfterSave)
            {
                var entities = _db.ObjectStateManager
                    .GetObjectStateEntries(EntityState.Added | EntityState.Modified);

                Contract.Assume(entities != null);

                var changedEntity = entities
                    .Where(e => e.Entity != null);

                Contract.Assume(changedEntity != null, @"changed entity is null");

                var changedEntities = changedEntity.ToList();

                _db.SaveChanges();

                changedEntities.ForEach(e => _db.Refresh(
                    _isStoreWin ? RefreshMode.StoreWins : RefreshMode.ClientWins,
                    e.Entity)
                    );
            }
            else
            {
                _db.SaveChanges();
            }
        }

        public void Detach<T>(T entity) where T : class
        {
            _db.Detach(entity);
        }

        public T Create<T>() where T : class, new()
        {
            return _db.CreateObject<T>();
        }

        public event CollectionChangeEventHandler ObjectStateManagerChanged
        {
            add
            {
                _db.ObjectStateManager.ObjectStateManagerChanged += value;
            }
            remove
            {
                _db.ObjectStateManager.ObjectStateManagerChanged -= value;
            }
        }

        public void Refresh<T>(T entity) where T : class
        {
            //_db.GetObjectByKey(new EntityKey())
            try
            {
                _db.Refresh(
                    RefreshMode.StoreWins,
                    entity);
            }
            catch (InvalidOperationException e)
            {
                //todo: fix me!
                Debug.WriteLine(e);
            }
        }

        public void Attach<T>(T entity) where T : class
        {
            var entitySetType = ResolveEntitySetType<T>();
            ObjectStateEntry state = null;
            var objectStateManager = GetObjectStateManager();
            var isPresent = objectStateManager.TryGetObjectStateEntry(_db.CreateEntityKey(entitySetType.Name, entity), out state);

            if (!isPresent || state.State == EntityState.Detached)
                _db.AttachTo(entitySetType.Name, entity);

            try
            {
                objectStateManager.ChangeObjectState(entity, EntityState.Modified);
            }
            catch (InvalidOperationException)
            {
                _db.ApplyCurrentValues(entitySetType.Name, entity);
            }

        }

        private static Type ResolveEntitySetType<T>()
        {
            var objectType = typeof(T);
            while (objectType.BaseType != typeof(EntityObject) && objectType.BaseType != typeof(Object) && objectType.BaseType != null && !objectType.BaseType.IsAbstract)
            {
                objectType = objectType.BaseType;
            }
            return objectType;
        }

        #endregion

        #region Implementation of IDisposable

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (AutoSaveOnDispose)
                    Save();

                _db.Dispose();
            }

            _db = null;
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        ~EntityFramework()
        {
            Dispose(false);
        }

        #endregion

        #region ICloneable
        public IRepository Clone()
        {
            return new EntityFramework((ObjectContext)Activator.CreateInstance(_db.GetType(), _db.Connection.ConnectionString), TrackingOption);
        }
        object ICloneable.Clone()
        {
            return Clone();
        }
        #endregion
    }
}
