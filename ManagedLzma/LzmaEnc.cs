#pragma warning disable CA1034
using System;

namespace ManagedLzma {
	unsafe partial class Lzma {
		public sealed class CLzmaEncProps {
			#region Variables

			/// <summary>
			/// 0 &lt;= level &lt;= 9
			/// </summary>
			public int mLevel;

			/// <summary>
			/// (1 &lt;&lt; 12) &lt;= dictSize &lt;= (1 &lt;&lt; 27) for 32-bit version <para/>
			/// (1 &lt;&lt; 12) &lt;= dictSize &lt;= (1 &lt;&lt; 30) for 64-bit version <para/>
			/// default = (1 &lt;&lt; 24)
			/// </summary>
			public uint mDictSize;

			/// <summary>
			/// Estimated size of data that will be compressed. default = 0xFFFFFFFF.
			/// Encoder uses this value to reduce dictionary size
			/// </summary>
			public uint mReduceSize;

			/// <summary>
			/// 0 &lt;= lc &lt;= 8, default = 3
			/// </summary>
			public int mLC;

			/// <summary>
			/// 0 &lt;= lp &lt;= 4, default = 0
			/// </summary>
			public int mLP;

			/// <summary>
			/// 0 &lt;= pb &lt;= 4, default = 2
			/// </summary>
			public int mPB;

			/// <summary>
			/// 0 - fast, 1 - normal, default = 1
			/// </summary>
			public int mAlgo;

			/// <summary>
			/// 5 &lt;= fb &lt;= 273, default = 32
			/// </summary>
			public int mFB;

			/// <summary>
			/// 0 - hashChain Mode, 1 - binTree mode - normal, default = 1
			/// </summary>
			public int mBtMode;

			/// <summary>
			/// 2, 3 or 4, default = 4
			/// </summary>
			public int mNumHashBytes;

			/// <summary>
			/// 1 &lt;= mc &lt;= (1 &lt;&lt; 30), default = 32
			/// </summary>
			public uint mMC;

			/// <summary>
			/// 0 - do not write EOPM, 1 - write EOPM, default = 0
			/// </summary>
			public uint mWriteEndMark;

			/// <summary>
			/// 1 or 2, default = 2
			/// </summary>
			public int mNumThreads;

			#endregion

			#region Private Methods

			private CLzmaEncProps() {
				// was LzmaEncProps_Init

				mLevel = 5;
				mDictSize = 0;
				mMC = 0;
				mReduceSize = ~0u;
				mLC = -1;
				mLP = -1;
				mPB = -1;
				mAlgo = -1;
				mFB = -1;
				mBtMode = -1;
				mNumHashBytes = -1;
				mNumThreads = -1;
				mWriteEndMark = 0;
			}

			#endregion

			#region Public Methods

			public CLzmaEncProps(CLzmaEncProps other) {
				mLevel = other.mLevel;
				mDictSize = other.mDictSize;
				mReduceSize = other.mReduceSize;
				mLC = other.mLC;
				mLP = other.mLP;
				mPB = other.mPB;
				mAlgo = other.mAlgo;
				mFB = other.mFB;
				mBtMode = other.mBtMode;
				mNumHashBytes = other.mNumHashBytes;
				mMC = other.mMC;
				mWriteEndMark = other.mWriteEndMark;
				mNumThreads = other.mNumThreads;
			}

			public static CLzmaEncProps LzmaEncProps_Init() {
				return new CLzmaEncProps();
			}

			public void LzmaEncProps_Normalize() {
				int level = mLevel;
				if (level < 0)
					level = 5;
				mLevel = level;

				if (mDictSize == 0) {
					if (level <= 5)
						mDictSize = 1u << (level * 2 + 14);
					else if (level == 6)
						mDictSize = 1u << 25;
					else
						mDictSize = 1u << 26;
				}

				if (mDictSize > mReduceSize) {
					for (int i = 15; i <= 30; i++) {
						if (mReduceSize <= (2u << i)) {
							mDictSize = 2u << i;
							break;
						}
						if (mReduceSize <= (3u << i)) {
							mDictSize = 3u << i;
							break;
						}
					}
				}

				if (mLC < 0)
					mLC = 3;
				if (mLP < 0)
					mLP = 0;
				if (mPB < 0)
					mPB = 2;
				if (mAlgo < 0)
					mAlgo = level < 5 ? 0 : 1;
				if (mFB < 0)
					mFB = level < 7 ? 32 : 64;
				if (mBtMode < 0)
					mBtMode = mAlgo == 0 ? 0 : 1;
				if (mNumHashBytes < 0)
					mNumHashBytes = 4;
				if (mMC == 0)
					mMC = (16u + ((uint)mFB >> 1)) >> (mBtMode != 0 ? 0 : 1);
				if (mNumThreads < 0)
					mNumThreads = 1;
			}

			#endregion

			#region Internal Methods

			internal uint LzmaEncProps_GetDictSize() {
				CLzmaEncProps props = new CLzmaEncProps(this);
				props.LzmaEncProps_Normalize();
				return props.mDictSize;
			}

			#endregion
		}


		/* ---------- CLzmaEncHandle Interface ---------- */

		/* LzmaEnc_* functions can return the following exit codes:
        Returns:
          SZ_OK           - OK
          SZ_ERROR_MEM    - Memory allocation error
          SZ_ERROR_PARAM  - Incorrect paramater in props
          SZ_ERROR_WRITE  - Write callback error.
          SZ_ERROR_PROGRESS - some break from progress callback
          SZ_ERROR_THREAD - errors in multithreading functions (only for Mt version)
        */

		#region Internal Classes

		internal struct OptimumReps {
			// number of slots == LZMA_NUM_REPS
			public uint _0, _1, _2, _3;

			public uint this[uint index] {
				get {
					switch (index) {
					case 0: return _0;
					case 1: return _1;
					case 2: return _2;
					case 3: return _3;
					default: throw new InvalidOperationException();
					}
				}
				set {
					switch (index) {
					case 0: _0 = value; break;
					case 1: _1 = value; break;
					case 2: _2 = value; break;
					case 3: _3 = value; break;
					default: throw new InvalidOperationException();
					}
				}
			}
		}

		internal sealed class COptimal {
			internal uint mPrice;

			internal uint mState; // CState
			internal bool mPrev1IsChar;
			internal bool mPrev2;

			internal uint mPosPrev2;
			internal uint mBackPrev2;

			internal uint mPosPrev;
			internal uint mBackPrev;
			internal OptimumReps mBacks = new OptimumReps();

			internal void MakeAsChar() {
				mBackPrev = ~0u;
				mPrev1IsChar = false;
			}

			internal void MakeAsShortRep() {
				mBackPrev = 0;
				mPrev1IsChar = false;
			}

			internal bool IsShortRep() {
				return mBackPrev == 0;
			}
		}

		internal class CLenEnc {
			#region Variables

			public ushort mChoice;
			public ushort mChoice2;
			public ushort[] mLow = new ushort[CLzmaEnc.LZMA_NUM_PB_STATES_MAX << CLzmaEnc.kLenNumLowBits];
			public ushort[] mMid = new ushort[CLzmaEnc.LZMA_NUM_PB_STATES_MAX << CLzmaEnc.kLenNumMidBits];
			public ushort[] mHigh = new ushort[CLzmaEnc.kLenNumHighSymbols];

			#endregion

			public CLenEnc() { }
			public CLenEnc(CLenEnc other) {
				mChoice = other.mChoice;
				mChoice2 = other.mChoice2;
				for (int i = 0; i < mLow.Length; i++)
					mLow[i] = other.mLow[i];
				for (int i = 0; i < mMid.Length; i++)
					mMid[i] = other.mMid[i];
				for (int i = 0; i < mHigh.Length; i++)
					mHigh[i] = other.mHigh[i];
			}

			internal void LenEnc_Init() {
				mChoice = CLzmaEnc.kProbInitValue;
				mChoice2 = CLzmaEnc.kProbInitValue;
				for (uint i = 0; i < (CLzmaEnc.LZMA_NUM_PB_STATES_MAX << CLzmaEnc.kLenNumLowBits); i++)
					mLow[i] = CLzmaEnc.kProbInitValue;
				for (uint i = 0; i < (CLzmaEnc.LZMA_NUM_PB_STATES_MAX << CLzmaEnc.kLenNumMidBits); i++)
					mMid[i] = CLzmaEnc.kProbInitValue;
				for (uint i = 0; i < CLzmaEnc.kLenNumHighSymbols; i++)
					mHigh[i] = CLzmaEnc.kProbInitValue;
			}

			internal void LenEnc_Encode(CRangeEnc rc, uint symbol, uint posState) {
				if (symbol < CLzmaEnc.kLenNumLowSymbols) {
					rc.RangeEnc_EncodeBit(ref mChoice, 0);
					CLzmaEnc.RcTree_Encode(rc, P.From(mLow, posState << CLzmaEnc.kLenNumLowBits), CLzmaEnc.kLenNumLowBits, symbol);
				}
				else {
					rc.RangeEnc_EncodeBit(ref mChoice, 1);
					if (symbol < CLzmaEnc.kLenNumLowSymbols + CLzmaEnc.kLenNumMidSymbols) {
						rc.RangeEnc_EncodeBit(ref mChoice2, 0);
						CLzmaEnc.RcTree_Encode(rc, P.From(mMid, posState << CLzmaEnc.kLenNumMidBits), CLzmaEnc.kLenNumMidBits, symbol - CLzmaEnc.kLenNumLowSymbols);
					}
					else {
						rc.RangeEnc_EncodeBit(ref mChoice2, 1);
						CLzmaEnc.RcTree_Encode(rc, mHigh, CLzmaEnc.kLenNumHighBits, symbol - CLzmaEnc.kLenNumLowSymbols - CLzmaEnc.kLenNumMidSymbols);
					}
				}
			}

			internal void LenEnc_SetPrices(uint posState, uint numSymbols, P<uint> prices, P<uint> probPrices) {
				uint a0 = CLzmaEnc.GET_PRICE_0(probPrices, mChoice);
				uint a1 = CLzmaEnc.GET_PRICE_1(probPrices, mChoice);
				uint b0 = a1 + CLzmaEnc.GET_PRICE_0(probPrices, mChoice2);
				uint b1 = a1 + CLzmaEnc.GET_PRICE_1(probPrices, mChoice2);

				uint i = 0;
				for (; i < CLzmaEnc.kLenNumLowSymbols; i++) {
					if (i >= numSymbols)
						return;
					prices[i] = a0 + CLzmaEnc.RcTree_GetPrice(P.From(mLow, posState << CLzmaEnc.kLenNumLowBits), CLzmaEnc.kLenNumLowBits, i, probPrices);
				}
				for (; i < CLzmaEnc.kLenNumLowSymbols + CLzmaEnc.kLenNumMidSymbols; i++) {
					if (i >= numSymbols)
						return;
					prices[i] = b0 + CLzmaEnc.RcTree_GetPrice(P.From(mMid, posState << CLzmaEnc.kLenNumMidBits), CLzmaEnc.kLenNumMidBits, i - CLzmaEnc.kLenNumLowSymbols, probPrices);
				}
				for (; i < numSymbols; i++)
					prices[i] = b1 + CLzmaEnc.RcTree_GetPrice(mHigh, CLzmaEnc.kLenNumHighBits, i - (CLzmaEnc.kLenNumLowSymbols + CLzmaEnc.kLenNumMidSymbols), probPrices);
			}
		}

		internal class CLenPriceEnc : CLenEnc {
			internal uint[][] mPrices;
			internal uint mTableSize;
			internal uint[] mCounters;

			internal CLenPriceEnc() {
				mPrices = new uint[CLzmaEnc.LZMA_NUM_PB_STATES_MAX][];
				for (int i = 0; i < mPrices.Length; i++)
					mPrices[i] = new uint[CLzmaEnc.kLenNumSymbolsTotal];

				mCounters = new uint[CLzmaEnc.LZMA_NUM_PB_STATES_MAX];
			}

			internal CLenPriceEnc(CLenPriceEnc other)
				: base(other) {
				mPrices = new uint[CLzmaEnc.LZMA_NUM_PB_STATES_MAX][];
				for (int i = 0; i < mPrices.Length; i++) {
					mPrices[i] = new uint[CLzmaEnc.kLenNumSymbolsTotal];
					for (int j = 0; j < CLzmaEnc.kLenNumSymbolsTotal; j++)
						mPrices[i][j] = other.mPrices[i][j];
				}

				mTableSize = other.mTableSize;

				mCounters = new uint[CLzmaEnc.LZMA_NUM_PB_STATES_MAX];
				for (int i = 0; i < mCounters.Length; i++)
					mCounters[i] = other.mCounters[i];
			}

			private void LenPriceEnc_UpdateTable(uint posState, P<uint> probPrices) {
				LenEnc_SetPrices(posState, mTableSize, mPrices[posState], probPrices);
				mCounters[posState] = mTableSize;
			}

			internal void LenPriceEnc_UpdateTables(uint numPosStates, P<uint> probPrices) {
				for (uint posState = 0; posState < numPosStates; posState++)
					LenPriceEnc_UpdateTable(posState, probPrices);
			}

			internal void LenEnc_Encode2(CRangeEnc rc, uint symbol, uint posState, bool updatePrice, P<uint> probPrices) {
				LenEnc_Encode(rc, symbol, posState);

				if (updatePrice) {
					if (--mCounters[posState] == 0)
						LenPriceEnc_UpdateTable(posState, probPrices);
				}
			}
		}

		internal sealed class CRangeEnc {
			#region Constants

			private const int kBufferSize = 1 << 16;

			#endregion

			#region Variables

			public uint mRange;
			public byte mCache;
			public ulong mLow;
			public ulong mCacheSize;
			public P<byte> mBuf;
			public P<byte> mBufLim;
			public P<byte> mBufBase;
			public ISeqOutStream mOutStream;
			public ulong mProcessed;
			public int mRes;

			#endregion

			internal void RangeEnc_Construct() {
				mOutStream = null;
				mBufBase = null;
			}

			internal ulong RangeEnc_GetProcessed() {
				return mProcessed + (uint)(mBuf - mBufBase) + mCacheSize;
			}

