// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EditorBrowsableAttribute = System.ComponentModel.EditorBrowsableAttribute;
using EditorBrowsableState = System.ComponentModel.EditorBrowsableState;

namespace System
{
    /// <summary>
    /// Represents a contiguous region of memory, similar to <see cref="ReadOnlySpan{T}"/>.
    /// Unlike <see cref="ReadOnlySpan{T}"/>, it is not a byref-like type.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [DebuggerTypeProxy(typeof(MemoryDebugView<>))]
    public readonly struct ReadOnlyMemory<T>
    {
        // NOTE: With the current implementation, Memory<T> and ReadOnlyMemory<T> must have the same layout,
        // as code uses Unsafe.As to cast between them.

        // The highest order bit of _index is used to discern whether _object is an array/string or an owned memory
        // if (_index >> 31) == 1, _object is an OwnedMemory<T>
        // else, _object is a T[] or string
        private readonly object _object;
        private readonly int _index;
        private readonly int _length;

        private const int RemoveOwnedFlagBitMask = 0x7FFFFFFF;

        /// <summary>
        /// Creates a new memory over the entirety of the target array.
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="array"/> is a null
        /// reference (Nothing in Visual Basic).</exception>
        /// <exception cref="System.ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory(T[] array)
        {
            if (array == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);

            _object = array;
            _index = 0;
            _length = array.Length;
        }

        /// <summary>
        /// Creates a new memory over the portion of the target array beginning
        /// at 'start' index and ending at 'end' index (exclusive).
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <param name="start">The index at which to begin the memory.</param>
        /// <param name="length">The number of items in the memory.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="array"/> is a null
        /// reference (Nothing in Visual Basic).</exception>
        /// <exception cref="System.ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> or end index is not in the range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory(T[] array, int start, int length)
        {
            if (array == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException();

            _object = array;
            _index = start;
            _length = length;
        }

        /// <summary>Creates a new memory over the existing object, start, and length.  No validation is performed.</summary>
        /// <param name="obj">The target object.</param>
        /// <param name="start">The index at which to begin the memory.</param>
        /// <param name="length">The number of items in the memory.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyMemory(object obj, int start, int length)
        {
            // No validation performed; caller must provide any necessary validation.
            _object = obj;
            _index = start;
            _length = length;
        }

        //Debugger Display = {T[length]}
        private string DebuggerDisplay => string.Format("{{{0}[{1}]}}", typeof(T).Name, _length);

        /// <summary>
        /// Defines an implicit conversion of an array to a <see cref="ReadOnlyMemory{T}"/>
        /// </summary>
        public static implicit operator ReadOnlyMemory<T>(T[] array) => new ReadOnlyMemory<T>(array);
        
        /// <summary>
        /// Defines an implicit conversion of a <see cref="ArraySegment{T}"/> to a <see cref="ReadOnlyMemory{T}"/>
        /// </summary>
        public static implicit operator ReadOnlyMemory<T>(ArraySegment<T> arraySegment) => new ReadOnlyMemory<T>(arraySegment.Array, arraySegment.Offset, arraySegment.Count);

        /// <summary>
        /// Returns an empty <see cref="ReadOnlyMemory{T}"/>
        /// </summary>
        public static ReadOnlyMemory<T> Empty { get; } = Array.Empty<T>();

        /// <summary>
        /// The number of items in the memory.
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// Returns true if Length is 0.
        /// </summary>
        public bool IsEmpty => _length == 0;

        /// <summary>
        /// Forms a slice out of the given memory, beginning at 'start'.
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> index is not in range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<T> Slice(int start)
        {
            if ((uint)start > (uint)_length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            }

            return new ReadOnlyMemory<T>(_object, _index + start, _length - start);
        }

        /// <summary>
        /// Forms a slice out of the given memory, beginning at 'start', of given length
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <param name="length">The desired length for the slice (exclusive).</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> or end index is not in range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<T> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            }

            return new ReadOnlyMemory<T>(_object, _index + start, length);
        }

