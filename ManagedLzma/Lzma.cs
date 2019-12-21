#pragma warning disable CA1034
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ManagedLzma {
	public static unsafe partial class Lzma {
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
			int Read(byte* buf, ref long size);
		}

		/* Returns: result - the number of actually written bytes.
           (result < size) means error */
		public interface ISeqOutStream {
			long Write(byte* buf, long size);
		}

		/* Returns: result. (result != SZ_OK) means break.
           Value (ulong)(long)-1 for size means unknown value. */
		public interface ICompressProgress {
			int Progress(ulong inSize, ulong outSize);
		}

		public static class SzAlloc {
			private static readonly Dictionary<uint, List<IntPtr>> _cache = new Dictionary<uint, List<IntPtr>>();
			private static readonly Dictionary<IntPtr, BufferInfo> _handles = new Dictionary<IntPtr, BufferInfo>();

			public static byte* AllocBytes(uint size) {
				lock (_cache) {
					List<IntPtr> cache;

					if (_cache.TryGetValue(size, out cache) && cache.Count > 0) {
						IntPtr p;

						p = cache[cache.Count - 1];
						cache.RemoveAt(cache.Count - 1);
						return (byte*)p;
					}
				}
				lock (_handles) {
					byte[] buffer;
					GCHandle gcHandle;
					IntPtr p;

					buffer = new byte[size];
					gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
					p = gcHandle.AddrOfPinnedObject();
					_handles.Add(p, new BufferInfo { Size = size, GCHandle = gcHandle });
					return (byte*)p;
				}
			}

			public static ushort* AllocUInt16(uint size) {
				return (ushort*)AllocBytes(size * 2);
			}

			public static uint* AllocUInt32(uint size) {
				return (uint*)AllocBytes(size * 4);
			}

			public static void Free(void* p) {
				if (p != null) {
					BufferInfo bufferInfo;

					bufferInfo = _handles[(IntPtr)p];
					lock (_cache) {
						List<IntPtr> cache;

						if (!_cache.TryGetValue(bufferInfo.Size, out cache)) {
							cache = new List<IntPtr>();
							_cache.Add(bufferInfo.Size, cache);
						}
						cache.Add((IntPtr)p);
					}
				}
			}

			public static void ClearCache() {
				_cache.Clear();
				foreach (KeyValuePair<IntPtr, BufferInfo> handle in _handles)
					handle.Value.GCHandle.Free();
				_handles.Clear();
			}

			private struct BufferInfo {
				public uint Size;
				public GCHandle GCHandle;
			}
		}

		#endregion

		#region Private Methods

		private delegate T Func<T>();

		private static void Memcpy(byte* dst, byte* src, uint size) {
			if ((uint)(dst - src) < size || (uint)(src - dst) < size)
				throw new InvalidOperationException("memcpy cannot handle overlapping regions correctly");

			Memcpy32(src, dst, (int)size);
		}

		private static void Memmove(byte* dst, byte* src, uint size) {
			if ((uint)(dst - src) >= size && (uint)(src - dst) >= size) {
				Memcpy(dst, src, size);
				return;
			}
			byte* d = dst;
			byte* s = src;
			if (d < s)
				while (size-- != 0)
					*d++ = *s++;
			else {
				byte* lasts = s + (size - 1);
				byte* lastd = d + (size - 1);
				while (size-- != 0)
					*lastd-- = *lasts--;
			}
		}

		// from Microsoft Reference Source
		private static void Memcpy32(byte* src, byte* dest, int len) {
			if (len >= 16) {
				do {
#if AMD64
					((long*)dest)[0] = ((long*)src)[0];
					((long*)dest)[1] = ((long*)src)[1];
#else
					((int*)dest)[0] = ((int*)src)[0];
					((int*)dest)[1] = ((int*)src)[1];
					((int*)dest)[2] = ((int*)src)[2];
					((int*)dest)[3] = ((int*)src)[3];
#endif
					dest += 16;
					src += 16;
				} while ((len -= 16) >= 16);
			}
			if (len > 0)  // protection against negative len and optimization for len==16*N
			{
				if ((len & 8) != 0) {
#if AMD64
					((long*)dest)[0] = ((long*)src)[0];
#else
					((int*)dest)[0] = ((int*)src)[0];
					((int*)dest)[1] = ((int*)src)[1];
#endif
					dest += 8;
					src += 8;
				}
				if ((len & 4) != 0) {
					((int*)dest)[0] = ((int*)src)[0];
					dest += 4;
					src += 4;
				}
				if ((len & 2) != 0) {
					((short*)dest)[0] = ((short*)src)[0];
					dest += 2;
					src += 2;
				}
				if ((len & 1) != 0)
					*dest++ = *src++;
			}
		}

		#endregion
	}
}
#pragma warning restore CA1034
