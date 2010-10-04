using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SmartEF
{
    public class DataLoadOptions
    {
        private static readonly string[] ValidTypes = new[] { "ComplexObject ", "StructuralObject", "EntityObject", "EntityCollection`1" };
        private readonly Dictionary<MetaPosition, MemberInfo> _includes;

        public DataLoadOptions()
        {
            _includes = new Dictionary<MetaPosition, MemberInfo>();
        }

        public MemberInfo[] GetPreloadedMembers()
        {
            //var type = typeof(TEntity);

            return _includes.Values.OfType<PropertyInfo>()
                //.Where(prop => prop.DeclaringType == type)
                .Cast<MemberInfo>()
                .ToArray();
        }

        public MemberInfo[] GetPreloadedMembers<TEntity>()
        {
            var type = typeof(TEntity);

            return _includes.Values.OfType<PropertyInfo>()
                .Where(prop => prop.DeclaringType == type)
                .Cast<MemberInfo>()
                .ToArray();
        }

        internal bool IsPreloaded(MemberInfo member)
        {
            return _includes.ContainsKey(new MetaPosition(member));
        }

        public DataLoadOptions LoadWith<T>(Expression<Func<T, object>> expression)
        {
            LoadWith(GetLoadWithMemberInfo(expression));

            return this;
        }

        public DataLoadOptions LoadWith(MemberInfo memberInfo)
        {
            if (IsPreloaded(memberInfo))
            {
                throw new InvalidOperationException("Association is already added");
            }

            Preload(memberInfo);

            return this;
        }

        private void Preload(MemberInfo association)
        {

            _includes.Add(new MetaPosition(association), association);
        }

        private static MemberInfo GetLoadWithMemberInfo(LambdaExpression lambda)
        {
            var body = lambda.Body;
            if ((body != null) && ((body.NodeType == ExpressionType.Convert) || (body.NodeType == ExpressionType.ConvertChecked)))
            {
                body = ((UnaryExpression)body).Operand;
            }
            var expression = body as MemberExpression;

            //ValidateMemberExpression(expression);

            if (expression != null) return expression.Member;

            throw new InvalidOperationException("The expression specified must be of the form p.A, where p is the parameter and A is a property member.");
        }

        private static void ValidateMemberExpression(MemberExpression expression)
        {
            if ((expression == null) || (expression.Expression.NodeType != ExpressionType.Parameter) || expression.Member.MemberType != MemberTypes.Property)
            {
                throw new InvalidOperationException("The expression specified must be of the form p.A, where p is the parameter and A is a property member.");
            }
            var member = expression.Member as PropertyInfo;

            if (member == null)
            {
                throw new InvalidOperationException(String.Format("{0} must be property member", expression));
            }
            //Member type must be EntityCollection<T> or StructuralObject
            var isEntityCollection = ValidTypes.Any(s => s == member.PropertyType.Name);
            var isEntity = member.PropertyType.BaseType != null &&
                           ValidTypes.Any(s => s == member.PropertyType.BaseType.Name);

            if (isEntity || isEntityCollection)
                return;

            var errorMsg =
                String.Format(
                    "Related end \"{0}\" must be of type that implements System.Data.Objects.DataClasses.EntityCollection<T> or System.Data.Objects.DataClasses.IEntityWithRelationships",
                    expression);
            throw new InvalidOperationException(errorMsg);
        }

        private struct MetaPosition : IEqualityComparer<MetaPosition>, IEqualityComparer
        {
            private readonly int _metadataToken;

            private readonly Assembly _assembly;

            internal MetaPosition(MemberInfo mi)
                : this(mi.DeclaringType.Assembly, mi.MetadataToken)
            {
            }
            private MetaPosition(Assembly assembly, int metadataToken)
            {
                _assembly = assembly;
                _metadataToken = metadataToken;
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }
                return obj.GetType() == GetType() && AreEqual(this, (MetaPosition)obj);
            }
            public bool Equals(MetaPosition x, MetaPosition y)
            {
                return AreEqual(x, y);
            }
            public override int GetHashCode()
            {
                return _metadataToken;
            }
            public int GetHashCode(MetaPosition obj)
            {
                return obj._metadataToken;
            }

            public static bool operator ==(MetaPosition x, MetaPosition y)
            {
                return AreEqual(x, y);
            }
            public static bool operator !=(MetaPosition x, MetaPosition y)
            {
                return !AreEqual(x, y);
            }

            bool IEqualityComparer.Equals(object x, object y)
            {
                return Equals((MetaPosition)x, (MetaPosition)y);
            }
            int IEqualityComparer.GetHashCode(object obj)
            {
                return GetHashCode((MetaPosition)obj);
            }

            private static bool AreEqual(MetaPosition x, MetaPosition y)
            {
                return ((x._metadataToken == y._metadataToken) && (x._assembly == y._assembly));
            }

        }

        private static string GetPath(Type type, MemberInfo member)
        {
            var prop = type.GetProperties()
                .FirstOrDefault(
                    p => p.PropertyType == member.DeclaringType ||
                        p.PropertyType.GetGenericArguments().FirstOrDefault() == member.DeclaringType
                );

            return prop != null ? string.Format("{0}.{1}", member.DeclaringType.Name, member.Name) : null;
        }

        public string ResolvePath<T>(MemberInfo member)
        {
            var type = typeof (T);

            if(member.DeclaringType == type)
                return member.Name;


            
            var path = GetPath(type, member);

            if (path != null)
                return path;

            foreach (var prop2 in type.GetProperties())
            {
                path = GetPath(prop2.PropertyType, member);

                if (path != null)
                    return string.Format("{0}.{1}", prop2.Name, path);
            }

            return member.Name;
        }
    }
}