        /// <summary>
        /// Returns a span from the memory.
        /// </summary>
        public ReadOnlySpan<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_index < 0)
                {
                    return ((OwnedMemory<T>)_object).Span.Slice(_index & RemoveOwnedFlagBitMask, _length);
                }
                else if (typeof(T) == typeof(char) && _object is string s)
                {
                    return new ReadOnlySpan<T>(ref Unsafe.As<char, T>(ref s.GetRawStringData()), s.Length).Slice(_index, _length);
                }
                else
                {
                    return new ReadOnlySpan<T>((T[])_object, _index, _length);
                }
            }
        }

        /// <summary>Creates a handle for the memory.</summary>
        /// <param name="pin">
        /// If pin is true, the GC will not move the array until the returned <see cref="MemoryHandle"/>
        /// is disposed, enabling the memory's address can be taken and used.
        /// </param>
        public unsafe MemoryHandle Retain(bool pin = false)
        {
            MemoryHandle memoryHandle;
            if (pin)
            {
                if (_index < 0)
                {
                    memoryHandle = ((OwnedMemory<T>)_object).Pin();
                    memoryHandle.AddOffset((_index & RemoveOwnedFlagBitMask) * Unsafe.SizeOf<T>());
                }
                else if (typeof(T) == typeof(char) && _object is string s)
                {
                    GCHandle handle = GCHandle.Alloc(s, GCHandleType.Pinned);
                    void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref s.GetRawStringData()), _index);
                    memoryHandle = new MemoryHandle(null, pointer, handle);
                }
                else
                {
                    var array = (T[])_object;
                    var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                    void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref array.GetRawSzArrayData()), _index);
                    memoryHandle = new MemoryHandle(null, pointer, handle);
                }
            }
            else
            {
                if (_index < 0)
                {
                    ((OwnedMemory<T>)_object).Retain();
                    memoryHandle = new MemoryHandle((OwnedMemory<T>)_object);
                }
                else
                {
                    memoryHandle = new MemoryHandle(null);
                }
            }
            return memoryHandle;
        }

        /// <summary>
        /// Get an array segment from the underlying memory. 
        /// If unable to get the array segment, return false with a default array segment.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool DangerousTryGetArray(out ArraySegment<T> arraySegment)
        {
            if (_index < 0)
            {
                if (((OwnedMemory<T>)_object).TryGetArray(out var segment))
                {
                    arraySegment = new ArraySegment<T>(segment.Array, segment.Offset + (_index & RemoveOwnedFlagBitMask), _length);
                    return true;
                }
            }
            else
            {
                T[] arr = _object as T[];
                if (typeof(T) != typeof(char) || arr != null)
                {
                    arraySegment = new ArraySegment<T>(arr, _index, _length);
                    return true;
                }
            }

            arraySegment = default;
            return false;
        }

        /// <summary>
        /// Copies the contents from the memory into a new array.  This heap
        /// allocates, so should generally be avoided, however it is sometimes
        /// necessary to bridge the gap with APIs written in terms of arrays.
        /// </summary>
        public T[] ToArray() => Span.ToArray();

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if (obj is ReadOnlyMemory<T> readOnlyMemory)
            {
                return Equals(readOnlyMemory);
            }
            else if (obj is Memory<T> memory)
            {
                return Equals(memory);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the memory points to the same array and has the same length.  Note that
        /// this does *not* check to see if the *contents* are equal.
        /// </summary>
        public bool Equals(ReadOnlyMemory<T> other)
        {
            return
                _object == other._object &&
                _index == other._index &&
                _length == other._length;
        }

        /// <summary>Returns the hash code for this <see cref="ReadOnlyMemory{T}"/></summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return CombineHashCodes(_object.GetHashCode(), _index.GetHashCode(), _length.GetHashCode());
        }
        
        private static int CombineHashCodes(int left, int right)
        {
            return ((left << 5) + left) ^ right;
        }

        private static int CombineHashCodes(int h1, int h2, int h3)
        {
            return CombineHashCodes(CombineHashCodes(h1, h2), h3);
        }

        /// <summary>Gets the state of the memory as individual fields.</summary>
        /// <param name="start">The offset.</param>
        /// <param name="length">The count.</param>
        /// <returns>The object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal object GetObjectStartLength(out int start, out int length)
        {
            start = _index;
            length = _length;
            return _object;
        }
    }
}