			internal bool RangeEnc_Alloc(SzAlloc alloc) {
				if (mBufBase == null) {
					mBufBase = alloc.AllocBytes(alloc, kBufferSize);
					if (mBufBase == null)
						return false;

					mBufLim = mBufBase + kBufferSize;
				}

				return true;
			}

			internal void RangeEnc_Free(SzAlloc alloc) {
				alloc.FreeBytes(alloc, mBufBase.mBuffer);
				mBufBase = null;
			}

			internal void RangeEnc_Init() {
				/* Stream.Init(); */
				mLow = 0;
				mRange = 0xFFFFFFFF;
				mCacheSize = 1;
				mCache = 0;

				mBuf = mBufBase;

				mProcessed = 0;
				mRes = SZ_OK;
			}

			internal void RangeEnc_FlushStream() {
				if (mRes != SZ_OK)
					return;

				long num = mBuf - mBufBase;
				if (num != mOutStream.Write(mBufBase, num))
					mRes = SZ_ERROR_WRITE;

				mProcessed += (ulong)num;
				mBuf = mBufBase;
			}

			internal void RangeEnc_ShiftLow() {
				if ((uint)mLow < 0xFF000000 || (int)(mLow >> 32) != 0) {
					byte temp = mCache;
					do {
						P<byte> buf = mBuf;
						buf[0] = (byte)(temp + (byte)(mLow >> 32));
						buf++;
						mBuf = buf;
						if (buf == mBufLim)
							RangeEnc_FlushStream();
						temp = 0xFF;
					}
					while (--mCacheSize != 0);

					mCache = (byte)((uint)mLow >> 24);
				}

				mCacheSize++;
				mLow = (uint)mLow << 8;
			}

			internal void RangeEnc_FlushData() {
				for (int i = 0; i < 5; i++)
					RangeEnc_ShiftLow();
			}

			internal void RangeEnc_EncodeDirectBits(uint value, int numBits) {
				do {
					mRange >>= 1;
					mLow += mRange & (0 - ((value >> --numBits) & 1));
					if (mRange < CLzmaEnc.kTopValue) {
						mRange <<= 8;
						RangeEnc_ShiftLow();
					}
				}
				while (numBits != 0);
			}

			internal void RangeEnc_EncodeBit(P<ushort> prob, uint symbol) { RangeEnc_EncodeBit(ref prob.mBuffer[prob.mOffset], symbol); }
			internal void RangeEnc_EncodeBit(ref ushort prob, uint symbol) {
				uint temp = prob;

				uint newBound = (mRange >> CLzmaEnc.kNumBitModelTotalBits) * temp;
				if (symbol == 0) {
					mRange = newBound;
					temp += (CLzmaEnc.kBitModelTotal - temp) >> CLzmaEnc.kNumMoveBits;
				}
				else {
					mLow += newBound;
					mRange -= newBound;
					temp -= temp >> CLzmaEnc.kNumMoveBits;
				}

				prob = (ushort)temp;

				if (mRange < CLzmaEnc.kTopValue) {
					mRange <<= 8;
					RangeEnc_ShiftLow();
				}
			}
		}

		internal sealed class CSaveState {
			#region Variables

			public ushort[] mLitProbs;

			public ushort[][] mIsMatch = NewArray<ushort>(CLzmaEnc.kNumStates, CLzmaEnc.LZMA_NUM_PB_STATES_MAX);
			public ushort[] mIsRep = new ushort[CLzmaEnc.kNumStates];
			public ushort[] mIsRepG0 = new ushort[CLzmaEnc.kNumStates];
			public ushort[] mIsRepG1 = new ushort[CLzmaEnc.kNumStates];
			public ushort[] mIsRepG2 = new ushort[CLzmaEnc.kNumStates];
			public ushort[][] mIsRep0Long = NewArray<ushort>(CLzmaEnc.kNumStates, CLzmaEnc.LZMA_NUM_PB_STATES_MAX);

			public ushort[][] mPosSlotEncoder = NewArray<ushort>(CLzmaEnc.kNumLenToPosStates, 1 << CLzmaEnc.kNumPosSlotBits);
			public ushort[] mPosEncoders = new ushort[CLzmaEnc.kNumFullDistances - CLzmaEnc.kEndPosModelIndex];
			public ushort[] mPosAlignEncoder = new ushort[1 << CLzmaEnc.kNumAlignBits];

			public CLenPriceEnc mLenEnc = new CLenPriceEnc();
			public CLenPriceEnc mRepLenEnc = new CLenPriceEnc();

			public OptimumReps mReps = new OptimumReps();
			public uint mState;

			#endregion
		}

		internal sealed class CMatchFinder {
			#region Constants

			private static readonly uint[] crc = CreateCrcTable();

			internal const int kEmptyHashValue = 0;
			internal const uint kMaxValForNormalize = 0xFFFFFFFF;
			internal const uint kNormalizeStepMin = 1u << 10; // it must be power of 2
			internal const uint kNormalizeMask = ~(kNormalizeStepMin - 1u);
			internal const uint kMaxHistorySize = 3u << 30;

			internal const int kStartMaxLen = 3;

			#endregion

			#region Variables

			public P<byte> mBuffer;
			public uint mPos;
			public uint mPosLimit;
			public uint mStreamPos;
			public uint mLenLimit;

			public uint mCyclicBufferPos;
			public uint mCyclicBufferSize; // it must be = (historySize + 1)

			public uint mMatchMaxLen;
			public uint[] mHash;
			public P<uint> mSon;
			public uint mHashMask;
			public uint mCutValue;

			public P<byte> mBufferBase;
			public ISeqInStream mStream;
			public bool mStreamEndWasReached;

			public uint mBlockSize;
			public uint mKeepSizeBefore;
			public uint mKeepSizeAfter;

			public uint mNumHashBytes;
			public bool mDirectInput;
			public long mDirectInputRem;
			public bool mBtMode;
			public bool mBigHash;
			public uint mHistorySize;
			public uint mFixedHashSize;
			public uint mHashSizeSum;
			public uint mNumSons;
			public int mResult;

			#endregion

			internal CMatchFinder() {
				mBufferBase = null;
				mDirectInput = false;
				mHash = null;
				mCutValue = 32;
				mBtMode = true;
				mNumHashBytes = 4;
				mBigHash = false;
			}

			internal bool MatchFinder_NeedMove() {
				if (mDirectInput)
					return false;

				// if (p.streamEndWasReached) return false;
				return mBufferBase + mBlockSize - mBuffer <= mKeepSizeAfter;
			}

			internal P<byte> MatchFinder_GetPointerToCurrentPos() {
				return mBuffer;
			}

			internal void MatchFinder_MoveBlock() {
				// Note: source and destination memory regions may overlap!
				Memmove(mBufferBase, mBuffer - mKeepSizeBefore, mStreamPos - mPos + mKeepSizeBefore);
				mBuffer = mBufferBase + mKeepSizeBefore;
			}

			internal void MatchFinder_ReadIfRequired() {
				if (!mStreamEndWasReached && mKeepSizeAfter >= mStreamPos - mPos)
					MatchFinder_ReadBlock();
			}

			private void LzInWindow_Free(SzAlloc alloc) {
				if (!mDirectInput) {
					alloc.FreeBytes(alloc, mBufferBase.mBuffer);
					mBufferBase = null;
				}
			}

			// keepSizeBefore + keepSizeAfter + keepSizeReserv must be < 4G)
			private bool LzInWindow_Create(uint keepSizeReserv, SzAlloc alloc) {
				uint blockSize = mKeepSizeBefore + mKeepSizeAfter + keepSizeReserv;

				if (mDirectInput) {
					mBlockSize = blockSize;
					return true;
				}

				if (mBufferBase == null || mBlockSize != blockSize) {
					LzInWindow_Free(alloc);
					mBlockSize = blockSize;
					mBufferBase = alloc.AllocBytes(alloc, blockSize);
				}

				return mBufferBase != null;
			}

			internal byte MatchFinder_GetIndexByte(int index) {
				return mBuffer[index];
			}

			internal uint MatchFinder_GetNumAvailableBytes() {
				return mStreamPos - mPos;
			}

			internal void MatchFinder_ReduceOffsets(uint subValue) {
				mPosLimit -= subValue;
				mPos -= subValue;
				mStreamPos -= subValue;
			}

			private void MatchFinder_ReadBlock() {
				if (mStreamEndWasReached || mResult != SZ_OK)
					return;

				if (mDirectInput) {
					uint curSize = 0xFFFFFFFF - mStreamPos;
					if (curSize > mDirectInputRem)
						curSize = (uint)mDirectInputRem;

					mDirectInputRem -= curSize;
					mStreamPos += curSize;
					if (mDirectInputRem == 0)
						mStreamEndWasReached = true;

					return;
				}

				for (; ; )
				{
					P<byte> dest = mBuffer + (mStreamPos - mPos);
					long size = mBufferBase + mBlockSize - dest;
					if (size == 0)
						return;

					mResult = mStream.Read(dest, ref size);
					if (mResult != SZ_OK)
						return;

					if (size == 0) {
						mStreamEndWasReached = true;
						return;
					}

					mStreamPos += (uint)size;
					if (mStreamPos - mPos > mKeepSizeAfter)
						return;
				}
			}

			private void MatchFinder_CheckAndMoveAndRead() {
				if (MatchFinder_NeedMove())
					MatchFinder_MoveBlock();

				MatchFinder_ReadBlock();
			}

			private void MatchFinder_FreeThisClassMemory(SzAlloc alloc) {
				alloc.FreeUInt32(alloc, mHash);
				mHash = null;
			}

			internal void MatchFinder_Free(SzAlloc alloc) {
				MatchFinder_FreeThisClassMemory(alloc);
				LzInWindow_Free(alloc);
			}

			private static uint[] AllocRefs(uint num, SzAlloc alloc) {
				long sizeInBytes = (long)num * sizeof(uint);
				if (sizeInBytes / sizeof(uint) != num)
					return null;

				return alloc.AllocUInt32(alloc, num);
			}

			// Conditions:
			//     historySize <= 3 GB
			//     keepAddBufferBefore + matchMaxLen + keepAddBufferAfter < 511MB
			internal bool MatchFinder_Create(uint historySize, uint keepAddBufferBefore, uint matchMaxLen, uint keepAddBufferAfter, SzAlloc alloc) {
				if (historySize > kMaxHistorySize) {
					MatchFinder_Free(alloc);
					return false;
				}

				uint sizeReserv = historySize >> 1;
				if (historySize > ((uint)2 << 30))
					sizeReserv = historySize >> 2;
				sizeReserv += (keepAddBufferBefore + matchMaxLen + keepAddBufferAfter) / 2 + (1 << 19);

				mKeepSizeBefore = historySize + keepAddBufferBefore + 1;
				mKeepSizeAfter = matchMaxLen + keepAddBufferAfter;

				// we need one additional byte, since we use MoveBlock after pos++ and before dictionary using
				if (LzInWindow_Create(sizeReserv, alloc)) {
					uint newCyclicBufferSize = historySize + 1;
					uint hs;

					mMatchMaxLen = matchMaxLen;

					{
						mFixedHashSize = 0;

						if (mNumHashBytes == 2) {
							hs = (1 << 16) - 1;
						}
						else {
							hs = historySize - 1;
							hs |= hs >> 1;
							hs |= hs >> 2;
							hs |= hs >> 4;
							hs |= hs >> 8;
							hs >>= 1;
							hs |= 0xFFFF; // don't change it! It's required for Deflate
							if (hs > (1 << 24)) {
								if (mNumHashBytes == 3)
									hs = (1 << 24) - 1;
								else
									hs >>= 1;
							}
						}

						mHashMask = hs;
						hs++;

						if (mNumHashBytes > 2)
							mFixedHashSize += kHash2Size;
						if (mNumHashBytes > 3)
							mFixedHashSize += kHash3Size;
						if (mNumHashBytes > 4)
							mFixedHashSize += kHash4Size;

						hs += mFixedHashSize;
					}

					{
						uint prevSize = mHashSizeSum + mNumSons;

						mHistorySize = historySize;
						mHashSizeSum = hs;
						mCyclicBufferSize = newCyclicBufferSize;
						mNumSons = mBtMode ? newCyclicBufferSize * 2 : newCyclicBufferSize;

						uint newSize = mHashSizeSum + mNumSons;
						if (mHash != null && prevSize == newSize)
							return true;

						MatchFinder_FreeThisClassMemory(alloc);

						mHash = AllocRefs(newSize, alloc);
						if (mHash != null) {
							mSon = P.From(mHash, mHashSizeSum);
							return true;
						}
					}
				}

				MatchFinder_Free(alloc);
				return false;
			}

			private void MatchFinder_SetLimits() {
				uint limit = kMaxValForNormalize - mPos;
				uint limit2 = mCyclicBufferSize - mCyclicBufferPos;
				if (limit2 < limit)
					limit = limit2;
				limit2 = mStreamPos - mPos;
				if (limit2 <= mKeepSizeAfter) {
					if (limit2 > 0)
						limit2 = 1;
				}
				else {
					limit2 -= mKeepSizeAfter;
				}
				if (limit2 < limit)
					limit = limit2;
				uint lenLimit = mStreamPos - mPos;
				if (lenLimit > mMatchMaxLen)
					lenLimit = mMatchMaxLen;
				mLenLimit = lenLimit;
				mPosLimit = mPos + limit;
			}

			internal void MatchFinder_Init() {
				//for(uint i = 0; i < mHashSizeSum; i++)
				//    mHash[i] = kEmptyHashValue;
				Array.Clear(mHash, 0, (int)mHashSizeSum);

				mCyclicBufferPos = 0;
				mBuffer = mBufferBase;
				mPos = mCyclicBufferSize;
				mStreamPos = mCyclicBufferSize;
				mResult = SZ_OK;
				mStreamEndWasReached = false;
				MatchFinder_ReadBlock();
				MatchFinder_SetLimits();
			}

			private uint MatchFinder_GetSubValue() {
				return (mPos - mHistorySize - 1) & kNormalizeMask;
			}

			internal static void MatchFinder_Normalize3(uint subValue, P<uint> items, uint numItems) {
				for (uint i = 0; i < numItems; i++) {
					uint value = items[i];
					if (value <= subValue)
						value = kEmptyHashValue;
					else
						value -= subValue;
					items[i] = value;
				}
			}

