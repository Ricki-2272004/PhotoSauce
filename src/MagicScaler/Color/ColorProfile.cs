// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Blake2Fast;
using PhotoSauce.MagicScaler.Converters;

using static PhotoSauce.MagicScaler.MathUtil;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PhotoSauce.MagicScaler;

internal enum ColorProfileType { Unknown, Curve, Matrix, Table }

//http://www.color.org/specification/ICC1v43_2010-12.pdf
internal class ColorProfile
{
	// This value accounts for 132 bytes of header, plus a single tag (20 bytes overhead)
	// with data length 8. Such a profile would not be useful, but it could be parsed.
	public const int MinProfileLength = 160;

	internal static class Cache
	{
		private static readonly ConcurrentDictionary<Guid, WeakReference<ColorProfile>> dic = new();

		private static ColorProfile addOrUpdate(Guid guid, ReadOnlySpan<byte> bytes)
		{
			var prof = Parse(bytes);
			dic.AddOrUpdate(guid, (g) => new WeakReference<ColorProfile>(prof), (g, r) => { r.SetTarget(prof); return r; });

			return prof;
		}

		public static ColorProfile GetOrAdd(ReadOnlySpan<byte> bytes)
		{
			var hash = (Span<byte>)stackalloc byte[Unsafe.SizeOf<Guid>()];
			Blake2b.ComputeAndWriteHash(Unsafe.SizeOf<Guid>(), bytes, hash);

			var guid = MemoryMarshal.Read<Guid>(hash);

			return (dic.TryGetValue(guid, out var wref) && wref.TryGetTarget(out var prof)) ? prof : addOrUpdate(guid, bytes);
		}
	}

	private static class IccStrings
	{
		public const uint acsp = 'a' << 24 | 'c' << 16 | 's' << 8 | 'p';
		public const uint RGB  = 'R' << 24 | 'G' << 16 | 'B' << 8 | ' ';
		public const uint GRAY = 'G' << 24 | 'R' << 16 | 'A' << 8 | 'Y';
		public const uint CMYK = 'C' << 24 | 'M' << 16 | 'Y' << 8 | 'K';
		public const uint XYZ  = 'X' << 24 | 'Y' << 16 | 'Z' << 8 | ' ';
		public const uint Lab  = 'L' << 24 | 'a' << 16 | 'b' << 8 | ' ';
	}

	private static class IccTags
	{
		public const uint bXYZ = 'b' << 24 | 'X' << 16 | 'Y' << 8 | 'Z';
		public const uint gXYZ = 'g' << 24 | 'X' << 16 | 'Y' << 8 | 'Z';
		public const uint rXYZ = 'r' << 24 | 'X' << 16 | 'Y' << 8 | 'Z';
		public const uint bTRC = 'b' << 24 | 'T' << 16 | 'R' << 8 | 'C';
		public const uint gTRC = 'g' << 24 | 'T' << 16 | 'R' << 8 | 'C';
		public const uint rTRC = 'r' << 24 | 'T' << 16 | 'R' << 8 | 'C';
		public const uint kTRC = 'k' << 24 | 'T' << 16 | 'R' << 8 | 'C';
		public const uint A2B0 = 'A' << 24 | '2' << 16 | 'B' << 8 | '0';
		public const uint B2A0 = 'B' << 24 | '2' << 16 | 'A' << 8 | '0';
	}

	private static class IccTypes
	{
		public const uint XYZ  = 'X' << 24 | 'Y' << 16 | 'Z' << 8 | ' ';
		public const uint curv = 'c' << 24 | 'u' << 16 | 'r' << 8 | 'v';
		public const uint para = 'p' << 24 | 'a' << 16 | 'r' << 8 | 'a';
	}

	private readonly record struct TagEntry(uint Tag, Range Range);

	internal enum ProfileColorSpace
	{
		Other,
		Rgb,
		Grey,
		Cmyk,
		Xyz,
		Lab
	}

