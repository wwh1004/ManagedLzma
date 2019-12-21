using System;
using System.Diagnostics;
using System.IO;

namespace ManagedLzma.Demo {
	internal static unsafe class Program {
		static void Main() {
			//byte[] source;
			//byte[] props;
			//Stopwatch stopwatch;
			//byte[] compressedData;
			//byte[] decompressedData;

			//source = File.ReadAllBytes(@"C:\Windows\System32\KernelBase.dll");
			//Console.WriteLine($"source size: {(float)source.Length / 1024} kb");
			//stopwatch = Stopwatch.StartNew();
			//compressedData = Compress(source, out props);
			//stopwatch.Stop();
			//Console.WriteLine($"compression time: {stopwatch.ElapsedMilliseconds} ms");
			//Console.WriteLine($"compressed size: {(float)compressedData.Length / 1024} kb");
			//File.WriteAllBytes(@"KernelBase.dll.lzma", compressedData);
			//decompressedData = Decompress(compressedData, props, source.Length);
			//Console.WriteLine($"data integrity check: {Compare(source, decompressedData) == 0}");
			for (int i = 1; i <= 10; i++) {
				byte[] source;
				byte[] props;
				Stopwatch stopwatch;
				byte[] compressedData;
				byte[] decompressedData;

				Console.WriteLine($"id: {i}");
				source = new byte[0x1000000];
				new Random().NextBytes(source);
				Console.WriteLine($"source size: {(float)source.Length / 1024} kb");
				stopwatch = Stopwatch.StartNew();
				compressedData = Compress(source, out props);
				stopwatch.Stop();
				Console.WriteLine($"compression time: {stopwatch.ElapsedMilliseconds} ms");
				Console.WriteLine($"compressed size: {(float)compressedData.Length / 1024} kb");
				decompressedData = Decompress(compressedData, props, source.Length);
				Console.WriteLine($"data integrity check: {Compare(source, decompressedData) == 0}");
				if (i % 2 == 0) {
					Lzma.SzAlloc.ClearCache();
					GC.Collect();
				}
			}
			Console.ReadKey(true);
		}

		private static byte[] Compress(byte[] data, out byte[] props) {
			byte[] dest = new byte[(int)(data.Length * 1.1)];
			long destLen = dest.Length;
			long srcLen = data.Length;
			props = new byte[Lzma.LZMA_PROPS_SIZE];
			long propsLen = props.Length;
			Lzma.LzmaCompress(
				ref dest[0], ref destLen,
				ref data[0], srcLen,
				ref props[0], ref propsLen,
				9, 128 * 1024 * 1024, -1, -1, -1, 273, 1);
			byte[] compressed = new byte[destLen];
			Buffer.BlockCopy(dest, 0, compressed, 0, compressed.Length);
			return compressed;
		}

		private static byte[] Decompress(byte[] compressed, byte[] props, int rawSize) {
			byte[] decompressed = new byte[rawSize];
			long destSize = decompressed.Length;
			long srcSize = compressed.Length;
			Lzma.LzmaUncompress(
				ref decompressed[0], ref destSize,
				ref compressed[0], ref srcSize,
				ref props[0], props.Length);
			return decompressed;
		}

		private static int Compare(byte[] left, byte[] right) {
			if (left is null)
				throw new ArgumentNullException(nameof(left));
			if (right is null)
				throw new ArgumentNullException(nameof(right));

			if (left.Length != right.Length)
				return left.Length - right.Length;
			for (int i = 0; i < left.Length; i++) {
				int difference;

				difference = left[i] - right[i];
				if (difference == 0)
					continue;
				else
					return difference;
			}
			return 0;
		}
	}
}
