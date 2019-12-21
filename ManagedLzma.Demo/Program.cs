using System;
using System.Diagnostics;
using System.IO;

namespace ManagedLzma.Demo {
	internal static class Program {
		static void Main() {
			byte[] source;
			byte[] props;
			Stopwatch stopwatch;
			byte[] compressedData;
			byte[] decompressedData;

			source = File.ReadAllBytes(@"C:\Windows\System32\KernelBase.dll");
			Console.WriteLine($"source size: {(float)source.Length / 1024} kb");
			stopwatch = Stopwatch.StartNew();
			compressedData = Compress(source, out props);
			stopwatch.Stop();
			Console.WriteLine($"compression time: {stopwatch.ElapsedMilliseconds} ms");
			Console.WriteLine($"compressed size: {(float)compressedData.Length / 1024} kb");
			File.WriteAllBytes(@"KernelBase.dll.lzma", compressedData);
			decompressedData = Decompress(compressedData, props, source.Length);
			Console.WriteLine($"data integrity check: {Compare(source, decompressedData) == 0}");
			Console.ReadKey(true);
		}

		private static byte[] Compress(byte[] data, out byte[] props) {
			byte[] buffer = new byte[(int)(data.Length * 1.1)];
			long destLen = buffer.Length;
			long srcLen = data.Length;
			props = new byte[Lzma.LZMA_PROPS_SIZE];
			long propsLen = props.Length;
			Lzma.LzmaCompress(
				buffer, ref destLen,
				data, srcLen,
				props, ref propsLen,
				9, 128 * 1024 * 1024, -1, -1, -1, 273, 1);
			byte[] compressedData = new byte[destLen];
			Buffer.BlockCopy(buffer, 0, compressedData, 0, compressedData.Length);
			return compressedData;
		}

		private static byte[] Decompress(byte[] compressedData, byte[] props, int rawSize) {
			byte[] decompressedData = new byte[rawSize];
			long destLen = decompressedData.Length;
			long srcLen = compressedData.Length;
			Lzma.LzmaUncompress(
				decompressedData, ref destLen,
				compressedData, ref srcLen, props, props.Length);
			return decompressedData;
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