	private static readonly ColorProfile invalidProfile = new();

	private static readonly Lazy<MatrixProfile> srgb = new(() => {
		var m = new Matrix3x3C(
			0.43602939, 0.22243797, 0.01389754,
			0.38510027, 0.71694100, 0.09707674,
			0.14307328, 0.06062103, 0.71393112
		);
		Matrix3x3C.Invert(m, out var im);
		var curve = new ProfileCurve(LookupTables.SrgbGamma, LookupTables.SrgbInverseGamma);

		return new MatrixProfile(IccProfiles.sRgbV4.Value, IccProfiles.sRgbCompact.Value, m, im, curve, ProfileColorSpace.Rgb, ProfileColorSpace.Xyz);
	});

	private static readonly Lazy<CurveProfile> sgrey = new(() =>
		new CurveProfile(IccProfiles.sGreyV4.Value, IccProfiles.sGreyCompact.Value, sRGB.Curve, ProfileColorSpace.Grey, ProfileColorSpace.Xyz)
	);

	private static readonly Lazy<MatrixProfile> adobeRgb = new(() => {
		var m = new Matrix3x3C(
			0.60974189, 0.31111293, 0.01946551,
			0.20527343, 0.62567449, 0.06087462,
			0.14918756, 0.06321258, 0.74456527
		);
		Matrix3x3C.Invert(m, out var im);
		var curve = curveFromPower(2.2);

		return new MatrixProfile(IccProfiles.AdobeRgbV4.Value, IccProfiles.AdobeRgbCompact.Value, m, im, curve, ProfileColorSpace.Rgb, ProfileColorSpace.Xyz);
	});

	private static readonly Lazy<MatrixProfile> displayP3 = new(() => {
		var m = new Matrix3x3C(
			0.51511960, 0.24118953, -0.00105045,
			0.29197886, 0.69224341,  0.04187909,
			0.15710442, 0.06656706,  0.78407676
		);
		Matrix3x3C.Invert(m, out var im);

		return new MatrixProfile(IccProfiles.DisplayP3V4.Value, IccProfiles.DisplayP3Compact.Value, m, im, sRGB.Curve, ProfileColorSpace.Rgb, ProfileColorSpace.Xyz);
	});

	private static ProfileCurve curveFromPower(double gamma)
	{
		var igt = new float[LookupTables.InverseGammaLength];
		for (int i = 0; i < igt.Length; i++)
			igt[i] = (float)Math.Pow((double)i / LookupTables.InverseGammaScale, gamma);

		gamma = 1d / gamma;

		var gt = new float[LookupTables.GammaLengthFloat];
		for (int i = 0; i < gt.Length; i++)
			gt[i] = (float)Math.Pow((double)i / LookupTables.GammaScaleFloat, gamma);

		LookupTables.Fixup(gt, LookupTables.GammaScaleFloat);
		LookupTables.Fixup(igt, LookupTables.InverseGammaScale);

		return new ProfileCurve(gt, igt);
	}