			private void MatchFinder_Normalize() {
				uint subValue = MatchFinder_GetSubValue();
				MatchFinder_Normalize3(subValue, mHash, mHashSizeSum + mNumSons);
				MatchFinder_ReduceOffsets(subValue);
			}

			private void MatchFinder_CheckLimits() {
				if (mPos == kMaxValForNormalize)
					MatchFinder_Normalize();

				if (!mStreamEndWasReached && mKeepSizeAfter == mStreamPos - mPos)
					MatchFinder_CheckAndMoveAndRead();

				if (mCyclicBufferPos == mCyclicBufferSize)
					mCyclicBufferPos = 0;

				MatchFinder_SetLimits();
			}

			private static P<uint> Hc_GetMatchesSpec(uint lenLimit, uint curMatch, uint pos, P<byte> cur, P<uint> son, uint _cyclicBufferPos, uint _cyclicBufferSize, uint cutValue, P<uint> distances, uint maxLen) {
				son[_cyclicBufferPos] = curMatch;

				for (; ; )
				{
					uint delta = pos - curMatch;
					if (cutValue-- == 0 || delta >= _cyclicBufferSize)
						return distances;

					P<byte> pb = cur - delta;
					curMatch = son[_cyclicBufferPos - delta + ((delta > _cyclicBufferPos) ? _cyclicBufferSize : 0)];
					if (pb[maxLen] == cur[maxLen] && pb[0] == cur[0]) {
						uint len = 0;
						while (++len != lenLimit)
							if (pb[len] != cur[len])
								break;

						if (maxLen < len) {
							distances[0] = maxLen = len;
							distances++;
							distances[0] = delta - 1;
							distances++;
							if (len == lenLimit)
								return distances;
						}
					}
				}
			}

			internal static P<uint> GetMatchesSpec1(uint lenLimit, uint curMatch, uint pos, P<byte> cur, P<uint> son, uint _cyclicBufferPos, uint _cyclicBufferSize, uint cutValue, P<uint> distances, uint maxLen) {
				P<uint> ptr0 = son + (_cyclicBufferPos << 1) + 1;
				P<uint> ptr1 = son + (_cyclicBufferPos << 1);
				uint len0 = 0;
				uint len1 = 0;

				for (; ; )
				{
					uint delta = pos - curMatch;
					if (cutValue-- == 0 || delta >= _cyclicBufferSize) {
						ptr0[0] = ptr1[0] = kEmptyHashValue;
						return distances;
					}

					P<uint> pair = son + ((_cyclicBufferPos - delta + ((delta > _cyclicBufferPos) ? _cyclicBufferSize : 0)) << 1);
					P<byte> pb = cur - delta;
					uint len = len0 < len1 ? len0 : len1;

					if (pb[len] == cur[len]) {
						if (++len != lenLimit && pb[len] == cur[len])
							while (++len != lenLimit)
								if (pb[len] != cur[len])
									break;

						if (maxLen < len) {
							distances[0] = maxLen = len;
							distances++;
							distances[0] = delta - 1;
							distances++;

							if (len == lenLimit) {
								ptr1[0] = pair[0];
								ptr0[0] = pair[1];
								return distances;
							}
						}
					}

					if (pb[len] < cur[len]) {
						ptr1[0] = curMatch;
						ptr1 = pair + 1;
						curMatch = ptr1[0];
						len1 = len;
					}
					else {
						ptr0[0] = curMatch;
						ptr0 = pair;
						curMatch = ptr0[0];
						len0 = len;
					}
				}
			}

			private static void SkipMatchesSpec(uint lenLimit, uint curMatch, uint pos, P<byte> cur, P<uint> son, uint _cyclicBufferPos, uint _cyclicBufferSize, uint cutValue) {
				P<uint> ptr0 = son + (_cyclicBufferPos << 1) + 1;
				P<uint> ptr1 = son + (_cyclicBufferPos << 1);
				uint len0 = 0;
				uint len1 = 0;

				for (; ; )
				{
					uint delta = pos - curMatch;
					if (cutValue-- == 0 || delta >= _cyclicBufferSize) {
						ptr0[0] = ptr1[0] = kEmptyHashValue;
						return;
					}

					P<uint> pair = son + ((_cyclicBufferPos - delta + ((delta > _cyclicBufferPos) ? _cyclicBufferSize : 0)) << 1);
					P<byte> pb = cur - delta;
					uint len = len0 < len1 ? len0 : len1;

					if (pb[len] == cur[len]) {
						while (++len != lenLimit)
							if (pb[len] != cur[len])
								break;

						if (len == lenLimit) {
							ptr1[0] = pair[0];
							ptr0[0] = pair[1];
							return;
						}
					}

					if (pb[len] < cur[len]) {
						ptr1[0] = curMatch;
						ptr1 = pair + 1;
						curMatch = ptr1[0];
						len1 = len;
					}
					else {
						ptr0[0] = curMatch;
						ptr0 = pair;
						curMatch = ptr0[0];
						len0 = len;
					}
				}
			}

			private void MatchFinder_MovePos() {
				mCyclicBufferPos++;
				mBuffer++;
				mPos++;

				if (mPos == mPosLimit)
					MatchFinder_CheckLimits();
			}

			internal uint Bt2_MatchFinder_GetMatches(P<uint> distances) {
				uint lenLimit = mLenLimit;
				if (lenLimit < 2) {
					MatchFinder_MovePos();
					return 0;
				}

				P<byte> cur = mBuffer;
				uint hashValue = cur[0] | ((uint)cur[1] << 8);
				uint curMatch = mHash[hashValue];
				mHash[hashValue] = mPos;

				uint offset = 0;
				offset = (uint)(GetMatchesSpec1(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, 1) - distances);

				mCyclicBufferPos++;
				mBuffer++;

				if (++mPos == mPosLimit)
					MatchFinder_CheckLimits();

				return offset;
			}

			internal uint Bt3_MatchFinder_GetMatches(P<uint> distances) {
				uint lenLimit = mLenLimit;
				if (lenLimit < 3) {
					MatchFinder_MovePos();
					return 0;
				}

				P<byte> cur = mBuffer;

				uint temp = crc[cur[0]] ^ cur[1];
				uint hash2Value = temp & (kHash2Size - 1);
				uint hashValue = (temp ^ ((uint)cur[2] << 8)) & mHashMask;

				uint delta2 = mPos - mHash[hash2Value];
				uint curMatch = mHash[kFix3HashSize + hashValue];

				mHash[hash2Value] = mPos;
				mHash[kFix3HashSize + hashValue] = mPos;

				uint maxLen = 2;
				uint offset = 0;

				if (delta2 < mCyclicBufferSize && cur[-delta2] == cur[0]) {
					while (maxLen != lenLimit) {
						if (cur[maxLen - delta2] != cur[maxLen])
							break;

						maxLen++;
					}

					distances[0] = maxLen;
					distances[1] = delta2 - 1;
					offset = 2;

					if (maxLen == lenLimit) {
						SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

						mCyclicBufferPos++;
						mBuffer++;

						if (++mPos == mPosLimit)
							MatchFinder_CheckLimits();

						return offset;
					}
				}

				offset = (uint)(GetMatchesSpec1(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, maxLen) - distances);

				mCyclicBufferPos++;
				mBuffer++;

				if (++mPos == mPosLimit)
					MatchFinder_CheckLimits();

				return offset;
			}

			internal uint Bt4_MatchFinder_GetMatches(P<uint> distances) {
				uint lenLimit = mLenLimit;
				if (lenLimit < 4) {
					MatchFinder_MovePos();
					return 0;
				}

				P<byte> cur = mBuffer;

				uint temp = crc[cur[0]] ^ cur[1];
				uint hash2Value = temp & (kHash2Size - 1);
				uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
				uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (crc[cur[3]] << 5)) & mHashMask;

				uint delta2 = mPos - mHash[hash2Value];
				uint delta3 = mPos - mHash[kFix3HashSize + hash3Value];
				uint curMatch = mHash[kFix4HashSize + hashValue];

				mHash[hash2Value] = mPos;
				mHash[kFix3HashSize + hash3Value] = mPos;
				mHash[kFix4HashSize + hashValue] = mPos;

				uint maxLen = 1;
				uint offset = 0;

				if (delta2 < mCyclicBufferSize && cur[-delta2] == cur[0]) {
					distances[0] = maxLen = 2;
					distances[1] = delta2 - 1;
					offset = 2;
				}

				if (delta2 != delta3 && delta3 < mCyclicBufferSize && cur[-delta3] == cur[0]) {
					maxLen = 3;
					distances[offset + 1] = delta3 - 1;
					offset += 2;
					delta2 = delta3;
				}

				if (offset != 0) {
					while (maxLen != lenLimit && cur[maxLen - delta2] == cur[maxLen])
						maxLen++;

					distances[offset - 2] = maxLen;

					if (maxLen == lenLimit) {
						SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);
						mCyclicBufferPos++;
						mBuffer++;

						if (++mPos == mPosLimit)
							MatchFinder_CheckLimits();

						return offset;
					}
				}

				if (maxLen < 3)
					maxLen = 3;

				offset = (uint)(GetMatchesSpec1(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, maxLen) - distances);

				mCyclicBufferPos++;
				mBuffer++;

				if (++mPos == mPosLimit)
					MatchFinder_CheckLimits();

				return offset;
			}

			internal uint Hc4_MatchFinder_GetMatches(P<uint> distances) {
				uint lenLimit = mLenLimit;
				if (lenLimit < 4) {
					MatchFinder_MovePos();
					return 0;
				}

				P<byte> cur = mBuffer;

				uint temp = crc[cur[0]] ^ cur[1];
				uint hash2Value = temp & (kHash2Size - 1);
				uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
				uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (crc[cur[3]] << 5)) & mHashMask;

				uint delta2 = mPos - mHash[hash2Value];
				uint delta3 = mPos - mHash[kFix3HashSize + hash3Value];
				uint curMatch = mHash[kFix4HashSize + hashValue];

				mHash[hash2Value] = mPos;
				mHash[kFix3HashSize + hash3Value] = mPos;
				mHash[kFix4HashSize + hashValue] = mPos;

				uint maxLen = 1;
				uint offset = 0;

				if (delta2 < mCyclicBufferSize && cur[-delta2] == cur[0]) {
					distances[0] = maxLen = 2;
					distances[1] = delta2 - 1;
					offset = 2;
				}

				if (delta2 != delta3 && delta3 < mCyclicBufferSize && cur[-delta3] == cur[0]) {
					maxLen = 3;
					distances[offset + 1] = delta3 - 1;
					offset += 2;
					delta2 = delta3;
				}

				if (offset != 0) {
					while (maxLen != lenLimit && cur[maxLen - delta2] == cur[maxLen])
						maxLen++;

					distances[offset - 2] = maxLen;

					if (maxLen == lenLimit) {
						mSon[mCyclicBufferPos] = curMatch;
						mCyclicBufferPos++;
						mBuffer++;
						mPos++;
						if (mPos == mPosLimit)
							MatchFinder_CheckLimits();
						return offset;
					}
				}

				if (maxLen < 3)
					maxLen = 3;

