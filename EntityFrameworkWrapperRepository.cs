using System;
using System.Data;
using System.Data.Objects;
using System.Data.Objects.DataClasses;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.ComponentModel;

namespace SmartEF
{
    public class EntityFrameworkWrapperRepository : IRepository
    {
        private ObjectContext _db;
        public bool AutoSaveOnDispose { get; private set; }
        public bool AutoRefreshAfterSave { get; private set; }

        public EntityFrameworkWrapperRepository(ObjectContext objectContext, bool autoRefreshAfterSave)
        {
            if (objectContext == null)
                throw new ArgumentNullException("objectContext");

            AutoSaveOnDispose = false;
            AutoRefreshAfterSave = autoRefreshAfterSave;
            _db = objectContext;
        }

        //private EntityFrameworkWrapperRepository(ObjectContext objectContext, bool autoSaveOnDispose) : this(objectContext)
        //{
        //    AutoSaveOnDispose = autoSaveOnDispose;
        //}

        #region Implementation of IRepository

        public IList<T> Get<T>()
        {
            return Get<T>(t => true);
        }

        public IList<T> Get<T>(DataLoadOptions dlo)
        {
            return Get<T>(t => true, dlo);
        }

        public IList<T> Get<T>(Expression<Func<T, bool>> predicate, DataLoadOptions dlo)
        {
            return Get(dlo, new [] {predicate});
        }

        public IList<T> Get<T>(params Expression<Func<T, bool>>[] predicate)
        {
            return Get(null, predicate);
        }

        public IList<T> Get<T>(DataLoadOptions dlo, params Expression<Func<T, bool>>[] predicate)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().ToString());

            //looking for entityset type
            var entitySetType = ResolveEntitySetType<T>();

            var propertyType = typeof(ObjectQuery<>).MakeGenericType(new[] { entitySetType });

            var pi = _db.GetType().GetProperties()
                .Where(p => p.PropertyType == propertyType)
                .FirstOrDefault();

            if (pi == null)
            {
                propertyType = typeof(ObjectSet<>).MakeGenericType(new[] { entitySetType });

                pi = _db.GetType().GetProperties()
                    .Where(p => p.PropertyType == propertyType)
                    .FirstOrDefault();

            }

            if (pi == null)
                throw new InvalidOperationException("EntitySet not found");

            var entitySet = pi.GetValue(_db, null);

            if (dlo != null)
            {
                foreach (var member in dlo.GetPreloadedMembers())
                {
                    var includeMethod = propertyType
                        .GetMethod("Include");

                    entitySet = includeMethod.Invoke(
                        entitySet, new[]
                        {
                            dlo.ResolvePath<T>(member)
                        });
                }
            }

            //get the exact child class
            var ofTypeMethod = propertyType
                .GetMethod("OfType")
                .MakeGenericMethod(new[] { typeof(T) });

            return ((IQueryable<T>)ofTypeMethod.Invoke(entitySet, null))
                .And(predicate)
                .ToList();
        }

        public void Add<T>(T entity)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().ToString());
            
            if (entity.Equals(default(T)))
                throw new ArgumentNullException("entity");

            var entitySetType = ResolveEntitySetType<T>();

            var addMethod = _db.GetType().GetMethods()
                .Where(m => m.Name.StartsWith("Add"))
                .Where(m => m.GetParameters().Count() == 1)
                .Where(m => m.GetParameters().FirstOrDefault().ParameterType == entitySetType)
                .FirstOrDefault();

            if (addMethod == null)
            {
                addMethod = _db.GetType().GetMethods()
                    .Where(m => m.Name == "AddObject")
                    .Where(m => m.GetParameters().Count() == 2)
                    .FirstOrDefault();

                if (addMethod != null)
                {
                    addMethod.Invoke(_db, new object[] {entitySetType.Name, entity});

                    return;
                }

            }
            else
            {
                addMethod.Invoke(_db, new object[] { entity });

                return;
            }

            if (addMethod == null)
                throw new InvalidOperationException("Add method not found");

        }

        public void Remove<T>(T entity)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().ToString());

            _db.DeleteObject(entity);
        }

        public void Save()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().ToString());

            if (AutoRefreshAfterSave)
            {
                var changedEntities = _db.ObjectStateManager
                    .GetObjectStateEntries(EntityState.Added | EntityState.Modified)
                    .Where(e => e.Entity != null)
                    .Select(e => e.Entity).ToList();

                _db.SaveChanges();

                changedEntities.ForEach(e => _db.Refresh(RefreshMode.StoreWins, e));
            }
            else
            {
                _db.SaveChanges();
            }
        }

        public void Detach<T>(T entity)
        {
            _db.Detach(entity);
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

        private static Type ResolveEntitySetType<T>()
        {
            var objectType = typeof(T);
            while (objectType.BaseType != typeof(EntityObject) && objectType.BaseType != typeof(Object))
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
                if(AutoSaveOnDispose)
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

        ~EntityFrameworkWrapperRepository()
        {
            Dispose(false);
        }

        #endregion        

        #region ICloneable
        public IRepository Clone()
        {
            return new EntityFrameworkWrapperRepository((ObjectContext)Activator.CreateInstance(_db.GetType(), _db.Connection.ConnectionString), AutoRefreshAfterSave);
        }
        object ICloneable.Clone()
        {
            return Clone();
        }
        #endregion
    }
}