	private static ProfileCurve curveFromPoints(ReadOnlySpan<ushort> points, bool inverse)
	{
		using var buff = BufferPool.RentLocal<double>(points.Length);
		var curve = buff.Span;

		for (int i = 0; i < curve.Length; i++)
			curve[i] = (double)points[i] / ushort.MaxValue;

		double cscal = curve.Length - 1;
		var igt = new float[LookupTables.InverseGammaLength];
		for (int i = 0; i <= LookupTables.InverseGammaScale; i++)
		{
			double val = (double)i / LookupTables.InverseGammaScale;
			double pos = val * cscal;

			int idx = Math.Min((int)pos, curve.Length - 2);
			igt[i] = (float)Lerp(curve[idx], curve[idx + 1], pos - idx);
		}

		if (lutInvertsTo(igt, LookupTables.SrgbGamma))
			return sRGB.Curve;

		if (inverse)
			curve.Reverse();

		var gt = new float[LookupTables.GammaLengthFloat];
		for (int i = 0; i <= LookupTables.GammaScaleFloat; i++)
		{
			double val = (double)i / LookupTables.GammaScaleFloat;
			int idx = curve.BinarySearch(val);

			double pos;
			if (idx >= 0)
				pos = idx;
			else
			{
				idx = ~idx;
				if (idx == 0)
					pos = 0;
				else if (idx == curve.Length)
					pos = curve.Length - 1;
				else
				{
					double vh = curve[idx];
					double vl = curve[idx - 1];
					if (vl == vh)
						pos = idx;
					else
						pos = idx - 1 + (val - vl) / (vh - vl);
				}
			}

			gt[i] = (float)(pos / (curve.Length - 1));
		}

		if (inverse)
			Array.Reverse(gt, 0, LookupTables.GammaScaleFloat + 1);

		LookupTables.Fixup(gt, LookupTables.GammaScaleFloat);
		LookupTables.Fixup(igt, LookupTables.InverseGammaScale);

		return new ProfileCurve(gt, igt);
	}

	private static ProfileCurve curveFromParameters(double a, double b, double c, double d, double e, double f, double g)
	{
		if (
			g.IsRoughlyEqualTo(2.4) &&
			d.IsRoughlyEqualTo(0.04045) &&
			a.IsRoughlyEqualTo(1.000/1.055) &&
			b.IsRoughlyEqualTo(0.055/1.055) &&
			c.IsRoughlyEqualTo(1.000/12.92)
		) return sRGB.Curve;

		var igt = new float[LookupTables.InverseGammaLength];
		for (int i = 0; i < igt.Length; i++)
		{
			double val = (double)i / LookupTables.InverseGammaScale;
			if (val >= d)
				val = Math.Pow(val * a + b, g) + c + e;
			else
				val = val * c + f;

			igt[i] = (float)val;
		}

		g = 1d / g;

		var gt = new float[LookupTables.GammaLengthFloat];
		for (int i = 0; i < gt.Length; i++)
		{
			double val = (double)i / LookupTables.GammaScaleFloat;
			if (val > (c * d + f))
				val = (Math.Pow(val - c - e, g) - b) / a;
			else
				val = c == 0f ? 0f : ((val - f) / c);

			gt[i] = (float)val;
		}

		LookupTables.Fixup(gt, LookupTables.GammaScaleFloat);
		LookupTables.Fixup(igt, LookupTables.InverseGammaScale);

		return new ProfileCurve(gt, igt);
	}

	private static bool lutInvertsTo(float[] igt, float[] gt)
	{
		for (int i = 0; i <= LookupTables.InverseGammaScale; i++)
		{
			float pos = igt[i] * LookupTables.GammaScaleFloat;
			int idx = (int)pos;

			if (i != FixToByte(Lerp(gt[idx], gt[idx + 1], pos - idx)))
				return false;
		}

		return true;
	}

	private static bool tryGetTagEntry(ReadOnlySpan<TagEntry> entries, uint tag, out TagEntry entry)
	{
		for (int i = 0; i < entries.Length; i++)
		{
			var cand = entries[i];
			if (cand.Tag == tag)
			{
				entry = cand;
				return true;
			}
		}

		entry = default;
		return false;
	}

	private static bool tryGetMatrix(ReadOnlySpan<byte> bXYZ, ReadOnlySpan<byte> gXYZ, ReadOnlySpan<byte> rXYZ, out Matrix3x3C matrix)
	{
		matrix = default;

		var hdr = bXYZ[..8];
		uint tag = ReadUInt32BigEndian(hdr);
		if (tag is not IccTypes.XYZ || !hdr.SequenceEqual(gXYZ[..8]) || !hdr.SequenceEqual(rXYZ[..8]))
			return false;

		int bx = ReadInt32BigEndian(bXYZ[8..]);
		int gx = ReadInt32BigEndian(gXYZ[8..]);
		int rx = ReadInt32BigEndian(rXYZ[8..]);

		int by = ReadInt32BigEndian(bXYZ[12..]);
		int gy = ReadInt32BigEndian(gXYZ[12..]);
		int ry = ReadInt32BigEndian(rXYZ[12..]);

		int bz = ReadInt32BigEndian(bXYZ[16..]);
		int gz = ReadInt32BigEndian(gXYZ[16..]);
		int rz = ReadInt32BigEndian(rXYZ[16..]);

		double div = 65536;
		matrix = new(
			rx / div, ry / div, rz / div,
			gx / div, gy / div, gz / div,
			bx / div, by / div, bz / div
		);

		return true;
	}