				offset = (uint)(Hc_GetMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, maxLen) - distances);

				mCyclicBufferPos++;
				mBuffer++;
				mPos++;

				if (mPos == mPosLimit)
					MatchFinder_CheckLimits();

				return offset;
			}

			internal void Bt2_MatchFinder_Skip(uint num) {
				do {
					uint lenLimit = mLenLimit;
					if (lenLimit < 2) {
						MatchFinder_MovePos();
						continue;
					}

					P<byte> cur = mBuffer;
					uint hashValue = cur[0] | ((uint)cur[1] << 8);

					uint curMatch = mHash[hashValue];

					mHash[hashValue] = mPos;

					SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

					mCyclicBufferPos++;
					mBuffer++;

					if (++mPos == mPosLimit)
						MatchFinder_CheckLimits();
				}
				while (--num != 0);
			}

			internal void Bt3_MatchFinder_Skip(uint num) {
				do {
					uint lenLimit = mLenLimit;
					if (lenLimit < 3) {
						MatchFinder_MovePos();
						continue;
					}

					P<byte> cur = mBuffer;
					uint temp = crc[cur[0]] ^ cur[1];
					uint hash2Value = temp & (kHash2Size - 1);
					uint hashValue = (temp ^ ((uint)cur[2] << 8)) & mHashMask;

					uint curMatch = mHash[kFix3HashSize + hashValue];

					mHash[hash2Value] = mPos;
					mHash[kFix3HashSize + hashValue] = mPos;

					SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

					mCyclicBufferPos++;
					mBuffer++;

					if (++mPos == mPosLimit)
						MatchFinder_CheckLimits();
				}
				while (--num != 0);
			}

			internal void Bt4_MatchFinder_Skip(uint num) {
				do {
					uint lenLimit = mLenLimit;
					if (lenLimit < 4) {
						MatchFinder_MovePos();
						continue;
					}

					P<byte> cur = mBuffer;
					uint temp = crc[cur[0]] ^ cur[1];
					uint hash2Value = temp & (kHash2Size - 1);
					uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
					uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (crc[cur[3]] << 5)) & mHashMask;

					uint curMatch = mHash[kFix4HashSize + hashValue];

					mHash[hash2Value] = mPos;
					mHash[kFix3HashSize + hash3Value] = mPos;
					mHash[kFix4HashSize + hashValue] = mPos;

					SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

					mCyclicBufferPos++;
					mBuffer++;

					if (++mPos == mPosLimit)
						MatchFinder_CheckLimits();
				}
				while (--num != 0);
			}

			internal void Hc4_MatchFinder_Skip(uint num) {
				do {
					uint lenLimit = mLenLimit;
					if (lenLimit < 4) {
						MatchFinder_MovePos();
						continue;
					}

					P<byte> cur = mBuffer;
					uint temp = crc[cur[0]] ^ cur[1];
					uint hash2Value = temp & (kHash2Size - 1);
					uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
					uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (crc[cur[3]] << 5)) & mHashMask;

					uint curMatch = mHash[kFix4HashSize + hashValue];

					mHash[hash2Value] = mPos;
					mHash[kFix3HashSize + hash3Value] = mPos;
					mHash[kFix4HashSize + hashValue] = mPos;

					mSon[mCyclicBufferPos] = curMatch;

					mCyclicBufferPos++;
					mBuffer++;

					if (++mPos == mPosLimit)
						MatchFinder_CheckLimits();
				}
				while (--num != 0);
			}

			private static uint[] CreateCrcTable() {
				const uint kCrcPoly = 0xEDB88320;

				uint[] crc;

				crc = new uint[256];
				for (uint i = 0; i < 256; i++) {
					uint r = i;
					for (int j = 0; j < 8; j++)
						r = (r >> 1) ^ (kCrcPoly & ~((r & 1) - 1));

					crc[i] = r;
				}
				return crc;
			}
		}

		// Conditions:
		//    GetNumAvailableBytes must be called before each GetMatchLen.
		//    GetPointerToCurrentPos result must be used only before any other function

		internal interface IMatchFinder {
			void Init(object p);
			byte GetIndexByte(object p, int index);
			uint GetNumAvailableBytes(object p);
			P<byte> GetPointerToCurrentPos(object p);
			uint GetMatches(object p, P<uint> distances);
			void Skip(object p, uint num);
		}

		internal static void MatchFinder_CreateVTable(CMatchFinder p, out IMatchFinder vTable) {
			if (!p.mBtMode)
				vTable = new MatchFinderHc4();
			else if (p.mNumHashBytes == 2)
				vTable = new MatchFinderBt2();
			else if (p.mNumHashBytes == 3)
				vTable = new MatchFinderBt3();
			else
				vTable = new MatchFinderBt4();
		}

		private abstract class MatchFinderBase : IMatchFinder {
			public void Init(object p) {
				((CMatchFinder)p).MatchFinder_Init();
			}

			public byte GetIndexByte(object p, int index) {
				return ((CMatchFinder)p).MatchFinder_GetIndexByte(index);
			}

			public uint GetNumAvailableBytes(object p) {
				return ((CMatchFinder)p).MatchFinder_GetNumAvailableBytes();
			}

			public P<byte> GetPointerToCurrentPos(object p) {
				return ((CMatchFinder)p).MatchFinder_GetPointerToCurrentPos();
			}

			public abstract uint GetMatches(object p, P<uint> distances);
			public abstract void Skip(object p, uint num);
		}

		private sealed class MatchFinderHc4 : MatchFinderBase {
			public override uint GetMatches(object p, P<uint> distances) {
				return ((CMatchFinder)p).Hc4_MatchFinder_GetMatches(distances);
			}

			public override void Skip(object p, uint num) {
				((CMatchFinder)p).Hc4_MatchFinder_Skip(num);
			}
		}

		private sealed class MatchFinderBt2 : MatchFinderBase {
			public override uint GetMatches(object p, P<uint> distances) {
				return ((CMatchFinder)p).Bt2_MatchFinder_GetMatches(distances);
			}

			public override void Skip(object p, uint num) {
				((CMatchFinder)p).Bt2_MatchFinder_Skip(num);
			}
		}

		private sealed class MatchFinderBt3 : MatchFinderBase {
			public override uint GetMatches(object p, P<uint> distances) {
				return ((CMatchFinder)p).Bt3_MatchFinder_GetMatches(distances);
			}

			public override void Skip(object p, uint num) {
				((CMatchFinder)p).Bt3_MatchFinder_Skip(num);
			}
		}

		private sealed class MatchFinderBt4 : MatchFinderBase {
			public override uint GetMatches(object p, P<uint> distances) {
				return ((CMatchFinder)p).Bt4_MatchFinder_GetMatches(distances);
			}

			public override void Skip(object p, uint num) {
				((CMatchFinder)p).Bt4_MatchFinder_Skip(num);
			}
		}

		#endregion

		public sealed class CLzmaEnc {

			#region Constants

			private const int kNumTopBits = 24;
			internal const uint kTopValue = 1u << kNumTopBits;

			internal const int kNumBitModelTotalBits = 11;
			internal const int kBitModelTotal = 1 << kNumBitModelTotalBits;
			internal const int kNumMoveBits = 5;
			internal const int kProbInitValue = kBitModelTotal >> 1;

			private const int kNumMoveReducingBits = 4;
			private const int kNumBitPriceShiftBits = 4;
			private const int kNumLogBits = 9 + sizeof(long) / 2; // that was sizeof(size_t)
			private const int kDicLogSizeMaxCompress = (kNumLogBits - 1) * 2 + 7;

			internal void LzmaEnc_FastPosInit() {
				mFastPos[0] = 0;
				mFastPos[1] = 1;

				int i = 2;
				for (int slotFast = 2; slotFast < kNumLogBits * 2; slotFast++) {
					int k = 1 << ((slotFast >> 1) - 1);
					for (int j = 0; j < k; j++)
						mFastPos[i++] = (byte)slotFast;
				}
			}

			private uint BSR2_RET(uint pos) {
				//return (pos < (1 << (6 + kNumLogBits)))
				//    ? mFastPos[pos >> 6] + 12u
				//    : mFastPos[pos >> (6 + kNumLogBits - 1)] + (6 + (kNumLogBits - 1)) * 2u;

				uint i = 6 + ((kNumLogBits - 1) & (0 - (((1 << (kNumLogBits + 6)) - 1 - pos) >> 31)));
				return mFastPos[pos >> (int)i] + (i * 2);
			}

			private byte GetPosSlot1(uint pos) {
				return mFastPos[pos];
			}

			private uint GetPosSlot2(uint pos) {
				return BSR2_RET(pos);
			}

			private uint GetPosSlot(uint pos) {
				if (pos < kNumFullDistances)
					return mFastPos[pos];
				else
					return BSR2_RET(pos);
			}

			internal const int LZMA_NUM_REPS = 4;

			private const int kNumOpts = 1 << 12;

			internal const int kNumLenToPosStates = 4;
			internal const int kNumPosSlotBits = 6;
			private const int kDicLogSizeMax = 32;
			private const int kDistTableSizeMax = kDicLogSizeMax * 2;


			internal const int kNumAlignBits = 4;
			private const int kAlignTableSize = 1 << kNumAlignBits;
			private const int kAlignMask = kAlignTableSize - 1;

			private const int kStartPosModelIndex = 4;
			internal const int kEndPosModelIndex = 14;
			internal const int kNumFullDistances = 1 << (kEndPosModelIndex >> 1);

			private const int LZMA_PB_MAX = 4;
			private const int LZMA_LC_MAX = 8;
			private const int LZMA_LP_MAX = 4;

			internal const int LZMA_NUM_PB_STATES_MAX = 1 << LZMA_PB_MAX;


			internal const int kLenNumLowBits = 3;
			internal const int kLenNumLowSymbols = 1 << kLenNumLowBits;
			internal const int kLenNumMidBits = 3;
			internal const int kLenNumMidSymbols = 1 << kLenNumMidBits;
			internal const int kLenNumHighBits = 8;
			internal const int kLenNumHighSymbols = 1 << kLenNumHighBits;

			internal const int kLenNumSymbolsTotal = kLenNumLowSymbols + kLenNumMidSymbols + kLenNumHighSymbols;

			private const int LZMA_MATCH_LEN_MIN = 2;
			private const int LZMA_MATCH_LEN_MAX = LZMA_MATCH_LEN_MIN + kLenNumSymbolsTotal - 1;

			internal const int kNumStates = 12;

			private static readonly uint[] kLiteralNextStates = new uint[kNumStates] { 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 4, 5 };
			private static readonly uint[] kMatchNextStates = new uint[kNumStates] { 7, 7, 7, 7, 7, 7, 7, 10, 10, 10, 10, 10 };
			private static readonly uint[] kRepNextStates = new uint[kNumStates] { 8, 8, 8, 8, 8, 8, 8, 11, 11, 11, 11, 11 };
			private static readonly uint[] kShortRepNextStates = new uint[kNumStates] { 9, 9, 9, 9, 9, 9, 9, 11, 11, 11, 11, 11 };

			private static bool IsCharState(uint s) {
				return s < 7;
			}

			private static uint GetLenToPosState(uint len) {
				return (len < kNumLenToPosStates + 1) ? len - 2 : kNumLenToPosStates - 1;
			}

			private const int kInfinityPrice = 1 << 30;

			private static void LitEnc_Encode(CRangeEnc p, P<ushort> probs, uint symbol) {
				symbol |= 0x100;
				do {
					p.RangeEnc_EncodeBit(probs + (symbol >> 8), (symbol >> 7) & 1);
					symbol <<= 1;
				}
				while (symbol < 0x10000);
			}

			private static void LitEnc_EncodeMatched(CRangeEnc p, P<ushort> probs, uint symbol, uint matchByte) {
				uint offs = 0x100;
				symbol |= 0x100;
				do {
					matchByte <<= 1;
					p.RangeEnc_EncodeBit(probs + (offs + (matchByte & offs) + (symbol >> 8)), (symbol >> 7) & 1);
					symbol <<= 1;
					offs &= ~(matchByte ^ symbol);
				}
				while (symbol < 0x10000);
			}

			internal static void LzmaEnc_InitPriceTables(P<uint> probPrices) {
				for (uint i = (1 << kNumMoveReducingBits) / 2; i < kBitModelTotal; i += 1 << kNumMoveReducingBits) {
					const int kCyclesBits = kNumBitPriceShiftBits;

					uint w = i;
					uint bitCount = 0;
					for (int j = 0; j < kCyclesBits; j++) {
						w *= w;
						bitCount <<= 1;
						while (w >= ((uint)1 << 16)) {
							w >>= 1;
							bitCount++;
						}
					}

					probPrices[i >> kNumMoveReducingBits] = (kNumBitModelTotalBits << kCyclesBits) - 15 - bitCount;
				}
			}

			internal static uint GET_PRICE(P<uint> probPrices, ushort prob, uint symbol) {
				//return symbol == 0
				//    ? GET_PRICE_0(probPrices, prob)
				//    : GET_PRICE_1(probPrices, prob);

				return probPrices[(prob ^ ((-(int)symbol) & (kBitModelTotal - 1))) >> kNumMoveReducingBits];
			}

			internal static uint GET_PRICE_0(P<uint> probPrices, ushort prob) {
				return probPrices[prob >> kNumMoveReducingBits];
			}

			internal static uint GET_PRICE_1(P<uint> probPrices, ushort prob) {
				return probPrices[(prob ^ (kBitModelTotal - 1)) >> kNumMoveReducingBits];
			}

			private static uint LitEnc_GetPrice(P<ushort> probs, uint symbol, P<uint> probPrices) {
				uint price = 0;
				symbol |= 0x100;
				do {
					price += GET_PRICE(probPrices, probs[symbol >> 8], (symbol >> 7) & 1);
					symbol <<= 1;
				}
				while (symbol < 0x10000);
				return price;
			}

			private static uint LitEnc_GetPriceMatched(P<ushort> probs, uint symbol, uint matchByte, P<uint> probPrices) {
				uint price = 0;
				uint offs = 0x100;
				symbol |= 0x100;
				do {
					matchByte <<= 1;
					price += GET_PRICE(probPrices, probs[offs + (matchByte & offs) + (symbol >> 8)], (symbol >> 7) & 1);
					symbol <<= 1;
					offs &= ~(matchByte ^ symbol);
				}
				while (symbol < 0x10000);
				return price;
			}

			internal static void RcTree_Encode(CRangeEnc rc, P<ushort> probs, int numBitLevels, uint symbol) {
				uint m = 1;
				for (int i = numBitLevels; i != 0;) {
					i--;
					uint bit = (symbol >> i) & 1;
					rc.RangeEnc_EncodeBit(probs + m, bit);
					m = (m << 1) | bit;
				}
			}

			private static void RcTree_ReverseEncode(CRangeEnc rc, P<ushort> probs, int numBitLevels, uint symbol) {
				uint m = 1;
				for (int i = 0; i < numBitLevels; i++) {
					uint bit = symbol & 1;
					rc.RangeEnc_EncodeBit(probs + m, bit);
					m = (m << 1) | bit;
					symbol >>= 1;
				}
			}

			internal static uint RcTree_GetPrice(P<ushort> probs, int numBitLevels, uint symbol, P<uint> ProbPrices) {
				uint price = 0;
				symbol |= 1u << numBitLevels;
				while (symbol != 1) {
					price += GET_PRICE(ProbPrices, probs[symbol >> 1], symbol & 1);
					symbol >>= 1;
				}
				return price;
			}

			private static uint RcTree_ReverseGetPrice(P<ushort> probs, int numBitLevels, uint symbol, P<uint> probPrices) {
				uint price = 0;
				uint m = 1;
				for (int i = numBitLevels; i != 0; i--) {
					uint bit = symbol & 1;
					symbol >>= 1;
					price += GET_PRICE(probPrices, probs[m], bit);
					m = (m << 1) | bit;
				}
				return price;
			}

			private P<ushort> LIT_PROBS(uint pos, byte prevByte) {
				return P.From(mLitProbs, (((pos & mLpMask) << mLC) + ((uint)prevByte >> (8 - mLC))) * 0x300);
			}

			private static bool ChangePair(uint smallDist, uint bigDist) {
				return (bigDist >> 7) > smallDist;
			}

			private sealed class CSeqOutStreamBuf : ISeqOutStream {
				public P<byte> mData;
				public long mRem;
				public bool mOverflow;

				public long Write(P<byte> data, long size) {
					if (mRem < size) {
						size = mRem;
						mOverflow = true;
					}

					Lzma.Memcpy(mData, data, size);

					mRem -= size;
					mData += size;
					return size;
				}
			}

			#endregion

			#region Variables

			internal IMatchFinder mMatchFinder;
			internal object mMatchFinderObj;

			internal CMatchFinder mMatchFinderBase;

			internal uint mOptimumEndIndex;
			internal uint mOptimumCurrentIndex;

			internal uint mLongestMatchLength;
			internal uint mNumPairs;
			internal uint mNumAvail;
			internal COptimal[] mOpt = NewArray(kNumOpts, () => new COptimal());

			internal byte[] mFastPos = new byte[1 << kNumLogBits];

			internal uint[] mProbPrices = new uint[kBitModelTotal >> kNumMoveReducingBits];
			internal uint[] mMatches = new uint[LZMA_MATCH_LEN_MAX * 2 + 2 + 1];
			internal uint mNumFastBytes;
			internal uint mAdditionalOffset;
			internal OptimumReps mReps = new OptimumReps();
			internal uint mState;

			internal uint[][] mPosSlotPrices = NewArray<uint>(kNumLenToPosStates, kDistTableSizeMax);
			internal uint[][] mDistancesPrices = NewArray<uint>(kNumLenToPosStates, kNumFullDistances);
			internal uint[] mAlignPrices = new uint[kAlignTableSize];
			internal uint mAlignPriceCount;

			internal uint mDistTableSize;

			internal int mLC;
			internal int mLP;
			internal int mPB;
			internal uint mLpMask;
			internal uint mPbMask;

			internal ushort[] mLitProbs;

			internal ushort[][] mIsMatch = NewArray<ushort>(kNumStates, LZMA_NUM_PB_STATES_MAX);
			internal ushort[] mIsRep = new ushort[kNumStates];
			internal ushort[] mIsRepG0 = new ushort[kNumStates];
			internal ushort[] mIsRepG1 = new ushort[kNumStates];
			internal ushort[] mIsRepG2 = new ushort[kNumStates];
			internal ushort[][] mIsRep0Long = NewArray<ushort>(kNumStates, LZMA_NUM_PB_STATES_MAX);

			internal ushort[][] mPosSlotEncoder = NewArray<ushort>(kNumLenToPosStates, 1 << kNumPosSlotBits);
			internal ushort[] mPosEncoders = new ushort[kNumFullDistances - kEndPosModelIndex];
			internal ushort[] mPosAlignEncoder = new ushort[1 << kNumAlignBits];

			internal CLenPriceEnc mLenEnc = new CLenPriceEnc();
			internal CLenPriceEnc mRepLenEnc = new CLenPriceEnc();

			internal int mLcLp;

			internal bool mFastMode;

			internal CRangeEnc mRC = new CRangeEnc();

			internal bool mWriteEndMark;
			internal ulong mNowPos64;
			internal uint mMatchPriceCount;
			internal bool mFinished;

			internal int mResult;
			internal uint mDictSize;

			internal bool mNeedInit;

			internal CSaveState mSaveState = new CSaveState();

			#endregion

			#region Public Methods

			public static CLzmaEnc LzmaEnc_Create() {
				return new CLzmaEnc();
			}

			public void LzmaEnc_Destroy(SzAlloc alloc, SzAlloc allocBig) {
				mMatchFinderBase.MatchFinder_Free(allocBig);
				LzmaEnc_FreeLits(alloc);
				mRC.RangeEnc_Free(alloc);
			}

			public int LzmaEnc_SetProps(CLzmaEncProps props2) {
				CLzmaEncProps props = new CLzmaEncProps(props2);
				props.LzmaEncProps_Normalize();

				if (props.mLC > LZMA_LC_MAX
					|| props.mLP > LZMA_LP_MAX
					|| props.mPB > LZMA_PB_MAX
					|| props.mDictSize > (1u << kDicLogSizeMaxCompress)
					|| props.mDictSize > (1u << 30))
					return SZ_ERROR_PARAM;

				mDictSize = props.mDictSize;

				uint fb = (uint)props.mFB;
				if (fb < 5)
					fb = 5;
				if (fb > LZMA_MATCH_LEN_MAX)
					fb = LZMA_MATCH_LEN_MAX;
				mNumFastBytes = fb;

				mLC = props.mLC;
				mLP = props.mLP;
				mPB = props.mPB;
				mFastMode = props.mAlgo == 0;
				mMatchFinderBase.mBtMode = props.mBtMode != 0;

				uint numHashBytes = 4;
				if (props.mBtMode != 0) {
					if (props.mNumHashBytes < 2)
						numHashBytes = 2;
					else if (props.mNumHashBytes < 4)
						numHashBytes = (uint)props.mNumHashBytes;
				}
				mMatchFinderBase.mNumHashBytes = numHashBytes;

				mMatchFinderBase.mCutValue = props.mMC;

				mWriteEndMark = props.mWriteEndMark != 0;

				return SZ_OK;
			}

			public int LzmaEnc_WriteProperties(P<byte> props, ref long size) {
				uint dictSize = mDictSize;
				if (size < LZMA_PROPS_SIZE)
					return SZ_ERROR_PARAM;
				size = LZMA_PROPS_SIZE;
				props[0] = (byte)((mPB * 5 + mLP) * 9 + mLC);

				for (int i = 11; i <= 30; i++) {
					if (dictSize <= (2u << i)) {
						dictSize = 2u << i;
						break;
					}
					if (dictSize <= (3u << i)) {
						dictSize = 3u << i;
						break;
					}
				}

				for (int i = 0; i < 4; i++)
					props[1 + i] = (byte)(dictSize >> (8 * i));

				return SZ_OK;
			}

			public int LzmaEnc_Encode(ISeqOutStream outStream, ISeqInStream inStream, ICompressProgress progress, SzAlloc alloc, SzAlloc allocBig) {
				int res;
				if ((res = LzmaEnc_Prepare(outStream, inStream, alloc, allocBig)) != SZ_OK)
					return res;

				return LzmaEnc_Encode2(progress);
			}

			public int LzmaEnc_MemEncode(P<byte> dest, ref long destLen, P<byte> src, long srcLen, bool writeEndMark, ICompressProgress progress, SzAlloc alloc, SzAlloc allocBig) {
				CSeqOutStreamBuf outStream = new CSeqOutStreamBuf();

				LzmaEnc_SetInputBuf(src, srcLen);

				outStream.mData = dest;
				outStream.mRem = destLen;
				outStream.mOverflow = false;

				mWriteEndMark = writeEndMark;

				mRC.mOutStream = outStream;

				int res = LzmaEnc_MemPrepare(src, srcLen, 0, alloc, allocBig);
				if (res == SZ_OK)
					res = LzmaEnc_Encode2(progress);

				destLen -= outStream.mRem;
				if (outStream.mOverflow)
					return SZ_ERROR_OUTPUT_EOF;

				return res;
			}

			#endregion

			#region Private Methods

			private static void Memcpy(ushort[] dst, ushort[] src, int size) {
				if (dst.Length != src.Length || size != src.Length * 2)
					throw new InvalidOperationException();

				Buffer.BlockCopy(src, 0, dst, 0, size);
			}

			internal void LzmaEnc_SaveState() {
				mSaveState.mLenEnc = new CLenPriceEnc(mLenEnc);
				mSaveState.mRepLenEnc = new CLenPriceEnc(mRepLenEnc);
				mSaveState.mState = mState;

				for (int i = 0; i < kNumStates; i++) {
					Memcpy(mSaveState.mIsMatch[i], mIsMatch[i], LZMA_NUM_PB_STATES_MAX * 2);
					Memcpy(mSaveState.mIsRep0Long[i], mIsRep0Long[i], LZMA_NUM_PB_STATES_MAX * 2);
				}

				for (int i = 0; i < kNumLenToPosStates; i++)
					Memcpy(mSaveState.mPosSlotEncoder[i], mPosSlotEncoder[i], (1 << kNumPosSlotBits) * 2);

				Memcpy(mSaveState.mIsRep, mIsRep, kNumStates * 2);
				Memcpy(mSaveState.mIsRepG0, mIsRepG0, kNumStates * 2);
				Memcpy(mSaveState.mIsRepG1, mIsRepG1, kNumStates * 2);
				Memcpy(mSaveState.mIsRepG2, mIsRepG2, kNumStates * 2);
				Memcpy(mSaveState.mPosEncoders, mPosEncoders, (kNumFullDistances - kEndPosModelIndex) * 2);
				Memcpy(mSaveState.mPosAlignEncoder, mPosAlignEncoder, (1 << kNumAlignBits) * 2);
				mSaveState.mReps = mReps;
				Memcpy(mSaveState.mLitProbs, mLitProbs, (0x300 << mLcLp) * 2);
			}

			internal void LzmaEnc_RestoreState() {
				mLenEnc = new CLenPriceEnc(mSaveState.mLenEnc);
				mRepLenEnc = new CLenPriceEnc(mSaveState.mRepLenEnc);
				mState = mSaveState.mState;

				for (int i = 0; i < kNumStates; i++) {
					Memcpy(mIsMatch[i], mSaveState.mIsMatch[i], LZMA_NUM_PB_STATES_MAX * 2);
					Memcpy(mIsRep0Long[i], mSaveState.mIsRep0Long[i], LZMA_NUM_PB_STATES_MAX * 2);
				}

				for (int i = 0; i < kNumLenToPosStates; i++)
					Memcpy(mPosSlotEncoder[i], mSaveState.mPosSlotEncoder[i], (1 << kNumPosSlotBits) * 2);

				Memcpy(mIsRep, mSaveState.mIsRep, kNumStates * 2);
				Memcpy(mIsRepG0, mSaveState.mIsRepG0, kNumStates * 2);
				Memcpy(mIsRepG1, mSaveState.mIsRepG1, kNumStates * 2);
				Memcpy(mIsRepG2, mSaveState.mIsRepG2, kNumStates * 2);
				Memcpy(mPosEncoders, mSaveState.mPosEncoders, (kNumFullDistances - kEndPosModelIndex) * 2);
				Memcpy(mPosAlignEncoder, mSaveState.mPosAlignEncoder, (1 << kNumAlignBits) * 2);
				mReps = mSaveState.mReps;
				Memcpy(mLitProbs, mSaveState.mLitProbs, (0x300 << mLcLp) * 2);
			}

			private uint GetOptimum(uint position, out uint backRes) {
				OptimumReps reps = new OptimumReps();
				P<uint> matches;
				uint numAvail;
				uint lenEnd;

				{
					if (mOptimumEndIndex != mOptimumCurrentIndex) {
						COptimal opt = mOpt[mOptimumCurrentIndex];
						uint lenRes = opt.mPosPrev - mOptimumCurrentIndex;
						backRes = opt.mBackPrev;
						mOptimumCurrentIndex = opt.mPosPrev;
						return lenRes;
					}

					mOptimumCurrentIndex = 0;
					mOptimumEndIndex = 0;

					uint mainLen, numPairs;
					if (mAdditionalOffset == 0) {
						mainLen = ReadMatchDistances(out numPairs);
					}
					else {
						mainLen = mLongestMatchLength;
						numPairs = mNumPairs;
					}

					numAvail = mNumAvail;
					if (numAvail < 2) {
						backRes = ~0u;
						return 1;
					}
					if (numAvail > LZMA_MATCH_LEN_MAX)
						numAvail = LZMA_MATCH_LEN_MAX;

					P<byte> data = mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - 1;
					OptimumReps repLens = new OptimumReps();
					uint repMaxIndex = 0;
					for (uint i = 0; i < LZMA_NUM_REPS; i++) {
						reps[i] = mReps[i];
						P<byte> data2 = data - (reps[i] + 1);
						if (data[0] != data2[0] || data[1] != data2[1]) {
							repLens[i] = 0;
							continue;
						}

						uint lenTest = 2;
						while (lenTest < numAvail && data[lenTest] == data2[lenTest])
							lenTest++;

						repLens[i] = lenTest;
						if (lenTest > repLens[repMaxIndex])
							repMaxIndex = i;
					}

					if (repLens[repMaxIndex] >= mNumFastBytes) {
						uint lenRes;
						backRes = repMaxIndex;
						lenRes = repLens[repMaxIndex];
						MovePos(lenRes - 1);
						return lenRes;
					}

					matches = mMatches;
					if (mainLen >= mNumFastBytes) {
						backRes = matches[numPairs - 1] + LZMA_NUM_REPS;
						MovePos(mainLen - 1);
						return mainLen;
					}

					byte curByte = data[0];
					byte matchByte = (data - (reps._0 + 1))[0];

					if (mainLen < 2 && curByte != matchByte && repLens[repMaxIndex] < 2) {
						backRes = ~0u;
						return 1;
					}

					mOpt[0].mState = mState;

					uint posState = position & mPbMask;

					{
						P<ushort> probs = LIT_PROBS(position, (data - 1)[0]);
						mOpt[1].mPrice = GET_PRICE_0(mIsMatch[mState][posState]) +
							(!IsCharState(mState) ?
							  LitEnc_GetPriceMatched(probs, curByte, matchByte, mProbPrices) :
							  LitEnc_GetPrice(probs, curByte, mProbPrices));
					}

					mOpt[1].MakeAsChar();

					uint matchPrice = GET_PRICE_1(mIsMatch[mState][posState]);
					uint repMatchPrice = matchPrice + GET_PRICE_1(mIsRep[mState]);

					if (matchByte == curByte) {
						uint shortRepPrice = repMatchPrice + GetRepLen1Price(mState, posState);
						if (shortRepPrice < mOpt[1].mPrice) {
							mOpt[1].mPrice = shortRepPrice;
							mOpt[1].MakeAsShortRep();
						}
					}
					lenEnd = (mainLen >= repLens[repMaxIndex]) ? mainLen : repLens[repMaxIndex];

					if (lenEnd < 2) {
						backRes = mOpt[1].mBackPrev;
						return 1;
					}

					mOpt[1].mPosPrev = 0;
					mOpt[0].mBacks = reps;

					uint len = lenEnd;
					do { mOpt[len--].mPrice = kInfinityPrice; }
					while (len >= 2);

					for (uint i = 0; i < LZMA_NUM_REPS; i++) {
						uint repLen = repLens[i];
						if (repLen < 2)
							continue;
						uint price = repMatchPrice + GetPureRepPrice(i, mState, posState);
						do {
							uint curAndLenPrice = price + mRepLenEnc.mPrices[posState][repLen - 2];
							COptimal opt = mOpt[repLen];
							if (curAndLenPrice < opt.mPrice) {
								opt.mPrice = curAndLenPrice;
								opt.mPosPrev = 0;
								opt.mBackPrev = i;
								opt.mPrev1IsChar = false;
							}
						}
						while (--repLen >= 2);
					}

					uint normalMatchPrice = matchPrice + GET_PRICE_0(mIsRep[mState]);

					len = (repLens._0 >= 2) ? repLens._0 + 1 : 2;
					if (len <= mainLen) {
						uint offs = 0;
						while (len > matches[offs])
							offs += 2;
						for (; ; len++) {
							uint distance = matches[offs + 1];

							uint curAndLenPrice = normalMatchPrice + mLenEnc.mPrices[posState][len - LZMA_MATCH_LEN_MIN];
							uint lenToPosState = GetLenToPosState(len);
							if (distance < kNumFullDistances) {
								curAndLenPrice += mDistancesPrices[lenToPosState][distance];
							}
							else {
								uint slot = GetPosSlot2(distance);
								curAndLenPrice += mAlignPrices[distance & kAlignMask] + mPosSlotPrices[lenToPosState][slot];
							}

							COptimal opt = mOpt[len];
							if (curAndLenPrice < opt.mPrice) {
								opt.mPrice = curAndLenPrice;
								opt.mPosPrev = 0;
								opt.mBackPrev = distance + LZMA_NUM_REPS;
								opt.mPrev1IsChar = false;
							}
							if (len == matches[offs]) {
								offs += 2;
								if (offs == numPairs)
									break;
							}
						}
					}
				}

				uint cur = 0;

				for (; ; )
				{
					cur++;
					if (cur == lenEnd)
						return Backward(out backRes, cur);

					uint numPairs;
					uint newLen = ReadMatchDistances(out numPairs);
					if (newLen >= mNumFastBytes) {
						mNumPairs = numPairs;
						mLongestMatchLength = newLen;
						return Backward(out backRes, cur);
					}
					position++;

					uint state;
					COptimal curOpt = mOpt[cur];
					uint posPrev = curOpt.mPosPrev;
					if (curOpt.mPrev1IsChar) {
						posPrev--;
						if (curOpt.mPrev2) {
							state = mOpt[curOpt.mPosPrev2].mState;
							if (curOpt.mBackPrev2 < LZMA_NUM_REPS)
								state = kRepNextStates[state];
							else
								state = kMatchNextStates[state];
						}
						else {
							state = mOpt[posPrev].mState;
						}
						state = kLiteralNextStates[state];
					}
					else {
						state = mOpt[posPrev].mState;
					}
					if (posPrev == cur - 1) {
						if (curOpt.IsShortRep())
							state = kShortRepNextStates[state];
						else
							state = kLiteralNextStates[state];
					}
					else {
						uint pos;
						if (curOpt.mPrev1IsChar && curOpt.mPrev2) {
							posPrev = curOpt.mPosPrev2;
							pos = curOpt.mBackPrev2;
							state = kRepNextStates[state];
						}
						else {
							pos = curOpt.mBackPrev;
							if (pos < LZMA_NUM_REPS)
								state = kRepNextStates[state];
							else
								state = kMatchNextStates[state];
						}
						COptimal prevOpt = mOpt[posPrev];
						if (pos < LZMA_NUM_REPS) {
							reps._0 = prevOpt.mBacks[pos];
							uint i = 1;
							for (; i <= pos; i++)
								reps[i] = prevOpt.mBacks[i - 1];
							for (; i < LZMA_NUM_REPS; i++)
								reps[i] = prevOpt.mBacks[i];
						}
						else {
							reps._0 = pos - LZMA_NUM_REPS;
							reps._1 = prevOpt.mBacks._0;
							reps._2 = prevOpt.mBacks._1;
							reps._3 = prevOpt.mBacks._2;
						}
					}
					curOpt.mState = state;
					curOpt.mBacks = reps;

					uint curPrice = curOpt.mPrice;
					bool nextIsChar = false;
					P<byte> data = mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - 1;
					byte curByte = data[0];
					byte matchByte = (data - (reps._0 + 1))[0];

					uint posState = position & mPbMask;

					uint curAnd1Price = curPrice + GET_PRICE_0(mIsMatch[state][posState]);
					{
						P<ushort> probs = LIT_PROBS(position, data[-1]);
						if (!IsCharState(state))
							curAnd1Price += LitEnc_GetPriceMatched(probs, curByte, matchByte, mProbPrices);
						else
							curAnd1Price += LitEnc_GetPrice(probs, curByte, mProbPrices);
					}

					COptimal nextOpt = mOpt[cur + 1];

					if (curAnd1Price < nextOpt.mPrice) {
						nextOpt.mPrice = curAnd1Price;
						nextOpt.mPosPrev = cur;
						nextOpt.MakeAsChar();
						nextIsChar = true;
					}

					uint matchPrice = curPrice + GET_PRICE_1(mIsMatch[state][posState]);
					uint repMatchPrice = matchPrice + GET_PRICE_1(mIsRep[state]);

					if (matchByte == curByte && !(nextOpt.mPosPrev < cur && nextOpt.mBackPrev == 0)) {
						uint shortRepPrice = repMatchPrice + GetRepLen1Price(state, posState);
						if (shortRepPrice <= nextOpt.mPrice) {
							nextOpt.mPrice = shortRepPrice;
							nextOpt.mPosPrev = cur;
							nextOpt.MakeAsShortRep();
							nextIsChar = true;
						}
					}

					uint numAvailFull = Math.Min(mNumAvail, kNumOpts - 1 - cur);
					if (numAvailFull < 2)
						continue;

					numAvail = numAvailFull <= mNumFastBytes ? numAvailFull : mNumFastBytes;

					if (!nextIsChar && matchByte != curByte) /* speed optimization */
					{
						/* try Literal + rep0 */
						P<byte> data2 = data - (reps._0 + 1);
						uint limit = mNumFastBytes + 1;
						if (limit > numAvailFull)
							limit = numAvailFull;

						uint temp = 1;
						while (temp < limit && data[temp] == data2[temp])
							temp++;

						uint lenTest2 = temp - 1;
						if (lenTest2 >= 2) {
							uint state2 = kLiteralNextStates[state];
							uint posStateNext = (position + 1) & mPbMask;
							uint nextRepMatchPrice = curAnd1Price + GET_PRICE_1(mIsMatch[state2][posStateNext]) + GET_PRICE_1(mIsRep[state2]);
							/* for (; lenTest2 >= 2; lenTest2--) */
							{
								uint offset = cur + 1 + lenTest2;
								while (lenEnd < offset)
									mOpt[++lenEnd].mPrice = kInfinityPrice;
								uint curAndLenPrice = nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);
								COptimal opt = mOpt[offset];
								if (curAndLenPrice < opt.mPrice) {
									opt.mPrice = curAndLenPrice;
									opt.mPosPrev = cur + 1;
									opt.mBackPrev = 0;
									opt.mPrev1IsChar = true;
									opt.mPrev2 = false;
								}
							}
						}
					}

					uint startLen = 2; /* speed optimization */

					for (uint repIndex = 0; repIndex < LZMA_NUM_REPS; repIndex++) {
						P<byte> data2 = data - (reps[repIndex] + 1);
						if (data[0] != data2[0] || data[1] != data2[1])
							continue;

						uint lenTest = 2;
						while (lenTest < numAvail && data[lenTest] == data2[lenTest])
							lenTest++;

						while (lenEnd < cur + lenTest)
							mOpt[++lenEnd].mPrice = kInfinityPrice;

						uint lenTestTemp = lenTest;
						uint price = repMatchPrice + GetPureRepPrice(repIndex, state, posState);
						do {
							uint curAndLenPrice = price + mRepLenEnc.mPrices[posState][lenTest - 2];
							COptimal opt = mOpt[cur + lenTest];
							if (curAndLenPrice < opt.mPrice) {
								opt.mPrice = curAndLenPrice;
								opt.mPosPrev = cur;
								opt.mBackPrev = repIndex;
								opt.mPrev1IsChar = false;
							}
						}
						while (--lenTest >= 2);
						lenTest = lenTestTemp;

						if (repIndex == 0)
							startLen = lenTest + 1;

						{
							uint lenTest2 = lenTest + 1;

							uint limit = lenTest2 + mNumFastBytes;
							if (limit > numAvailFull)
								limit = numAvailFull;

							while (lenTest2 < limit && data[lenTest2] == data2[lenTest2])
								lenTest2++;

							lenTest2 -= lenTest + 1;
							if (lenTest2 >= 2) {
								uint state2 = kRepNextStates[state];
								uint posStateNext = (position + lenTest) & mPbMask;
								uint curAndLenCharPrice = price
									+ mRepLenEnc.mPrices[posState][lenTest - 2]
									+ GET_PRICE_0(mIsMatch[state2][posStateNext])
									+ LitEnc_GetPriceMatched(
										LIT_PROBS(position + lenTest, data[lenTest - 1]),
										data[lenTest], data2[lenTest], mProbPrices);

								state2 = kLiteralNextStates[state2];
								posStateNext = (position + lenTest + 1) & mPbMask;
								uint nextRepMatchPrice = curAndLenCharPrice
									+ GET_PRICE_1(mIsMatch[state2][posStateNext])
									+ GET_PRICE_1(mIsRep[state2]);

								/* for (; lenTest2 >= 2; lenTest2--) */
								{
									uint offset = cur + lenTest + 1 + lenTest2;
									while (lenEnd < offset)
										mOpt[++lenEnd].mPrice = kInfinityPrice;

									uint curAndLenPrice = nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);

									COptimal opt = mOpt[offset];
									if (curAndLenPrice < opt.mPrice) {
										opt.mPrice = curAndLenPrice;
										opt.mPosPrev = cur + lenTest + 1;
										opt.mBackPrev = 0;
										opt.mPrev1IsChar = true;
										opt.mPrev2 = true;
										opt.mPosPrev2 = cur;
										opt.mBackPrev2 = repIndex;
									}
								}
							}
						}
					}

					/* for (uint lenTest = 2; lenTest <= newLen; lenTest++) */
					if (newLen > numAvail) {
						newLen = numAvail;
						numPairs = 0;
						while (newLen > matches[numPairs])
							numPairs += 2;
						matches[numPairs] = newLen;
						numPairs += 2;
					}
					if (newLen >= startLen) {
						uint normalMatchPrice = matchPrice + GET_PRICE_0(mIsRep[state]);

						while (lenEnd < cur + newLen)
							mOpt[++lenEnd].mPrice = kInfinityPrice;

						uint offs = 0;
						while (startLen > matches[offs])
							offs += 2;

						uint curBack = matches[offs + 1];
						uint posSlot = GetPosSlot2(curBack);
						for (uint lenTest = /*2*/ startLen; ; lenTest++) {
							uint curAndLenPrice = normalMatchPrice + mLenEnc.mPrices[posState][lenTest - LZMA_MATCH_LEN_MIN];
							uint lenToPosState = GetLenToPosState(lenTest);
							if (curBack < kNumFullDistances)
								curAndLenPrice += mDistancesPrices[lenToPosState][curBack];
							else
								curAndLenPrice += mPosSlotPrices[lenToPosState][posSlot] + mAlignPrices[curBack & kAlignMask];

							COptimal opt = mOpt[cur + lenTest];
							if (curAndLenPrice < opt.mPrice) {
								opt.mPrice = curAndLenPrice;
								opt.mPosPrev = cur;
								opt.mBackPrev = curBack + LZMA_NUM_REPS;
								opt.mPrev1IsChar = false;
							}

							if (lenTest == matches[offs]) {
								/* Try Match + Literal + Rep0 */
								P<byte> data2 = data - (curBack + 1);
								uint lenTest2 = lenTest + 1;
								uint limit = lenTest2 + mNumFastBytes;
								uint nextRepMatchPrice;
								if (limit > numAvailFull)
									limit = numAvailFull;

								while (lenTest2 < limit && data[lenTest2] == data2[lenTest2])
									lenTest2++;

								lenTest2 -= lenTest + 1;
								if (lenTest2 >= 2) {
									uint state2 = kMatchNextStates[state];
									uint posStateNext = (position + lenTest) & mPbMask;
									uint curAndLenCharPrice = curAndLenPrice
										+ GET_PRICE_0(mIsMatch[state2][posStateNext])
										+ LitEnc_GetPriceMatched(
											LIT_PROBS(position + lenTest, data[lenTest - 1]),
											data[lenTest], data2[lenTest], mProbPrices);

									state2 = kLiteralNextStates[state2];
									posStateNext = (posStateNext + 1) & mPbMask;
									nextRepMatchPrice = curAndLenCharPrice
										+ GET_PRICE_1(mIsMatch[state2][posStateNext])
										+ GET_PRICE_1(mIsRep[state2]);

									/* for (; lenTest2 >= 2; lenTest2--) */
									{
										uint offset = cur + lenTest + 1 + lenTest2;
										uint curAndLenPrice4;
										while (lenEnd < offset)
											mOpt[++lenEnd].mPrice = kInfinityPrice;
										curAndLenPrice4 = nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);
										COptimal opt4 = mOpt[offset];
										if (curAndLenPrice4 < opt4.mPrice) {
											opt4.mPrice = curAndLenPrice4;
											opt4.mPosPrev = cur + lenTest + 1;
											opt4.mBackPrev = 0;
											opt4.mPrev1IsChar = true;
											opt4.mPrev2 = true;
											opt4.mPosPrev2 = cur;
											opt4.mBackPrev2 = curBack + LZMA_NUM_REPS;
										}
									}
								}

								offs += 2;
								if (offs == numPairs)
									break;

								curBack = matches[offs + 1];
								if (curBack >= kNumFullDistances)
									posSlot = GetPosSlot2(curBack);
							}
						}
					}
				}
			}

			private uint GetOptimumFast(out uint backRes) {
				uint mainLen, numPairs;
				if (mAdditionalOffset == 0) {
					mainLen = ReadMatchDistances(out numPairs);
				}
				else {
					mainLen = mLongestMatchLength;
					numPairs = mNumPairs;
				}

				uint numAvail = mNumAvail;
				backRes = ~0u;
				if (numAvail < 2)
					return 1;
				if (numAvail > LZMA_MATCH_LEN_MAX)
					numAvail = LZMA_MATCH_LEN_MAX;
				P<byte> data = mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - 1;

				uint repLen = 0;
				uint repIndex = 0;
				for (uint i = 0; i < LZMA_NUM_REPS; i++) {
					P<byte> data2 = data - (mReps[i] + 1);
					if (data[0] != data2[0] || data[1] != data2[1])
						continue;

					uint len = 2;
					while (len < numAvail && data[len] == data2[len])
						len++;

					if (len >= mNumFastBytes) {
						backRes = i;
						MovePos(len - 1);
						return len;
					}

					if (len > repLen) {
						repIndex = i;
						repLen = len;
					}
				}

				P<uint> matches = mMatches;
				if (mainLen >= mNumFastBytes) {
					backRes = matches[numPairs - 1] + LZMA_NUM_REPS;
					MovePos(mainLen - 1);
					return mainLen;
				}

				uint mainDist = 0;
				if (mainLen >= 2) {
					mainDist = matches[numPairs - 1];
					while (numPairs > 2 && mainLen == matches[numPairs - 4] + 1) {
						if (!ChangePair(matches[numPairs - 3], mainDist))
							break;

						numPairs -= 2;
						mainLen = matches[numPairs - 2];
						mainDist = matches[numPairs - 1];
					}

					if (mainLen == 2 && mainDist >= 0x80)
						mainLen = 1;
				}

				if (repLen >= 2 && (
					(repLen + 1 >= mainLen) ||
					(repLen + 2 >= mainLen && mainDist >= (1 << 9)) ||
					(repLen + 3 >= mainLen && mainDist >= (1 << 15)))) {
					backRes = repIndex;
					MovePos(repLen - 1);
					return repLen;
				}

				if (mainLen < 2 || numAvail <= 2)
					return 1;

				mLongestMatchLength = ReadMatchDistances(out mNumPairs);
				if (mLongestMatchLength >= 2) {
					uint newDistance = matches[mNumPairs - 1];
					if ((mLongestMatchLength >= mainLen && newDistance < mainDist) ||
						(mLongestMatchLength == mainLen + 1 && !ChangePair(mainDist, newDistance)) ||
						(mLongestMatchLength > mainLen + 1) ||
						(mLongestMatchLength + 1 >= mainLen && mainLen >= 3 && ChangePair(newDistance, mainDist)))
						return 1;
				}

				data = mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - 1;
				for (uint i = 0; i < LZMA_NUM_REPS; i++) {
					P<byte> data2 = data - (mReps[i] + 1);
					if (data[0] != data2[0] || data[1] != data2[1])
						continue;

					uint limit = mainLen - 1;

					uint len = 2;
					while (len < limit && data[len] == data2[len])
						len++;

					if (len >= limit)
						return 1;
				}

				backRes = mainDist + LZMA_NUM_REPS;
				MovePos(mainLen - 2);
				return mainLen;
			}

			private void WriteEndMarker(uint posState) {
				mRC.RangeEnc_EncodeBit(ref mIsMatch[mState][posState], 1);
				mRC.RangeEnc_EncodeBit(ref mIsRep[mState], 0);
				mState = kMatchNextStates[mState];
				uint len = LZMA_MATCH_LEN_MIN;
				mLenEnc.LenEnc_Encode2(mRC, len - LZMA_MATCH_LEN_MIN, posState, !mFastMode, mProbPrices);
				RcTree_Encode(mRC, mPosSlotEncoder[GetLenToPosState(len)], kNumPosSlotBits, (1 << kNumPosSlotBits) - 1);
				mRC.RangeEnc_EncodeDirectBits(((1u << 30) - 1) >> kNumAlignBits, 30 - kNumAlignBits);
				RcTree_ReverseEncode(mRC, mPosAlignEncoder, kNumAlignBits, kAlignMask);
			}

			private int CheckErrors() {
				if (mResult != SZ_OK)
					return mResult;

				if (mRC.mRes != SZ_OK)
					mResult = SZ_ERROR_WRITE;

				if (mMatchFinderBase.mResult != SZ_OK)
					mResult = SZ_ERROR_READ;

				if (mResult != SZ_OK)
					mFinished = true;

				return mResult;
			}

			private int Flush(uint nowPos) {
				/* ReleaseMFStream(); */
				mFinished = true;

				if (mWriteEndMark)
					WriteEndMarker(nowPos & mPbMask);

				mRC.RangeEnc_FlushData();
				mRC.RangeEnc_FlushStream();

				return CheckErrors();
			}

			private void FillAlignPrices() {
				for (uint i = 0; i < kAlignTableSize; i++)
					mAlignPrices[i] = RcTree_ReverseGetPrice(mPosAlignEncoder, kNumAlignBits, i, mProbPrices);

				mAlignPriceCount = 0;
			}

			private void FillDistancesPrices() {
				uint[] tempPrices = new uint[kNumFullDistances];

				for (uint i = kStartPosModelIndex; i < kNumFullDistances; i++) {
					uint posSlot = GetPosSlot1(i);
					uint footerBits = (posSlot >> 1) - 1;
					uint @base = (2u | (posSlot & 1u)) << (int)footerBits;
					tempPrices[i] = RcTree_ReverseGetPrice(P.From(mPosEncoders, @base - posSlot - 1), (int)footerBits, i - @base, mProbPrices);
				}

				for (uint lenToPosState = 0; lenToPosState < kNumLenToPosStates; lenToPosState++) {
					P<ushort> encoder = mPosSlotEncoder[lenToPosState];
					P<uint> posSlotPrices = mPosSlotPrices[lenToPosState];

					for (uint posSlot = 0; posSlot < mDistTableSize; posSlot++)
						posSlotPrices[posSlot] = RcTree_GetPrice(encoder, kNumPosSlotBits, posSlot, mProbPrices);

					for (uint posSlot = kEndPosModelIndex; posSlot < mDistTableSize; posSlot++)
						posSlotPrices[posSlot] += ((posSlot >> 1) - 1 - kNumAlignBits) << kNumBitPriceShiftBits;

					{
						P<uint> distancesPrices = mDistancesPrices[lenToPosState];
						uint i = 0;
						for (; i < kStartPosModelIndex; i++)
							distancesPrices[i] = posSlotPrices[i];
						for (; i < kNumFullDistances; i++)
							distancesPrices[i] = posSlotPrices[GetPosSlot1(i)] + tempPrices[i];
					}
				}

				mMatchPriceCount = 0;
			}

			internal CLzmaEnc() // LzmaEnc_Construct
			{
				mRC.RangeEnc_Construct();
				mMatchFinderBase = new CMatchFinder();
				LzmaEnc_SetProps(CLzmaEncProps.LzmaEncProps_Init());
				LzmaEnc_FastPosInit();
				LzmaEnc_InitPriceTables(mProbPrices);
				mLitProbs = null;
				mSaveState.mLitProbs = null;
			}

			internal void LzmaEnc_FreeLits(SzAlloc alloc) {
				alloc.FreeUInt16(alloc, mLitProbs);
				alloc.FreeUInt16(alloc, mSaveState.mLitProbs);
				mLitProbs = null;
				mSaveState.mLitProbs = null;
			}

			internal int LzmaEnc_CodeOneBlock(bool useLimits, uint maxPackSize, uint maxUnpackSize) {
				if (mNeedInit) {
					mMatchFinder.Init(mMatchFinderObj);
					mNeedInit = false;
				}

				if (mFinished)
					return mResult;

				int res;
				if ((res = CheckErrors()) != SZ_OK)
					return res;

				uint nowPos32 = (uint)mNowPos64;
				uint startPos32 = nowPos32;

				if (mNowPos64 == 0) {
					if (mMatchFinder.GetNumAvailableBytes(mMatchFinderObj) == 0)
						return Flush(nowPos32);
					ReadMatchDistances(out _);
					mRC.RangeEnc_EncodeBit(ref mIsMatch[mState][0], 0);
					mState = kLiteralNextStates[mState];
					byte curByte = mMatchFinder.GetIndexByte(mMatchFinderObj, (int)-mAdditionalOffset);
					LitEnc_Encode(mRC, mLitProbs, curByte);
					mAdditionalOffset--;
					nowPos32++;
				}

				if (mMatchFinder.GetNumAvailableBytes(mMatchFinderObj) != 0) {
					for (; ; )
					{
						uint len, pos;
						if (mFastMode)
							len = GetOptimumFast(out pos);
						else
							len = GetOptimum(nowPos32, out pos);

						uint posState = nowPos32 & mPbMask;
						if (len == 1 && pos == ~0u) {
							mRC.RangeEnc_EncodeBit(ref mIsMatch[mState][posState], 0);
							P<byte> data = mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - mAdditionalOffset;
							byte curByte = data[0];
							P<ushort> probs = LIT_PROBS(nowPos32, (data - 1)[0]);

							if (IsCharState(mState))
								LitEnc_Encode(mRC, probs, curByte);
							else
								LitEnc_EncodeMatched(mRC, probs, curByte, (data - mReps._0 - 1)[0]);

							mState = kLiteralNextStates[mState];
						}
						else {
							mRC.RangeEnc_EncodeBit(ref mIsMatch[mState][posState], 1);
							if (pos < LZMA_NUM_REPS) {
								mRC.RangeEnc_EncodeBit(ref mIsRep[mState], 1);
								if (pos == 0) {
									mRC.RangeEnc_EncodeBit(ref mIsRepG0[mState], 0);
									mRC.RangeEnc_EncodeBit(ref mIsRep0Long[mState][posState], (len == 1) ? 0u : 1u);
								}
								else {
									uint distance = mReps[pos];

									mRC.RangeEnc_EncodeBit(ref mIsRepG0[mState], 1);

									if (pos == 1) {
										mRC.RangeEnc_EncodeBit(ref mIsRepG1[mState], 0);
									}
									else {
										mRC.RangeEnc_EncodeBit(ref mIsRepG1[mState], 1);
										mRC.RangeEnc_EncodeBit(ref mIsRepG2[mState], pos - 2);
										if (pos == 3)
											mReps._3 = mReps._2;
										mReps._2 = mReps._1;
									}

									mReps._1 = mReps._0;
									mReps._0 = distance;
								}

								if (len == 1) {
									mState = kShortRepNextStates[mState];
								}
								else {
									mRepLenEnc.LenEnc_Encode2(mRC, len - LZMA_MATCH_LEN_MIN, posState, !mFastMode, mProbPrices);
									mState = kRepNextStates[mState];
								}
							}
							else {
								mRC.RangeEnc_EncodeBit(ref mIsRep[mState], 0);
								mState = kMatchNextStates[mState];
								mLenEnc.LenEnc_Encode2(mRC, len - LZMA_MATCH_LEN_MIN, posState, !mFastMode, mProbPrices);
								pos -= LZMA_NUM_REPS;
								uint posSlot = GetPosSlot(pos);
								RcTree_Encode(mRC, mPosSlotEncoder[GetLenToPosState(len)], kNumPosSlotBits, posSlot);

								if (posSlot >= kStartPosModelIndex) {
									int footerBits = (int)((posSlot >> 1) - 1);
									uint @base = (2 | (posSlot & 1)) << footerBits;
									uint posReduced = pos - @base;

									if (posSlot < kEndPosModelIndex)
										RcTree_ReverseEncode(mRC, P.From(mPosEncoders, @base - posSlot - 1), footerBits, posReduced);
									else {
										mRC.RangeEnc_EncodeDirectBits(posReduced >> kNumAlignBits, footerBits - kNumAlignBits);
										RcTree_ReverseEncode(mRC, mPosAlignEncoder, kNumAlignBits, posReduced & kAlignMask);
										mAlignPriceCount++;
									}
								}
								mReps._3 = mReps._2;
								mReps._2 = mReps._1;
								mReps._1 = mReps._0;
								mReps._0 = pos;
								mMatchPriceCount++;
							}
						}

						mAdditionalOffset -= len;
						nowPos32 += len;

						if (mAdditionalOffset == 0) {
							if (!mFastMode) {
								if (mMatchPriceCount >= (1 << 7))
									FillDistancesPrices();
								if (mAlignPriceCount >= kAlignTableSize)
									FillAlignPrices();
							}

							if (mMatchFinder.GetNumAvailableBytes(mMatchFinderObj) == 0)
								break;

							uint processed = nowPos32 - startPos32;
							if (useLimits) {
								if (processed + kNumOpts + 300 >= maxUnpackSize
									|| mRC.RangeEnc_GetProcessed() + kNumOpts * 2 >= maxPackSize)
									break;
							}
							else if (processed >= (1 << 15)) {
								mNowPos64 += nowPos32 - startPos32;
								return CheckErrors();
							}
						}
					}
				}

				mNowPos64 += nowPos32 - startPos32;
				return Flush(nowPos32);
			}

			private const uint kBigHashDicLimit = 1u << 24;

			private int LzmaEnc_Alloc(uint keepWindowSize, SzAlloc alloc, SzAlloc allocBig) {
				if (!mRC.RangeEnc_Alloc(alloc))
					return SZ_ERROR_MEM;
				_ = mMatchFinderBase.mBtMode;

				int lclp = mLC + mLP;
				if (mLitProbs == null || mSaveState.mLitProbs == null || mLcLp != lclp) {
					LzmaEnc_FreeLits(alloc);
					mLitProbs = alloc.AllocUInt16(alloc, 0x300 << lclp);
					mSaveState.mLitProbs = alloc.AllocUInt16(alloc, 0x300 << lclp);
					if (mLitProbs == null || mSaveState.mLitProbs == null) {
						LzmaEnc_FreeLits(alloc);
						return SZ_ERROR_MEM;
					}
					mLcLp = lclp;
				}

				mMatchFinderBase.mBigHash = mDictSize > kBigHashDicLimit;

				uint beforeSize = kNumOpts;
				if (beforeSize + mDictSize < keepWindowSize)
					beforeSize = keepWindowSize - mDictSize;

				if (!mMatchFinderBase.MatchFinder_Create(mDictSize, beforeSize, mNumFastBytes, LZMA_MATCH_LEN_MAX, allocBig))
					return SZ_ERROR_MEM;

				mMatchFinderObj = mMatchFinderBase;
				MatchFinder_CreateVTable(mMatchFinderBase, out mMatchFinder);

				return SZ_OK;
			}

			internal void LzmaEnc_Init() {
				mState = 0;
				mReps = new OptimumReps();

				mRC.RangeEnc_Init();

				for (uint i = 0; i < kNumStates; i++) {
					for (uint j = 0; j < LZMA_NUM_PB_STATES_MAX; j++) {
						mIsMatch[i][j] = kProbInitValue;
						mIsRep0Long[i][j] = kProbInitValue;
					}

					mIsRep[i] = kProbInitValue;
					mIsRepG0[i] = kProbInitValue;
					mIsRepG1[i] = kProbInitValue;
					mIsRepG2[i] = kProbInitValue;
				}

				uint n = 0x300u << (mLP + mLC);
				for (uint i = 0; i < n; i++)
					mLitProbs[i] = kProbInitValue;

				for (uint i = 0; i < kNumLenToPosStates; i++) {
					P<ushort> probs = mPosSlotEncoder[i];
					for (uint j = 0; j < (1 << kNumPosSlotBits); j++)
						probs[j] = kProbInitValue;
				}

				for (uint i = 0; i < kNumFullDistances - kEndPosModelIndex; i++)
					mPosEncoders[i] = kProbInitValue;

				mLenEnc.LenEnc_Init();
				mRepLenEnc.LenEnc_Init();

				for (uint i = 0; i < (1 << kNumAlignBits); i++)
					mPosAlignEncoder[i] = kProbInitValue;

				mOptimumEndIndex = 0;
				mOptimumCurrentIndex = 0;
				mAdditionalOffset = 0;

				mPbMask = (1u << mPB) - 1;
				mLpMask = (1u << mLP) - 1;
			}

			internal void LzmaEnc_InitPrices() {
				if (!mFastMode) {
					FillDistancesPrices();
					FillAlignPrices();
				}

				uint tableSize = mNumFastBytes + 1 - LZMA_MATCH_LEN_MIN;
				mLenEnc.mTableSize = tableSize;
				mRepLenEnc.mTableSize = tableSize;
				mLenEnc.LenPriceEnc_UpdateTables(1u << mPB, mProbPrices);
				mRepLenEnc.LenPriceEnc_UpdateTables(1u << mPB, mProbPrices);
			}

			internal int LzmaEnc_AllocAndInit(uint keepWindowSize, SzAlloc alloc, SzAlloc allocBig) {
				{
					uint i;
					for (i = 0; i < kDicLogSizeMaxCompress; i++)
						if (mDictSize <= (1u << (int)i))
							break;

					mDistTableSize = i * 2;
				}

				mFinished = false;
				mResult = SZ_OK;

				int res;
				if ((res = LzmaEnc_Alloc(keepWindowSize, alloc, allocBig)) != SZ_OK)
					return res;

				LzmaEnc_Init();
				LzmaEnc_InitPrices();
				mNowPos64 = 0;
				return SZ_OK;
			}

			internal void LzmaEnc_SetInputBuf(P<byte> src, long srcLen) {
				mMatchFinderBase.mDirectInput = true;
				mMatchFinderBase.mBufferBase = src;
				mMatchFinderBase.mDirectInputRem = srcLen;
			}

			internal int LzmaEnc_Prepare(ISeqOutStream outStream, ISeqInStream inStream, SzAlloc alloc, SzAlloc allocBig) {
				mMatchFinderBase.mStream = inStream;
				mNeedInit = true;
				mRC.mOutStream = outStream;
				return LzmaEnc_AllocAndInit(0, alloc, allocBig);
			}

			internal int LzmaEnc_PrepareForLzma2(ISeqInStream inStream, uint keepWindowSize, SzAlloc alloc, SzAlloc allocBig) {
				mMatchFinderBase.mStream = inStream;
				mNeedInit = true;
				return LzmaEnc_AllocAndInit(keepWindowSize, alloc, allocBig);
			}

			internal int LzmaEnc_MemPrepare(P<byte> src, long srcLen, uint keepWindowSize, SzAlloc alloc, SzAlloc allocBig) {
				LzmaEnc_SetInputBuf(src, srcLen);
				mNeedInit = true;
				return LzmaEnc_AllocAndInit(keepWindowSize, alloc, allocBig);
			}

			internal void LzmaEnc_Finish() {
			}

			internal uint LzmaEnc_GetNumAvailableBytes() {
				return mMatchFinder.GetNumAvailableBytes(mMatchFinderObj);
			}

			internal P<byte> LzmaEnc_GetCurBuf() {
				return mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - mAdditionalOffset;
			}

			internal int LzmaEnc_CodeOneMemBlock(bool reInit, P<byte> dest, ref long destLen, uint desiredPackSize, ref uint unpackSize) {
				CSeqOutStreamBuf outStream = new CSeqOutStreamBuf {
					mData = dest,
					mRem = destLen,
					mOverflow = false
				};

				mWriteEndMark = false;
				mFinished = false;
				mResult = SZ_OK;

				if (reInit)
					LzmaEnc_Init();

				LzmaEnc_InitPrices();
				ulong nowPos64 = mNowPos64;
				mRC.RangeEnc_Init();
				mRC.mOutStream = outStream;

				int res = LzmaEnc_CodeOneBlock(true, desiredPackSize, unpackSize);

				unpackSize = (uint)(mNowPos64 - nowPos64);
				destLen -= outStream.mRem;
				if (outStream.mOverflow)
					return SZ_ERROR_OUTPUT_EOF;

				return res;
			}

			private int LzmaEnc_Encode2(ICompressProgress progress) {
				_ = SZ_OK;
				int res;
				for (; ; )
				{
					res = LzmaEnc_CodeOneBlock(false, 0, 0);
					if (res != SZ_OK || mFinished)
						break;

					if (progress != null) {
						res = progress.Progress(mNowPos64, mRC.RangeEnc_GetProcessed());
						if (res != SZ_OK) {
							res = SZ_ERROR_PROGRESS;
							break;
						}
					}
				}

				LzmaEnc_Finish();
				return res;
			}

			private void MovePos(uint num) {
				if (num != 0) {
					mAdditionalOffset += num;
					mMatchFinder.Skip(mMatchFinderObj, num);
				}
			}

			private uint ReadMatchDistances(out uint numDistancePairsRes) {
				mNumAvail = mMatchFinder.GetNumAvailableBytes(mMatchFinderObj);
				uint numPairs = mMatchFinder.GetMatches(mMatchFinderObj, mMatches);

				uint lenRes = 0;
				if (numPairs > 0) {
					lenRes = mMatches[numPairs - 2];
					if (lenRes == mNumFastBytes) {
						P<byte> pby = mMatchFinder.GetPointerToCurrentPos(mMatchFinderObj) - 1;
						uint distance = mMatches[numPairs - 1] + 1;

						uint numAvail = mNumAvail;
						if (numAvail > LZMA_MATCH_LEN_MAX)
							numAvail = LZMA_MATCH_LEN_MAX;

						P<byte> pby2 = pby - distance;
						while (lenRes < numAvail && pby[lenRes] == pby2[lenRes])
							lenRes++;
					}
				}

				mAdditionalOffset++;
				numDistancePairsRes = numPairs;
				return lenRes;
			}

			private uint GetRepLen1Price(uint state, uint posState) {
				return GET_PRICE_0(mIsRepG0[state])
					+ GET_PRICE_0(mIsRep0Long[state][posState]);
			}

			private uint GetPureRepPrice(uint repIndex, uint state, uint posState) {
				uint price;
				if (repIndex == 0) {
					price = GET_PRICE_0(mIsRepG0[state]);
					price += GET_PRICE_1(mIsRep0Long[state][posState]);
				}
				else {
					price = GET_PRICE_1(mIsRepG0[state]);
					if (repIndex == 1) {
						price += GET_PRICE_0(mIsRepG1[state]);
					}
					else {
						price += GET_PRICE_1(mIsRepG1[state]);
						price += GET_PRICE(mIsRepG2[state], repIndex - 2);
					}
				}
				return price;
			}

			private uint GetRepPrice(uint repIndex, uint len, uint state, uint posState) {
				return mRepLenEnc.mPrices[posState][len - LZMA_MATCH_LEN_MIN]
					+ GetPureRepPrice(repIndex, state, posState);
			}

			private uint Backward(out uint backRes, uint cur) {
				uint posMem = mOpt[cur].mPosPrev;
				uint backMem = mOpt[cur].mBackPrev;
				mOptimumEndIndex = cur;
				do {
					if (mOpt[cur].mPrev1IsChar) {
						mOpt[posMem].MakeAsChar();
						mOpt[posMem].mPosPrev = posMem - 1;

						if (mOpt[cur].mPrev2) {
							mOpt[posMem - 1].mPrev1IsChar = false;
							mOpt[posMem - 1].mPosPrev = mOpt[cur].mPosPrev2;
							mOpt[posMem - 1].mBackPrev = mOpt[cur].mBackPrev2;
						}
					}

					uint posPrev = posMem;
					uint backCur = backMem;

					backMem = mOpt[posPrev].mBackPrev;
					posMem = mOpt[posPrev].mPosPrev;

					mOpt[posPrev].mBackPrev = backCur;
					mOpt[posPrev].mPosPrev = cur;
					cur = posPrev;
				}
				while (cur != 0);
				backRes = mOpt[0].mBackPrev;
				mOptimumCurrentIndex = mOpt[0].mPosPrev;
				return mOptimumCurrentIndex;
			}

			#endregion

			#region Private Methods

			private uint GET_PRICE(ushort prob, uint symbol) {
				return GET_PRICE(mProbPrices, prob, symbol);
			}

			private uint GET_PRICE_0(ushort prob) {
				return GET_PRICE_0(mProbPrices, prob);
			}

			private uint GET_PRICE_1(ushort prob) {
				return GET_PRICE_1(mProbPrices, prob);
			}

			#endregion
		}

		/* ---------- One Call Interface ---------- */

		/* LzmaEncode
        Return code:
          SZ_OK               - OK
          SZ_ERROR_MEM        - Memory allocation error
          SZ_ERROR_PARAM      - Incorrect paramater
          SZ_ERROR_OUTPUT_EOF - output buffer overflow
          SZ_ERROR_THREAD     - errors in multithreading functions (only for Mt version)
        */

		public static int LzmaEncode(P<byte> dest, ref long destLen, P<byte> src, long srcLen, CLzmaEncProps props, P<byte> propsEncoded, ref long propsSize, bool writeEndMark, ICompressProgress progress, SzAlloc alloc, SzAlloc allocBig) {
			CLzmaEnc encoder = CLzmaEnc.LzmaEnc_Create();
			if (encoder == null)
				return SZ_ERROR_MEM;

			int res;
			res = encoder.LzmaEnc_SetProps(props);
			if (res == SZ_OK) {
				res = encoder.LzmaEnc_WriteProperties(propsEncoded, ref propsSize);
				if (res == SZ_OK)
					res = encoder.LzmaEnc_MemEncode(dest, ref destLen, src, srcLen, writeEndMark, progress, alloc, allocBig);
			}

			encoder.LzmaEnc_Destroy(alloc, allocBig);
			return res;
		}

		/*
        RAM requirements for LZMA:
          for compression:   (dictSize * 11.5 + 6 MB) + state_size
          for decompression: dictSize + state_size
            state_size = (4 + (1.5 << (lc + lp))) KB
            by default (lc=3, lp=0), state_size = 16 KB.

        LZMA properties (5 bytes) format
            Offset Size  Description
              0     1    lc, lp and pb in encoded form.
              1     4    dictSize (little endian).
        */

		/*
        LzmaCompress
        ------------

        outPropsSize -
             In:  the pointer to the size of outProps buffer; *outPropsSize = LZMA_PROPS_SIZE = 5.
             Out: the pointer to the size of written properties in outProps buffer; *outPropsSize = LZMA_PROPS_SIZE = 5.

          LZMA Encoder will use defult values for any parameter, if it is
          -1  for any from: level, loc, lp, pb, fb, numThreads
           0  for dictSize
  
        level - compression level: 0 <= level <= 9;

          level dictSize algo  fb
            0:    16 KB   0    32
            1:    64 KB   0    32
            2:   256 KB   0    32
            3:     1 MB   0    32
            4:     4 MB   0    32
            5:    16 MB   1    32
            6:    32 MB   1    32
            7+:   64 MB   1    64
 
          The default value for "level" is 5.

          algo = 0 means fast method
          algo = 1 means normal method

        dictSize - The dictionary size in bytes. The maximum value is
                128 MB = (1 << 27) bytes for 32-bit version
                  1 GB = (1 << 30) bytes for 64-bit version
             The default value is 16 MB = (1 << 24) bytes.
             It's recommended to use the dictionary that is larger than 4 KB and
             that can be calculated as (1 << N) or (3 << N) sizes.

        lc - The number of literal context bits (high bits of previous literal).
             It can be in the range from 0 to 8. The default value is 3.
             Sometimes lc=4 gives the gain for big files.

        lp - The number of literal pos bits (low bits of current position for literals).
             It can be in the range from 0 to 4. The default value is 0.
             The lp switch is intended for periodical data when the period is equal to 2^lp.
             For example, for 32-bit (4 bytes) periodical data you can use lp=2. Often it's
             better to set lc=0, if you change lp switch.

        pb - The number of pos bits (low bits of current position).
             It can be in the range from 0 to 4. The default value is 2.
             The pb switch is intended for periodical data when the period is equal 2^pb.

        fb - Word size (the number of fast bytes).
             It can be in the range from 5 to 273. The default value is 32.
             Usually, a big number gives a little bit better compression ratio and
             slower compression process.

        numThreads - The number of thereads. 1 or 2. The default value is 2.
             Fast mode (algo = 0) can use only 1 thread.

        Out:
          destLen  - processed output size
        Returns:
          SZ_OK               - OK
          SZ_ERROR_MEM        - Memory allocation error
          SZ_ERROR_PARAM      - Incorrect paramater
          SZ_ERROR_OUTPUT_EOF - output buffer overflow
          SZ_ERROR_THREAD     - errors in multithreading functions (only for Mt version)
        */

		public static int LzmaCompress(
			P<byte> dest, ref long destLen,
			P<byte> src, long srcLen,
			P<byte> outProps, ref long outPropsSize, /* *outPropsSize must be = 5 */
			int level,      /* 0 <= level <= 9, default = 5 */
			uint dictSize,  /* default = (1 << 24) */
			int lc,         /* 0 <= lc <= 8, default = 3  */
			int lp,         /* 0 <= lp <= 4, default = 0  */
			int pb,         /* 0 <= pb <= 4, default = 2  */
			int fb,         /* 5 <= fb <= 273, default = 32 */
			int numThreads) /* 1 or 2, default = 2 */
		{
			CLzmaEncProps props = CLzmaEncProps.LzmaEncProps_Init();
			props.mLevel = level;
			props.mDictSize = dictSize;
			props.mLC = lc;
			props.mLP = lp;
			props.mPB = pb;
			props.mFB = fb;
			props.mNumThreads = numThreads;

			return LzmaEncode(dest, ref destLen, src, srcLen, props, outProps, ref outPropsSize, false, null, SzAlloc.SmallAlloc, SzAlloc.BigAlloc);
		}
	}
}
#pragma warning restore CA1034
