﻿//------------------------------------------------------------------------------
//	<auto-generated>
//		This code was generated from a template.
//		Manual changes to this file will be overwritten if the code is regenerated.
//	</auto-generated>
//------------------------------------------------------------------------------

using System;

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms
{
	internal sealed class ConvolverBgraByte : IConvolver
	{
		private const int channels = 4;

		public static readonly ConvolverBgraByte Instance = new ConvolverBgraByte();

		private ConvolverBgraByte() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0, a1 = 0, a2 = 0, aa = 0, aw = 0;

				nuint ix = *pmapx++;
				byte* ip = istart + ix * channels;
				byte* ipe = ip + smapx * channels - 4 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					int alpha = ip[3];
					int w = mp[0];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += ip[0] * w;
						a1 += ip[1] * w;
						a2 += ip[2] * w;
					}

					alpha = ip[7];
					w = mp[1];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += ip[4] * w;
						a1 += ip[5] * w;
						a2 += ip[6] * w;
					}

					alpha = ip[11];
					w = mp[2];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += ip[8] * w;
						a1 += ip[9] * w;
						a2 += ip[10] * w;
					}

					alpha = ip[15];
					w = mp[3];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += ip[12] * w;
						a1 += ip[13] * w;
						a2 += ip[14] * w;
					}

					ip += 4 * channels;
					mp += 4;
				}

				ipe += 4 * channels;
				while (ip < ipe)
				{
					int alpha = ip[3];
					int w = mp[0];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += ip[0] * w;
						a1 += ip[1] * w;
						a2 += ip[2] * w;
					}

					ip += channels;
					mp++;
				}

				if (aw != 0)
				{
					int wf = aw == UQ15One ? UQ15One : ((UQ15One * UQ15One) / (UQ15One - aw));
					a0 = UnFix15(a0) * wf;
					a1 = UnFix15(a1) * wf;
					a2 = UnFix15(a2) * wf;
				}

				tp[0] = UnFix8(a0);
				tp[1] = UnFix8(a1);
				tp[2] = UnFix8(a2);
				tp[3] = UnFix15(aa);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			byte* op = ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0, a1 = 0, a2 = 0, aa = 0, aw = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 2 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					int alpha = tp[3];
					int w = mp[0];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += tp[0] * w;
						a1 += tp[1] * w;
						a2 += tp[2] * w;
					}

					alpha = tp[7];
					w = mp[1];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += tp[4] * w;
						a1 += tp[5] * w;
						a2 += tp[6] * w;
					}

					tp += 2 * channels;
					mp += 2;
				}

				tpe += 2 * channels;
				while (tp < tpe)
				{
					int alpha = tp[3];
					int w = mp[0];

					aa += alpha * w;
					if (alpha < byte.MaxValue)
					{
						int pw = UnFix8(w * alpha);
						aw += w - pw;
						w = pw;
					}

					if (w != 0)
					{
						a0 += tp[0] * w;
						a1 += tp[1] * w;
						a2 += tp[2] * w;
					}

					tp += channels;
					mp++;
				}

				if (aa <= UQ15Round)
				{
					a0 = a1 = a2 = aa = 0;
				}
				else if (aw != 0)
				{
					int wf = aw == UQ15One ? UQ15One : ((UQ15One * UQ15One) / (UQ15One - aw));
					a0 = UnFix15(a0) * wf;
					a1 = UnFix15(a1) * wf;
					a2 = UnFix15(a2) * wf;
				}

				op[0] = UnFix22ToByte(a0);
				op[1] = UnFix22ToByte(a1);
				op[2] = UnFix22ToByte(a2);
				op[3] = UnFix15ToByte(aa);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			int iamt = Fix15(amt);
			int threshold = RoundF(thresh * byte.MaxValue);

			byte* ip = cstart + ox * channels, yp = ystart + ox, bp = bstart, op = ostart;

			for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
			{
				int dif = *yp++ - *bp++;

				byte c0 = ip[0], c1 = ip[1], c2 = ip[2], c3 = ip[3];
				if (threshold == 0 || Math.Abs(dif) > threshold)
				{
					dif = UnFix15(dif * iamt);
					op[0] = ClampToByte(c0 + dif);
					op[1] = ClampToByte(c1 + dif);
					op[2] = ClampToByte(c2 + dif);
					op[3] = c3;
				}
				else
				{
					op[0] = c0;
					op[1] = c1;
					op[2] = c2;
					op[3] = c3;
				}
			}
		}

		public override string ToString() => nameof(ConvolverBgraByte);
	}

	internal sealed class Convolver4ChanByte : IConvolver
	{
		private const int channels = 4;

		public static readonly Convolver4ChanByte Instance = new Convolver4ChanByte();

		private Convolver4ChanByte() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0, a1 = 0, a2 = 0, a3 = 0;

				nuint ix = *pmapx++;
				byte* ip = istart + ix * channels;
				byte* ipe = ip + smapx * channels - 4 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;
					a3 += ip[3] * w;

					w = mp[1];
					a0 += ip[4] * w;
					a1 += ip[5] * w;
					a2 += ip[6] * w;
					a3 += ip[7] * w;

					w = mp[2];
					a0 += ip[8] * w;
					a1 += ip[9] * w;
					a2 += ip[10] * w;
					a3 += ip[11] * w;

					w = mp[3];
					a0 += ip[12] * w;
					a1 += ip[13] * w;
					a2 += ip[14] * w;
					a3 += ip[15] * w;

					ip += 4 * channels;
					mp += 4;
				}

				ipe += 4 * channels;
				while (ip < ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;
					a3 += ip[3] * w;

					ip += channels;
					mp++;
				}

				tp[0] = UnFix8(a0);
				tp[1] = UnFix8(a1);
				tp[2] = UnFix8(a2);
				tp[3] = UnFix8(a3);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			byte* op = ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0, a1 = 0, a2 = 0, a3 = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 2 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;
					a3 += tp[3] * w;

					w = mp[1];
					a0 += tp[4] * w;
					a1 += tp[5] * w;
					a2 += tp[6] * w;
					a3 += tp[7] * w;

					tp += 2 * channels;
					mp += 2;
				}

				tpe += 2 * channels;
				while (tp < tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;
					a3 += tp[3] * w;

					tp += channels;
					mp++;
				}

				op[0] = UnFix22ToByte(a0);
				op[1] = UnFix22ToByte(a1);
				op[2] = UnFix22ToByte(a2);
				op[3] = UnFix22ToByte(a3);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			int iamt = Fix15(amt);
			int threshold = RoundF(thresh * byte.MaxValue);

			byte* ip = cstart + ox * channels, yp = ystart + ox, bp = bstart, op = ostart;

			for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
			{
				int dif = *yp++ - *bp++;

				byte c0 = ip[0], c1 = ip[1], c2 = ip[2], c3 = ip[3];
				if (threshold == 0 || Math.Abs(dif) > threshold)
				{
					dif = UnFix15(dif * iamt);
					op[0] = ClampToByte(c0 + dif);
					op[1] = ClampToByte(c1 + dif);
					op[2] = ClampToByte(c2 + dif);
					op[3] = c3;
				}
				else
				{
					op[0] = c0;
					op[1] = c1;
					op[2] = c2;
					op[3] = c3;
				}
			}
		}

		public override string ToString() => nameof(Convolver4ChanByte);
	}

	internal sealed class Convolver4ChanUQ15 : IConvolver
	{
		private const int channels = 4;

		public static readonly Convolver4ChanUQ15 Instance = new Convolver4ChanUQ15();

		private Convolver4ChanUQ15() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0, a1 = 0, a2 = 0, a3 = 0;

				nuint ix = *pmapx++;
				ushort* ip = (ushort*)istart + ix * channels;
				ushort* ipe = ip + smapx * channels - 4 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;
					a3 += ip[3] * w;

					w = mp[1];
					a0 += ip[4] * w;
					a1 += ip[5] * w;
					a2 += ip[6] * w;
					a3 += ip[7] * w;

					w = mp[2];
					a0 += ip[8] * w;
					a1 += ip[9] * w;
					a2 += ip[10] * w;
					a3 += ip[11] * w;

					w = mp[3];
					a0 += ip[12] * w;
					a1 += ip[13] * w;
					a2 += ip[14] * w;
					a3 += ip[15] * w;

					ip += 4 * channels;
					mp += 4;
				}

				ipe += 4 * channels;
				while (ip < ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;
					a3 += ip[3] * w;

					ip += channels;
					mp++;
				}

				tp[0] = UnFix15(a0);
				tp[1] = UnFix15(a1);
				tp[2] = UnFix15(a2);
				tp[3] = UnFix15(a3);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			ushort* op = (ushort*)ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0, a1 = 0, a2 = 0, a3 = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 2 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;
					a3 += tp[3] * w;

					w = mp[1];
					a0 += tp[4] * w;
					a1 += tp[5] * w;
					a2 += tp[6] * w;
					a3 += tp[7] * w;

					tp += 2 * channels;
					mp += 2;
				}

				tpe += 2 * channels;
				while (tp < tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;
					a3 += tp[3] * w;

					tp += channels;
					mp++;
				}

				op[0] = UnFixToUQ15(a0);
				op[1] = UnFixToUQ15(a1);
				op[2] = UnFixToUQ15(a2);
				op[3] = UnFixToUQ15(a3);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			fixed (ushort* igtstart = &LookupTables.SrgbInverseGammaUQ15[0])
			{
				int iamt = Fix15(amt);
				int threshold = RoundF(thresh * byte.MaxValue);

				byte* gt = gtstart;
				ushort* ip = (ushort*)cstart + ox * channels, yp = (ushort*)ystart + ox, bp = (ushort*)bstart, op = (ushort*)ostart, igt = igtstart;

				for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
				{
					int dif = *yp++ - *bp++;

					ushort c0 = ip[0], c1 = ip[1], c2 = ip[2], c3 = ip[3];
					if (threshold == 0 || Math.Abs(dif) > threshold)
					{
						c0 = gt[(nuint)ClampToUQ15One((uint)c0)];
						c1 = gt[(nuint)ClampToUQ15One((uint)c1)];
						c2 = gt[(nuint)ClampToUQ15One((uint)c2)];

						dif = UnFix15(dif * iamt);
						op[0] = igt[(nuint)ClampToByte(c0 + dif)];
						op[1] = igt[(nuint)ClampToByte(c1 + dif)];
						op[2] = igt[(nuint)ClampToByte(c2 + dif)];
						op[3] = c3;
					}
					else
					{
						op[0] = c0;
						op[1] = c1;
						op[2] = c2;
						op[3] = c3;
					}
				}
			}
		}

		public override string ToString() => nameof(Convolver4ChanUQ15);
	}

	internal sealed class ConvolverBgrByte : IConvolver
	{
		private const int channels = 3;

		public static readonly ConvolverBgrByte Instance = new ConvolverBgrByte();

		private ConvolverBgrByte() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0, a1 = 0, a2 = 0;

				nuint ix = *pmapx++;
				byte* ip = istart + ix * channels;
				byte* ipe = ip + smapx * channels - 5 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;

					w = mp[1];
					a0 += ip[3] * w;
					a1 += ip[4] * w;
					a2 += ip[5] * w;

					w = mp[2];
					a0 += ip[6] * w;
					a1 += ip[7] * w;
					a2 += ip[8] * w;

					w = mp[3];
					a0 += ip[9] * w;
					a1 += ip[10] * w;
					a2 += ip[11] * w;

					w = mp[4];
					a0 += ip[12] * w;
					a1 += ip[13] * w;
					a2 += ip[14] * w;

					ip += 5 * channels;
					mp += 5;
				}

				ipe += 5 * channels;
				while (ip < ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;

					ip += channels;
					mp++;
				}

				tp[0] = UnFix8(a0);
				tp[1] = UnFix8(a1);
				tp[2] = UnFix8(a2);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			byte* op = ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0, a1 = 0, a2 = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 2 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;

					w = mp[1];
					a0 += tp[3] * w;
					a1 += tp[4] * w;
					a2 += tp[5] * w;

					tp += 2 * channels;
					mp += 2;
				}

				tpe += 2 * channels;
				while (tp < tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;

					tp += channels;
					mp++;
				}

				op[0] = UnFix22ToByte(a0);
				op[1] = UnFix22ToByte(a1);
				op[2] = UnFix22ToByte(a2);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			int iamt = Fix15(amt);
			int threshold = RoundF(thresh * byte.MaxValue);

			byte* ip = cstart + ox * channels, yp = ystart + ox, bp = bstart, op = ostart;

			for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
			{
				int dif = *yp++ - *bp++;

				byte c0 = ip[0], c1 = ip[1], c2 = ip[2];
				if (threshold == 0 || Math.Abs(dif) > threshold)
				{
					dif = UnFix15(dif * iamt);
					op[0] = ClampToByte(c0 + dif);
					op[1] = ClampToByte(c1 + dif);
					op[2] = ClampToByte(c2 + dif);
				}
				else
				{
					op[0] = c0;
					op[1] = c1;
					op[2] = c2;
				}
			}
		}

		public override string ToString() => nameof(ConvolverBgrByte);
	}

	internal sealed class ConvolverBgrUQ15 : IConvolver
	{
		private const int channels = 3;

		public static readonly ConvolverBgrUQ15 Instance = new ConvolverBgrUQ15();

		private ConvolverBgrUQ15() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0, a1 = 0, a2 = 0;

				nuint ix = *pmapx++;
				ushort* ip = (ushort*)istart + ix * channels;
				ushort* ipe = ip + smapx * channels - 5 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;

					w = mp[1];
					a0 += ip[3] * w;
					a1 += ip[4] * w;
					a2 += ip[5] * w;

					w = mp[2];
					a0 += ip[6] * w;
					a1 += ip[7] * w;
					a2 += ip[8] * w;

					w = mp[3];
					a0 += ip[9] * w;
					a1 += ip[10] * w;
					a2 += ip[11] * w;

					w = mp[4];
					a0 += ip[12] * w;
					a1 += ip[13] * w;
					a2 += ip[14] * w;

					ip += 5 * channels;
					mp += 5;
				}

				ipe += 5 * channels;
				while (ip < ipe)
				{
					int w = mp[0];
					a0 += ip[0] * w;
					a1 += ip[1] * w;
					a2 += ip[2] * w;

					ip += channels;
					mp++;
				}

				tp[0] = UnFix15(a0);
				tp[1] = UnFix15(a1);
				tp[2] = UnFix15(a2);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			ushort* op = (ushort*)ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0, a1 = 0, a2 = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 2 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;

					w = mp[1];
					a0 += tp[3] * w;
					a1 += tp[4] * w;
					a2 += tp[5] * w;

					tp += 2 * channels;
					mp += 2;
				}

				tpe += 2 * channels;
				while (tp < tpe)
				{
					int w = mp[0];
					a0 += tp[0] * w;
					a1 += tp[1] * w;
					a2 += tp[2] * w;

					tp += channels;
					mp++;
				}

				op[0] = UnFixToUQ15(a0);
				op[1] = UnFixToUQ15(a1);
				op[2] = UnFixToUQ15(a2);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			fixed (ushort* igtstart = &LookupTables.SrgbInverseGammaUQ15[0])
			{
				int iamt = Fix15(amt);
				int threshold = RoundF(thresh * byte.MaxValue);

				byte* gt = gtstart;
				ushort* ip = (ushort*)cstart + ox * channels, yp = (ushort*)ystart + ox, bp = (ushort*)bstart, op = (ushort*)ostart, igt = igtstart;

				for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
				{
					int dif = *yp++ - *bp++;

					ushort c0 = ip[0], c1 = ip[1], c2 = ip[2];
					if (threshold == 0 || Math.Abs(dif) > threshold)
					{
						c0 = gt[(nuint)ClampToUQ15One((uint)c0)];
						c1 = gt[(nuint)ClampToUQ15One((uint)c1)];
						c2 = gt[(nuint)ClampToUQ15One((uint)c2)];

						dif = UnFix15(dif * iamt);
						op[0] = igt[(nuint)ClampToByte(c0 + dif)];
						op[1] = igt[(nuint)ClampToByte(c1 + dif)];
						op[2] = igt[(nuint)ClampToByte(c2 + dif)];
					}
					else
					{
						op[0] = c0;
						op[1] = c1;
						op[2] = c2;
					}
				}
			}
		}

		public override string ToString() => nameof(ConvolverBgrUQ15);
	}

	internal sealed class Convolver1ChanByte : IConvolver
	{
		private const int channels = 1;

		public static readonly Convolver1ChanByte Instance = new Convolver1ChanByte();

		private Convolver1ChanByte() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0;

				nuint ix = *pmapx++;
				byte* ip = istart + ix * channels;
				byte* ipe = ip + smapx * channels - 8 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					a0 += ip[0] * mp[0];
					a0 += ip[1] * mp[1];
					a0 += ip[2] * mp[2];
					a0 += ip[3] * mp[3];
					a0 += ip[4] * mp[4];
					a0 += ip[5] * mp[5];
					a0 += ip[6] * mp[6];
					a0 += ip[7] * mp[7];
					ip += 8 * channels;
					mp += 8;
				}

				ipe += 8 * channels;
				while (ip < ipe)
				{
					a0 += ip[0] * mp[0];

					ip += channels;
					mp++;
				}

				tp[0] = UnFix8(a0);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			byte* op = ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 4 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					a0 += tp[0] * mp[0];
					a0 += tp[1] * mp[1];
					a0 += tp[2] * mp[2];
					a0 += tp[3] * mp[3];
					tp += 4 * channels;
					mp += 4;
				}

				tpe += 4 * channels;
				while (tp < tpe)
				{
					a0 += tp[0] * mp[0];

					tp += channels;
					mp++;
				}

				op[0] = UnFix22ToByte(a0);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			int iamt = Fix15(amt);
			int threshold = RoundF(thresh * byte.MaxValue);

			byte* ip = cstart + ox * channels, yp = ystart + ox, bp = bstart, op = ostart;

			for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
			{
				int dif = *yp++ - *bp++;

				byte c0 = ip[0];
				if (threshold == 0 || Math.Abs(dif) > threshold)
				{
					dif = UnFix15(dif * iamt);
					op[0] = ClampToByte(c0 + dif);
				}
				else
				{
					op[0] = c0;
				}
			}
		}

		public override string ToString() => nameof(Convolver1ChanByte);
	}

	internal sealed class Convolver1ChanUQ15 : IConvolver
	{
		private const int channels = 1;

		public static readonly Convolver1ChanUQ15 Instance = new Convolver1ChanUQ15();

		private Convolver1ChanUQ15() { }

		int IConvolver.Channels => channels;
		int IConvolver.MapChannels => 1;

		unsafe void IConvolver.ConvolveSourceLine(byte* istart, byte* tstart, int cb, byte* mapxstart, int smapx, int smapy)
		{
			int* tp = (int*)tstart, tpe = (int*)(tstart + cb);
			uint* pmapx = (uint*)mapxstart;
			nuint tstride = (nuint)smapy * channels;

			while (tp < tpe)
			{
				int a0 = 0;

				nuint ix = *pmapx++;
				ushort* ip = (ushort*)istart + ix * channels;
				ushort* ipe = ip + smapx * channels - 8 * channels;
				int* mp = (int*)pmapx;
				pmapx += smapx;

				while (ip <= ipe)
				{
					a0 += ip[0] * mp[0];
					a0 += ip[1] * mp[1];
					a0 += ip[2] * mp[2];
					a0 += ip[3] * mp[3];
					a0 += ip[4] * mp[4];
					a0 += ip[5] * mp[5];
					a0 += ip[6] * mp[6];
					a0 += ip[7] * mp[7];
					ip += 8 * channels;
					mp += 8;
				}

				ipe += 8 * channels;
				while (ip < ipe)
				{
					a0 += ip[0] * mp[0];

					ip += channels;
					mp++;
				}

				tp[0] = UnFix15(a0);
				tp += tstride;
			}
		}

		unsafe void IConvolver.WriteDestLine(byte* tstart, byte* ostart, int ox, int ow, byte* pmapy, int smapy)
		{
			ushort* op = (ushort*)ostart;
			nuint tstride = (nuint)smapy * channels, nox = (nuint)ox;

			for (nuint xc = nox + (nuint)ow; nox < xc; nox++)
			{
				int a0 = 0;

				int* tp = (int*)tstart + nox * tstride;
				int* tpe = tp + tstride - 4 * channels;
				int* mp = (int*)pmapy;

				while (tp <= tpe)
				{
					a0 += tp[0] * mp[0];
					a0 += tp[1] * mp[1];
					a0 += tp[2] * mp[2];
					a0 += tp[3] * mp[3];
					tp += 4 * channels;
					mp += 4;
				}

				tpe += 4 * channels;
				while (tp < tpe)
				{
					a0 += tp[0] * mp[0];

					tp += channels;
					mp++;
				}

				op[0] = UnFixToUQ15(a0);
				op += channels;
			}
		}

		unsafe void IConvolver.SharpenLine(byte* cstart, byte* ystart, byte* bstart, byte* ostart, int ox, int ow, float amt, float thresh, bool gamma)
		{
			fixed (byte* gtstart = &LookupTables.SrgbGammaUQ15[0])
			fixed (ushort* igtstart = &LookupTables.SrgbInverseGammaUQ15[0])
			{
				int iamt = Fix15(amt);
				int threshold = RoundF(thresh * byte.MaxValue);

				byte* gt = gtstart;
				ushort* ip = (ushort*)cstart + ox * channels, yp = (ushort*)ystart + ox, bp = (ushort*)bstart, op = (ushort*)ostart, igt = igtstart;

				for (int xc = ox + ow; ox < xc; ox++, ip += channels, op += channels)
				{
					int dif = *yp++ - *bp++;

					ushort c0 = ip[0];
					if (threshold == 0 || Math.Abs(dif) > threshold)
					{
						c0 = gt[(nuint)ClampToUQ15One((uint)c0)];

						dif = UnFix15(dif * iamt);
						op[0] = igt[(nuint)ClampToByte(c0 + dif)];
					}
					else
					{
						op[0] = c0;
					}
				}
			}
		}

		public override string ToString() => nameof(Convolver1ChanUQ15);
	}
}