	private static bool tryGetCurve(ReadOnlySpan<byte> trc, out ProfileCurve? curve)
	{
		// true return with null curve indicates linear curve
		curve = default;

		uint tag = ReadUInt32BigEndian(trc);
		if (trc.Length < 12 || tag is not (IccTypes.curv or IccTypes.para))
			return false;

		if (tag is IccTypes.curv)
		{
			uint pcnt = ReadUInt32BigEndian(trc[8..]);
			if (trc.Length < (12 + pcnt * sizeof(ushort)))
				return false;

			if (pcnt == 0)
				return true;

			if (pcnt == 1)
			{
				ushort gi = ReadUInt16BigEndian(trc[12..]);
				double gd;
				switch (gi)
				{
					case 0:
						return false;
					case 0x100:
						return true;
					case 0x1cd:
						gd = 1.8;
						break;
					case 0x233:
						curve = AdobeRgb.Curve;
						return true;
					default:
						gd = gi / 256d;
						break;
				}

				curve = curveFromPower(gd);
			}
			else
			{
				if (pcnt == 2 && ReadUInt16BigEndian(trc[12..]) == ushort.MinValue && ReadUInt16BigEndian(trc[14..]) == ushort.MaxValue)
					return true;

				using var buff = BufferPool.RentLocal<ushort>((int)pcnt);
				var points = buff.Span;

				ushort pp = 0;
				bool inc = true, dec = true;
				for (int i = 0; i < points.Length; i++)
				{
					ushort p = ReadUInt16BigEndian(trc.Slice(12 + i * sizeof(ushort)));

					if (i > 0 && p < pp)
						inc = false;
					if (i > 0 && p > pp)
						dec = false;
					if (!inc && !dec)
						return false;

					points[i] = pp = p;
				}

				curve = curveFromPoints(points, dec);
			}
		}
		else // (tag is IccTypes.para)
		{
			ushort func = ReadUInt16BigEndian(trc[8..]);
			int minLen = func switch {
				0 => sizeof(int) * 1,
				1 => sizeof(int) * 3,
				2 => sizeof(int) * 4,
				3 => sizeof(int) * 5,
				4 => sizeof(int) * 7,
				_ => int.MaxValue
			};

			var param = trc[12..];
			if (param.Length < minLen)
				return false;


			int a = 0, b = 0, c = 0, d = 0, e = 0, f = 0, g = 0;
			switch (func)
			{
				case 0:
					g = ReadInt32BigEndian(param);
					break;
				case 1:
					a = ReadInt32BigEndian(param[4..]);
					b = ReadInt32BigEndian(param[8..]);
					goto case 0;
				case 2:
					c = ReadInt32BigEndian(param[12..]);
					goto case 1;
				case 3:
					d = ReadInt32BigEndian(param[16..]);
					goto case 2;
				case 4:
					e = ReadInt32BigEndian(param[20..]);
					f = ReadInt32BigEndian(param[24..]);
					goto case 3;
			}

			// prevent divide by 0 and some uninvertible curves.
			if (
				(g == 0) ||
				(a == 0 && func > 0) ||
				((uint)c > 0x10000u && func >= 2) ||
				((uint)d > 0x10000u && func >= 3) ||
				((uint)e > 0x10000u && func == 4)
			) return false;

			double div = 1 / 65536d;
			double da = a * div, db = b * div, dc = c * div, dd = d * div, de = e * div, df = f * div, dg = g * div;
			switch (func)
			{
				case 0:
					curve = g switch {
						0x10000            => default,
						0x1cd00 or 0x1cccd => curveFromPower(1.8),
						0x23300 or 0x23333 => AdobeRgb.Curve,
						_                  => curveFromPower(dg)
					};
					return true;
				case 1:
					dd = -db / da;
					break;
				case 2:
					df = de = dc;
					dc = 0d;
					goto case 1;
				case 3:
				case 4:
					de -= dc;
					break;
			}

			curve = curveFromParameters(da, db, dc, dd, de, df, dg);
		}

		return true;
	}

