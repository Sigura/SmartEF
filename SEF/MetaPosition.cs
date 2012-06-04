using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SEF
{
    struct MetaPosition : IEqualityComparer<MetaPosition>, IEqualityComparer
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
}