using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ClosedValues
{
    /// <summary>
    /// An immutable array with value equality, so that the incremental generator can cache its models.
    /// </summary>
    internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private ImmutableArray<T> Items => items.IsDefault ? ImmutableArray<T>.Empty : items;

        public int Count => Items.Length;

        public bool Equals(EquatableArray<T> other)
        {
            ImmutableArray<T> left = Items;
            ImmutableArray<T> right = other.Items;
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (T item in Items)
                {
                    hash = (hash * 31) + item.GetHashCode();
                }

                return hash;
            }
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