	public static ColorProfile Parse(ReadOnlySpan<byte> prof)
	{
		const int headerLength = 128;
		const int headerPlusTagCountLength = headerLength + sizeof(uint);

		if (prof.Length < MinProfileLength)
			return invalidProfile;

		uint len = ReadUInt32BigEndian(prof);
		if (len != prof.Length)
			return invalidProfile;

		uint acsp = ReadUInt32BigEndian(prof[36..]);
		var ver = prof.Slice(8, 4);
		if (acsp is not IccStrings.acsp || ver[0] is not (2 or 4))
			return invalidProfile;

		var dataColorSpace = ReadUInt32BigEndian(prof[16..]) switch {
			IccStrings.RGB  => ProfileColorSpace.Rgb,
			IccStrings.GRAY => ProfileColorSpace.Grey,
			IccStrings.CMYK => ProfileColorSpace.Cmyk,
			_               => ProfileColorSpace.Other
		};

		var pcsColorSpace = ReadUInt32BigEndian(prof[20..]) switch {
			IccStrings.XYZ => ProfileColorSpace.Xyz,
			IccStrings.Lab => ProfileColorSpace.Lab,
			_              => ProfileColorSpace.Other
		};

		if (pcsColorSpace is not ProfileColorSpace.Xyz || (dataColorSpace is not (ProfileColorSpace.Rgb or ProfileColorSpace.Grey)))
			return new ColorProfile(prof.ToArray(), dataColorSpace, pcsColorSpace, ColorProfileType.Unknown);

		uint tagCount = ReadUInt32BigEndian(prof[headerLength..]);
		if (len < (headerPlusTagCountLength + tagCount * Unsafe.SizeOf<TagEntry>()))
			return invalidProfile;

		using var tagBuffer = BufferPool.RentLocal<TagEntry>((int)tagCount);
		var tagEntries = tagBuffer.Span;
		for (int i = 0; i < tagEntries.Length; i++)
		{
			int entryStart = headerPlusTagCountLength + i * Unsafe.SizeOf<TagEntry>();
			var entry = prof[entryStart..];

			uint tag = ReadUInt32BigEndian(entry);
			uint pos = ReadUInt32BigEndian(entry[4..]);
			uint cb = ReadUInt32BigEndian(entry[8..]);

			uint end = pos + cb;
			if (cb < 8 || len < end)
				return invalidProfile;

			// not handling these yet, so we'll hand off to WCS
			if (tag is IccTags.A2B0 or IccTags.B2A0)
				return new ColorProfile(prof.ToArray(), dataColorSpace, pcsColorSpace, ColorProfileType.Table);

			tagEntries[i] = new(tag, (int)pos..(int)end);
		}

		if (dataColorSpace == ProfileColorSpace.Grey
			&& tryGetTagEntry(tagEntries, IccTags.kTRC, out var kTRC)
			&& tryGetCurve(prof[kTRC.Range], out var curve)
		) return new CurveProfile(prof.ToArray(), null, curve, dataColorSpace, pcsColorSpace);

		if (dataColorSpace == ProfileColorSpace.Rgb
			&& tryGetTagEntry(tagEntries, IccTags.bTRC, out var bTRC)
			&& tryGetTagEntry(tagEntries, IccTags.gTRC, out var gTRC)
			&& tryGetTagEntry(tagEntries, IccTags.rTRC, out var rTRC)
			&& tryGetTagEntry(tagEntries, IccTags.bXYZ, out var bXYZ)
			&& tryGetTagEntry(tagEntries, IccTags.gXYZ, out var gXYZ)
			&& tryGetTagEntry(tagEntries, IccTags.rXYZ, out var rXYZ)
		)
		{
			var bTRCData = prof[bTRC.Range];
			var gTRCData = prof[gTRC.Range];
			var rTRCData = prof[rTRC.Range];

			if (bTRCData.SequenceEqual(gTRCData) && bTRCData.SequenceEqual(rTRCData)
				&& tryGetCurve(bTRCData, out var rgbcurve)
				&& tryGetMatrix(prof[bXYZ.Range], prof[gXYZ.Range], prof[rXYZ.Range], out var matrix)
			)
			{
				if (Matrix3x3C.Invert(matrix, out var imatrix))
					return new MatrixProfile(prof.ToArray(), null, matrix, imatrix, rgbcurve, dataColorSpace, pcsColorSpace);
			}
		}

		return invalidProfile;
	}

