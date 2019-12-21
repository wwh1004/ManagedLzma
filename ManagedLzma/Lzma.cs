#pragma warning disable CA1034
using System;
using System.Collections.Generic;

namespace ManagedLzma {
	public static partial class Lzma {
		#region Constants

		public const int LZMA_PROPS_SIZE = 5;

		public const int SZ_OK = 0;

		public const int SZ_ERROR_DATA = 1;
		public const int SZ_ERROR_MEM = 2;
		public const int SZ_ERROR_CRC = 3;
		public const int SZ_ERROR_UNSUPPORTED = 4;
		public const int SZ_ERROR_PARAM = 5;
		public const int SZ_ERROR_INPUT_EOF = 6;
		public const int SZ_ERROR_OUTPUT_EOF = 7;
		public const int SZ_ERROR_READ = 8;
		public const int SZ_ERROR_WRITE = 9;
		public const int SZ_ERROR_PROGRESS = 10;
		public const int SZ_ERROR_FAIL = 11;
		public const int SZ_ERROR_THREAD = 12;

		public const int SZ_ERROR_ARCHIVE = 16;
		public const int SZ_ERROR_NO_ARCHIVE = 17;

		#endregion

		#region Pointer

		private static class P {
			public static P<T> From<T>(T[] buffer, int offset) {
				return new P<T>(buffer, offset);
			}

			public static P<T> From<T>(T[] buffer, uint offset) {
				return new P<T>(buffer, offset);
			}
		}

		public struct P<T> {
			public static P<T> Null {
				get { return default; }
			}

			public readonly T[] mBuffer;
			public readonly int mOffset;

			public P(T[] buffer, int offset = 0) {
				mBuffer = buffer;
				mOffset = offset;
			}

			public P(T[] buffer, uint offset)
				: this(buffer, (int)offset) {
			}

			public bool IsNull {
				get { return mBuffer == null; }
			}

			public T this[int index] {
				get { return mBuffer[mOffset + index]; }
				set { mBuffer[mOffset + index] = value; }
			}

			public T this[uint index] {
				get { return this[(int)index]; }
				set { this[(int)index] = value; }
			}

			public T this[long index] {
				get { return this[checked((int)index)]; }
				set { this[checked((int)index)] = value; }
			}

			public static bool operator <(P<T> left, P<T> right) {
				return left.mOffset < right.mOffset;
			}

			public static bool operator <=(P<T> left, P<T> right) {
				return left.mOffset <= right.mOffset;
			}

			public static bool operator >(P<T> left, P<T> right) {
				return left.mOffset > right.mOffset;
			}

			public static bool operator >=(P<T> left, P<T> right) {
				return left.mOffset >= right.mOffset;
			}

			public static int operator -(P<T> left, P<T> right) {
				return left.mOffset - right.mOffset;
			}

			public static P<T> operator -(P<T> left, int right) {
				return new P<T>(left.mBuffer, left.mOffset - right);
			}

			public static P<T> operator +(P<T> left, int right) {
				return new P<T>(left.mBuffer, left.mOffset + right);
			}

			public static P<T> operator +(P<T> left, long right) {
				return new P<T>(left.mBuffer, checked((int)(left.mOffset + right)));
			}

			public static P<T> operator +(int left, P<T> right) {
				return new P<T>(right.mBuffer, left + right.mOffset);
			}

			public static P<T> operator -(P<T> left, uint right) {
				return left - (int)right;
			}

			public static P<T> operator +(P<T> left, uint right) {
				return left + (int)right;
			}

			public static P<T> operator +(uint left, P<T> right) {
				return (int)left + right;
			}

			public static P<T> operator ++(P<T> self) {
				return new P<T>(self.mBuffer, self.mOffset + 1);
			}

			public static P<T> operator --(P<T> self) {
				return new P<T>(self.mBuffer, self.mOffset - 1);
			}

			// This allows us to treat null as Pointer<T>.
			public static implicit operator P<T>(T[] buffer) {
				return new P<T>(buffer);
			}

			#region Identity

			public override int GetHashCode() {
				int hash = mOffset;
				if (mBuffer != null)
					hash += mBuffer.GetHashCode();
				return hash;
			}

			public override bool Equals(object obj) {
				if (obj == null)
					return mBuffer == null;

				// This will invoke the implicit conversion when obj is T[]
				var other = obj as P<T>?;
				return other.HasValue && this == other.Value;
			}

			public bool Equals(P<T> other) {
				return mBuffer == other.mBuffer
					&& mOffset == other.mOffset;
			}

			public static bool operator ==(P<T> left, P<T> right) {
				return left.mBuffer == right.mBuffer
					&& left.mOffset == right.mOffset;
			}

			public static bool operator !=(P<T> left, P<T> right) {
				return left.mBuffer != right.mBuffer
					|| left.mOffset != right.mOffset;
			}

			#endregion
		}

		#endregion

		#region Hash
		internal const int kHash2Size = 1 << 10;
		internal const int kHash3Size = 1 << 16;
		internal const int kHash4Size = 1 << 20;

		internal const int kFix3HashSize = kHash2Size;
		internal const int kFix4HashSize = kHash2Size + kHash3Size;
		internal const int kFix5HashSize = kHash2Size + kHash3Size + kHash4Size;
		#endregion

