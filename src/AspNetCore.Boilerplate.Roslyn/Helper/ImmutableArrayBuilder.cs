using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace AspNetCore.Boilerplate.Roslyn.Helper;

internal ref struct ImmutableArrayBuilder<T>
{
    /// <summary>
    ///     The rented <see cref="Writer" /> instance to use.
    /// </summary>
    private Writer? _writer;

    /// <summary>
    ///     Creates a <see cref="ImmutableArrayBuilder{T}" /> value with a pooled underlying data writer.
    /// </summary>
    /// <returns>A <see cref="ImmutableArrayBuilder{T}" /> instance to write data to.</returns>
    public static ImmutableArrayBuilder<T> Rent()
    {
        return new ImmutableArrayBuilder<T>(new Writer());
    }

    /// <summary>
    ///     Creates a new <see cref="ImmutableArrayBuilder{T}" /> object with the specified parameters.
    /// </summary>
    /// <param name="writer">The target data writer to use.</param>
    private ImmutableArrayBuilder(Writer writer)
    {
        _writer = writer;
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Count" />
    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _writer!.Count;
    }

    /// <summary>
    ///     Gets the data written to the underlying buffer so far, as a <see cref="ReadOnlySpan{T}" />.
    /// </summary>
    public readonly ReadOnlySpan<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _writer!.WrittenSpan;
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Add(T)" />
    public readonly void Add(T item)
    {
        _writer!.Add(item);
    }

    /// <summary>
    ///     Adds the specified items to the end of the array.
    /// </summary>
    /// <param name="items">The items to add at the end of the array.</param>
    public readonly void AddRange(scoped ReadOnlySpan<T> items)
    {
        _writer!.AddRange(items);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.ToImmutable" />
    public readonly ImmutableArray<T> ToImmutable()
    {
        var array = _writer!.WrittenSpan.ToArray();

        return Unsafe.As<T[], ImmutableArray<T>>(ref array);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.ToArray" />
    public readonly T[] ToArray()
    {
        return _writer!.WrittenSpan.ToArray();
    }

    /// <summary>
    ///     Gets an <see cref="IEnumerable{T}" /> instance for the current builder.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}" /> instance for the current builder.</returns>
    /// <remarks>
    ///     The builder should not be mutated while an enumerator is in use.
    /// </remarks>
    public readonly IEnumerable<T> AsEnumerable()
    {
        return _writer!;
    }

    /// <inheritdoc />
    public readonly override string ToString()
    {
        return _writer!.WrittenSpan.ToString();
    }

    /// <inheritdoc cref="IDisposable.Dispose" />
    public void Dispose()
    {
        var writer = _writer;

        _writer = null;

        writer?.Dispose();
    }

    /// <summary>
    ///     A class handling the actual buffer writing.
    /// </summary>
    private sealed class Writer : ICollection<T>, IDisposable
    {
        private T?[]? _array;

        /// <summary>
        ///     The starting offset within <see cref="_array" />.
        /// </summary>
        private int _index;

        /// <summary>
        ///     Creates a new <see cref="Writer" /> instance with the specified parameters.
        /// </summary>
        public Writer()
        {
            _array = ArrayPool<T?>.Shared.Rent(typeof(T) == typeof(char) ? 1024 : 8);
            _index = 0;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.WrittenSpan" />
        public ReadOnlySpan<T> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_array!, 0, _index);
        }

        /// <inheritdoc />
        bool ICollection<T>.IsReadOnly => true;

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Count" />
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _index;
        }

        /// <inheritdoc />
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            var array = _array!;
            var length = _index;

            for (var i = 0; i < length; i++)
                yield return array[i]!;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var array = _array;

            _array = null;

            if (array is not null)
                ArrayPool<T?>.Shared.Return(array, typeof(T) != typeof(char));
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Add" />
        public void Add(T value)
        {
            EnsureCapacity(1);

            _array![_index++] = value;
        }

        /// <inheritdoc />
        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        bool ICollection<T>.Contains(T item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(_array!, 0, array, arrayIndex, _index);
        }

        /// <inheritdoc />
        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.AddRange" />
        public void AddRange(ReadOnlySpan<T> items)
        {
            EnsureCapacity(items.Length);

            items.CopyTo(_array.AsSpan(_index)!);

            _index += items.Length;
        }

        /// <summary>
        ///     Ensures that <see cref="_array" /> has enough free space to contain a given number of new items.
        /// </summary>
        /// <param name="requestedSize">The minimum number of items to ensure space for in <see cref="_array" />.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requestedSize)
        {
            if (requestedSize > _array!.Length - _index)
                ResizeBuffer(requestedSize);
        }

        /// <summary>
        ///     Resizes <see cref="_array" /> to ensure it can fit the specified number of new items.
        /// </summary>
        /// <param name="sizeHint">The minimum number of items to ensure space for in <see cref="_array" />.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ResizeBuffer(int sizeHint)
        {
            var minimumSize = _index + sizeHint;

            var oldArray = _array!;
            var newArray = ArrayPool<T?>.Shared.Rent(minimumSize);

            Array.Copy(oldArray, newArray, _index);

            _array = newArray;

            ArrayPool<T?>.Shared.Return(oldArray, typeof(T) != typeof(char));
        }
    }
}