	public static CurveProfile sGrey => sgrey.Value;
	public static MatrixProfile sRGB => srgb.Value;
	public static MatrixProfile AdobeRgb => adobeRgb.Value;
	public static MatrixProfile DisplayP3 => displayP3.Value;
	public static ColorProfile CmykDefault => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
		? WicColorProfile.Cmyk.Value.ParsedProfile
		: throw new PlatformNotSupportedException("CMYK conversion is not yet supported on this platform.");

	public static ColorProfile GetDefaultFor(PixelFormat fmt) => fmt.ColorRepresentation switch {
		PixelColorRepresentation.Cmyk => CmykDefault,
		PixelColorRepresentation.Grey => sGrey,
		_                             => sRGB
	};

	public static ColorProfile GetSourceProfile(ColorProfile prof, ColorProfileMode mode)
	{
		if (mode == ColorProfileMode.Preserve)
			return prof;

		if (prof.ProfileType == ColorProfileType.Curve && prof is CurveProfile cp && cp.Curve == sGrey.Curve)
			return sGrey;

		if (prof is MatrixProfile mp)
		{
			if (mp.Curve == sRGB.Curve && mp.Matrix.IsRouglyEqualTo(sRGB.Matrix))
				return sRGB;

			if (mp.Curve == DisplayP3.Curve && mp.Matrix.IsRouglyEqualTo(DisplayP3.Matrix))
				return DisplayP3;

			if (mp.Curve == AdobeRgb.Curve && mp.Matrix.IsRouglyEqualTo(AdobeRgb.Matrix))
				return AdobeRgb;
		}

		return prof;
	}

	public static ColorProfile GetDestProfile(ColorProfile prof, ColorProfileMode mode)
	{
		if (mode == ColorProfileMode.Preserve)
			return prof;

		if (mode <= ColorProfileMode.NormalizeAndEmbed)
		{
			if (prof == AdobeRgb || prof.DataColorSpace == ProfileColorSpace.Cmyk)
				return AdobeRgb;

			if (prof.ProfileType == ColorProfileType.Curve)
				return sGrey;

			if (prof is not MatrixProfile mp || isWideGamut(mp.Matrix))
				return DisplayP3;
		}

		return prof.ProfileType == ColorProfileType.Curve ? sGrey : sRGB;

		// check for red or green x,y coordinates outside sRGB gamut
		static bool isWideGamut(Matrix3x3C m) =>
			m.M11 / (m.M11 + m.M12 + m.M13) > 0.67 ||
			m.M22 / (m.M21 + m.M22 + m.M23) > 0.62;
	}