		#region Types

		//#define RINOK(x) { int __result__ = (x); if (__result__ != 0) return __result__; }

		/* The following interfaces use first parameter as pointer to structure */

		/* if (input(*size) != 0 && output(*size) == 0) means end_of_stream.
           (output(*size) < input(*size)) is allowed */
		public interface ISeqInStream {
			int Read(P<byte> buf, ref long size);
		}

		/* Returns: result - the number of actually written bytes.
           (result < size) means error */
		public interface ISeqOutStream {
			long Write(P<byte> buf, long size);
		}

		/* Returns: result. (result != SZ_OK) means break.
           Value (ulong)(long)-1 for size means unknown value. */
		public interface ICompressProgress {
			int Progress(ulong inSize, ulong outSize);
		}

		//public delegate object ISzAlloc_Alloc(object p, long size);
		//public delegate void ISzAlloc_Free(object p, object address); /* address can be null */
		public sealed class SzAlloc {
			public static readonly SzAlloc BigAlloc = new SzAlloc();
			public static readonly SzAlloc SmallAlloc = new SzAlloc();

			private static readonly Dictionary<long, List<byte[]>> Cache1 = new Dictionary<long, List<byte[]>>();
			private static readonly Dictionary<long, List<ushort[]>> Cache2 = new Dictionary<long, List<ushort[]>>();
			private static readonly Dictionary<long, List<uint[]>> Cache3 = new Dictionary<long, List<uint[]>>();

			private SzAlloc() {
			}

#pragma warning disable CA1822
#pragma warning disable IDE0060
			//public T AllocObject<T>(object p)
			//	where T : class, new() {
			//	return new T();
			//}

			public byte[] AllocBytes(object p, long size) {
				lock (Cache1) {
					List<byte[]> cache;
					if (Cache1.TryGetValue(size, out cache) && cache.Count > 0) {
						byte[] buffer = cache[cache.Count - 1];
						cache.RemoveAt(cache.Count - 1);
						return buffer;
					}
				}

				return new byte[size];
			}

			public ushort[] AllocUInt16(object p, long size) {
				lock (Cache2) {
					List<ushort[]> cache;
					if (Cache2.TryGetValue(size, out cache) && cache.Count > 0) {
						ushort[] buffer = cache[cache.Count - 1];
						cache.RemoveAt(cache.Count - 1);
						return buffer;
					}
				}

				return new ushort[size];
			}

			public uint[] AllocUInt32(object p, long size) {
				lock (Cache3) {
					List<uint[]> cache;
					if (Cache3.TryGetValue(size, out cache) && cache.Count > 0) {
						uint[] buffer = cache[cache.Count - 1];
						cache.RemoveAt(cache.Count - 1);
						return buffer;
					}
				}

				return new uint[size];
			}

			//public void FreeObject(object p, object address) {
			//	// ignore
			//}

			public void FreeBytes(object p, byte[] buffer) {
				if (buffer != null) {
					lock (Cache1) {
						List<byte[]> cache;
						if (!Cache1.TryGetValue(buffer.Length, out cache))
							Cache1.Add(buffer.Length, cache = new List<byte[]>());

						cache.Add(buffer);
					}
				}
			}

			public void FreeUInt16(object p, ushort[] buffer) {
				if (buffer != null) {
					lock (Cache2) {
						List<ushort[]> cache;
						if (!Cache2.TryGetValue(buffer.Length, out cache))
							Cache2.Add(buffer.Length, cache = new List<ushort[]>());

						cache.Add(buffer);
					}
				}
			}

			public void FreeUInt32(object p, uint[] buffer) {
				if (buffer != null) {
					lock (Cache3) {
						List<uint[]> cache;
						if (!Cache3.TryGetValue(buffer.Length, out cache))
							Cache3.Add(buffer.Length, cache = new List<uint[]>());

						cache.Add(buffer);
					}
				}
			}
		}
#pragma warning restore IDE0060
#pragma warning restore CA1822

		#endregion

		#region Private Methods

		private delegate T Func<T>();

		private static void Memcpy(P<byte> dst, P<byte> src, long size) {
			Memcpy(dst, src, checked((int)size));
		}

		private static void Memcpy(P<byte> dst, P<byte> src, int size) {
			if (dst.mBuffer == src.mBuffer && src.mOffset < dst.mOffset + size && dst.mOffset < src.mOffset + size)
				throw new InvalidOperationException("memcpy cannot handle overlapping regions correctly");

			Buffer.BlockCopy(src.mBuffer, src.mOffset, dst.mBuffer, dst.mOffset, size);
		}

		private static void Memmove(P<byte> dst, P<byte> src, uint size) {
			Buffer.BlockCopy(src.mBuffer, src.mOffset, dst.mBuffer, dst.mOffset, checked((int)size));
		}

		private static T[] NewArray<T>(int sz1, Func<T> creator) {
			T[] buffer = new T[sz1];
			for (int i = 0; i < sz1; i++)
				buffer[i] = creator();
			return buffer;
		}

		private static T[][] NewArray<T>(int sz1, int sz2) {
			T[][] buffer = new T[sz1][];
			for (int i = 0; i < buffer.Length; i++)
				buffer[i] = new T[sz2];
			return buffer;
		}

		#endregion
	}
}
#pragma warning restore CA1034