	public bool IsValid { get; }
	public byte[] ProfileBytes { get; }
	public ProfileColorSpace DataColorSpace { get; }
	public ProfileColorSpace PcsColorSpace { get; }

	public ColorProfileType ProfileType { get; protected set; }

	public bool IsCompatibleWith(PixelFormat fmt) => (DataColorSpace, fmt.ColorRepresentation) is
		(ProfileColorSpace.Rgb, PixelColorRepresentation.Bgr) or (ProfileColorSpace.Rgb, PixelColorRepresentation.Rgb) or
		(ProfileColorSpace.Cmyk, PixelColorRepresentation.Cmyk) or (ProfileColorSpace.Grey, PixelColorRepresentation.Grey);

	private ColorProfile() => ProfileBytes = Array.Empty<byte>();

	protected ColorProfile(byte[] bytes, ProfileColorSpace dataSpace, ProfileColorSpace pcsSpace, ColorProfileType profileType)
	{
		IsValid = true;
		ProfileBytes = bytes;
		DataColorSpace = dataSpace;
		PcsColorSpace = pcsSpace;
		ProfileType = profileType;
	}

	public class ProfileCurve
	{
		public float[] Gamma { get; }
		public float[] InverseGamma { get; }

		public ProfileCurve(float[] gamma, float[] inverseGamma)
		{
			Gamma = gamma;
			InverseGamma = inverseGamma;
		}
	}
}

internal class CurveProfile : ColorProfile
{
	private readonly ConcurrentDictionary<(Type tfrom, Type tto, Type tenc, Type trng, CurveProfile profile), IConverter> converterCache = new();

	public bool IsLinear { get; }
	public ProfileCurve Curve { get; }
	public byte[]? CompactProfile { get; }

	public CurveProfile(byte[] bytes, byte[]? compact, ProfileCurve? curve, ProfileColorSpace dataSpace, ProfileColorSpace pcsSpace) : base(bytes, dataSpace, pcsSpace, ColorProfileType.Curve)
	{
		IsLinear = curve is null;
		Curve = curve ?? new ProfileCurve(null!, LookupTables.Alpha);
		CompactProfile = compact;
	}

	private IConverter addConverter(in (Type, Type, Type, Type, CurveProfile) cacheKey) => converterCache.GetOrAdd(cacheKey, static key => {
		if (key.profile.IsLinear)
		{
			if (key.tfrom == typeof(float) && key.tto == typeof(float))
				return NoopConverter.Instance;
			if (key.tfrom == typeof(byte) && key.tto == typeof(float))
				return key.trng == typeof(EncodingRange.Video) ? FloatConverter.Widening.InstanceVideoRange : FloatConverter.Widening.InstanceFullRange;
			if (key.tfrom == typeof(float) && key.tto == typeof(byte))
				return key.trng == typeof(EncodingRange.Video) ? FloatConverter.Narrowing.InstanceVideoRange : FloatConverter.Narrowing.InstanceFullRange;
			if (key.tfrom == typeof(ushort) && key.tto == typeof(byte))
				return key.trng == typeof(EncodingRange.Video) ? UQ15Converter<EncodingRange.Video>.Instance : UQ15Converter<EncodingRange.Full>.Instance;
		}

		if (key.tenc == typeof(EncodingType.Linear))
		{
			var gt = key.profile.Curve.Gamma;
			if (key.tfrom == typeof(float) && key.tto == typeof(float))
				return new ConverterFromLinear<float, float>(gt);

			gt = key.trng == typeof(EncodingRange.Video) ? LookupTables.MakeVideoGamma(gt) : gt;
			var bgt = gt == LookupTables.SrgbGamma ? LookupTables.SrgbGammaUQ15 : LookupTables.MakeUQ15Gamma(gt);
			if (key.tfrom == typeof(ushort) && key.tto == typeof(byte))
				return new ConverterFromLinear<ushort, byte>(bgt);
			if (key.tfrom == typeof(float) && key.tto == typeof(byte))
				return new ConverterFromLinear<float, byte>(bgt);
		}

		if (key.tenc == typeof(EncodingType.Companded))
		{
			var igt = key.profile.Curve.InverseGamma;
			if (key.tfrom == typeof(float) && key.tto == typeof(float))
				return new ConverterToLinear<float, float>(igt);

			igt = key.trng == typeof(EncodingRange.Video) ? LookupTables.MakeVideoInverseGamma(igt) : igt;
			if (key.tfrom == typeof(byte) && key.tto == typeof(float))
				return new ConverterToLinear<byte, float>(igt);
			if (key.tfrom == typeof(byte) && key.tto == typeof(ushort))
				return new ConverterToLinear<byte, ushort>(LookupTables.MakeUQ15InverseGamma(igt));
		}

		throw new ArgumentException("Invalid Type combination", nameof(cacheKey));
	});

	public IConverter<TFrom, TTo> GetConverter<TFrom, TTo, TEnc>() where TFrom : unmanaged where TTo : unmanaged where TEnc : EncodingType
	{
		var cacheKey = (typeof(TFrom), typeof(TTo), typeof(TEnc), typeof(EncodingRange.Full), this);

		return (IConverter<TFrom, TTo>)(converterCache.TryGetValue(cacheKey, out var converter) ? converter : addConverter(cacheKey));
	}

	public IConverter<TFrom, TTo> GetConverter<TFrom, TTo, TEnc, TRng>() where TFrom : unmanaged where TTo : unmanaged where TEnc : EncodingType where TRng : EncodingRange
	{
		var cacheKey = (typeof(TFrom), typeof(TTo), typeof(TEnc), typeof(TRng), this);

		return (IConverter<TFrom, TTo>)(converterCache.TryGetValue(cacheKey, out var converter) ? converter : addConverter(cacheKey));
	}
}

internal class MatrixProfile : CurveProfile
{
	public Matrix3x3C Matrix { get; }
	public Matrix3x3C InverseMatrix { get; }

	public MatrixProfile(byte[] bytes, byte[]? compact, Matrix3x3C matrix, Matrix3x3C imatrix, ProfileCurve? curve, ProfileColorSpace dataSpace, ProfileColorSpace pcsSpace) : base(bytes, compact, curve, dataSpace, pcsSpace)
	{
		ProfileType = ColorProfileType.Matrix;
		Matrix = matrix;
		InverseMatrix = imatrix;
	}
}

internal static class IccProfiles
{
	private static byte[] getResourceBinary(string name)
	{
		string resName = $"{nameof(PhotoSauce)}.{nameof(MagicScaler)}.Resources.{name}";
		using var stm = typeof(IccProfiles).Assembly.GetManifestResourceStream(resName)!;

		var buff = new byte[(int)stm.Length];
		stm.Read(buff, 0, buff.Length);

		return buff;
	}

	public static readonly Lazy<byte[]> sRgbV4 = new(() => getResourceBinary("sRGB-v4.icc"));
	public static readonly Lazy<byte[]> sRgbCompact = new(() => getResourceBinary("sRGB-v2-micro.icc"));
	public static readonly Lazy<byte[]> sGreyV4 = new(() => getResourceBinary("sGrey-v4.icc"));
	public static readonly Lazy<byte[]> sGreyCompact = new(() => getResourceBinary("sRGB-v2-micro.icc"));
	public static readonly Lazy<byte[]> AdobeRgbV4 = new(() => getResourceBinary("AdobeCompat-v4.icc"));
	public static readonly Lazy<byte[]> AdobeRgbCompact = new(() => getResourceBinary("AdobeCompat-v2.icc"));
	public static readonly Lazy<byte[]> DisplayP3V4 = new(() => getResourceBinary("DisplayP3Compat-v4.icc"));
	public static readonly Lazy<byte[]> DisplayP3Compact = new(() => getResourceBinary("DisplayP3Compat-v2-micro.icc"));
}