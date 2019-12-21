﻿using System;
using System.Diagnostics;
using System.Collections.Generic;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal static class MagicTransforms
	{
		private static readonly IReadOnlyDictionary<Guid, Guid> externalFormatMap = new Dictionary<Guid, Guid> {
			[PixelFormat.Grey32BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat8bppGray,
			[PixelFormat.Grey32BppLinearFloat.FormatGuid] = Consts.GUID_WICPixelFormat8bppGray,
			[PixelFormat.Grey16BppLinearUQ15.FormatGuid] = Consts.GUID_WICPixelFormat8bppGray,

			[PixelFormat.Y32BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat8bppY,
			[PixelFormat.Y32BppLinearFloat.FormatGuid] = Consts.GUID_WICPixelFormat8bppY,
			[PixelFormat.Y16BppLinearUQ15.FormatGuid] = Consts.GUID_WICPixelFormat8bppY,

			[PixelFormat.Bgrx128BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat24bppBGR,
			[PixelFormat.Bgrx128BppLinearFloat.FormatGuid] = Consts.GUID_WICPixelFormat24bppBGR,
			[PixelFormat.Bgr96BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat24bppBGR,
			[PixelFormat.Bgr96BppLinearFloat.FormatGuid] = Consts.GUID_WICPixelFormat24bppBGR,
			[PixelFormat.Bgr48BppLinearUQ15.FormatGuid] = Consts.GUID_WICPixelFormat24bppBGR,

			[PixelFormat.Pbgra128BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat32bppBGRA,
			[PixelFormat.Pbgra128BppLinearFloat.FormatGuid] = Consts.GUID_WICPixelFormat32bppBGRA,
			[PixelFormat.Pbgra64BppLinearUQ15.FormatGuid] = Consts.GUID_WICPixelFormat32bppBGRA,

			//[PixelFormat.CbCr64BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat16bppCbCr,
			[PixelFormat.Cb32BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat8bppCb,
			[PixelFormat.Cr32BppFloat.FormatGuid] = Consts.GUID_WICPixelFormat8bppCr
		};

		private static readonly IReadOnlyDictionary<Guid, Guid> internalFormatMapSimd = new Dictionary<Guid, Guid> {
			[Consts.GUID_WICPixelFormat8bppGray] = PixelFormat.Grey32BppFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat8bppY] = PixelFormat.Y32BppFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat24bppBGR] = PixelFormat.Bgrx128BppFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat32bppBGRA] = PixelFormat.Pbgra128BppFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat32bppPBGRA] = PixelFormat.Pbgra128BppFloat.FormatGuid, // !

			[PixelFormat.Grey32BppLinearFloat.FormatGuid] = PixelFormat.Grey32BppFloat.FormatGuid,
			[PixelFormat.Y32BppLinearFloat.FormatGuid] = PixelFormat.Y32BppFloat.FormatGuid,
			[PixelFormat.Bgrx128BppLinearFloat.FormatGuid] = PixelFormat.Bgrx128BppFloat.FormatGuid,
			[PixelFormat.Pbgra128BppLinearFloat.FormatGuid] = PixelFormat.Pbgra128BppFloat.FormatGuid,

			//[Consts.GUID_WICPixelFormat16bppCbCr] = PixelFormat.CbCr64BppFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat8bppCb] = PixelFormat.Cb32BppFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat8bppCr] = PixelFormat.Cr32BppFloat.FormatGuid
		};

		private static readonly IReadOnlyDictionary<Guid, Guid> internalFormatMapLinear = new Dictionary<Guid, Guid> {
			[Consts.GUID_WICPixelFormat8bppGray] = PixelFormat.Grey16BppLinearUQ15.FormatGuid,
			[Consts.GUID_WICPixelFormat8bppY] = PixelFormat.Y16BppLinearUQ15.FormatGuid,
			[Consts.GUID_WICPixelFormat24bppBGR] = PixelFormat.Bgr48BppLinearUQ15.FormatGuid,
			[Consts.GUID_WICPixelFormat32bppBGRA] = PixelFormat.Pbgra64BppLinearUQ15.FormatGuid
		};

		private static readonly IReadOnlyDictionary<Guid, Guid> internalFormatMapLinearSimd = new Dictionary<Guid, Guid> {
			[Consts.GUID_WICPixelFormat8bppGray] = PixelFormat.Grey32BppLinearFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat8bppY] = PixelFormat.Y32BppLinearFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat24bppBGR] = PixelFormat.Bgrx128BppLinearFloat.FormatGuid,
			[Consts.GUID_WICPixelFormat32bppBGRA] = PixelFormat.Pbgra128BppLinearFloat.FormatGuid,

			[PixelFormat.Grey32BppFloat.FormatGuid] = PixelFormat.Grey32BppLinearFloat.FormatGuid,
			[PixelFormat.Y32BppFloat.FormatGuid] = PixelFormat.Y32BppLinearFloat.FormatGuid,
			[PixelFormat.Bgrx128BppFloat.FormatGuid] = PixelFormat.Bgrx128BppLinearFloat.FormatGuid,
			[PixelFormat.Pbgra128BppFloat.FormatGuid] = PixelFormat.Pbgra128BppLinearFloat.FormatGuid,
		};


		public static void AddInternalFormatConverter(PipelineContext ctx, PixelValueEncoding enc = PixelValueEncoding.Unspecified, bool allow96bppFloat = false)
		{
			var ifmt = ctx.Source.Format.FormatGuid;
			var ofmt = ifmt;
			bool linear = enc == PixelValueEncoding.Unspecified ? ctx.Settings.BlendingMode == GammaMode.Linear : enc == PixelValueEncoding.Linear;

			if (allow96bppFloat && MagicImageProcessor.EnableSimd && ifmt == Consts.GUID_WICPixelFormat24bppBGR)
				ofmt = linear ? PixelFormat.Bgr96BppLinearFloat.FormatGuid : PixelFormat.Bgr96BppFloat.FormatGuid;
			else if (linear && (MagicImageProcessor.EnableSimd ? internalFormatMapLinearSimd : internalFormatMapLinear).TryGetValue(ifmt, out var ofmtl))
				ofmt = ofmtl;
			else if (MagicImageProcessor.EnableSimd && internalFormatMapSimd.TryGetValue(ifmt, out var ofmts))
				ofmt = ofmts;

			bool videoLevels = ifmt == Consts.GUID_WICPixelFormat8bppY && ctx.ImageFrame is IYccImageFrame frame && !frame.IsFullRange;

			if (ofmt == ifmt && !videoLevels)
				return;

			bool forceSrgb = (ofmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ofmt == PixelFormat.Y16BppLinearUQ15.FormatGuid) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt, videoLevels));
		}

		public static void AddExternalFormatConverter(PipelineContext ctx, bool allowPlanar = false)
		{
			if (allowPlanar && ctx.PlanarContext != null)
			{
				AddExternalFormatConverter(ctx);
				ctx.PlanarContext.SourceY = ctx.Source;
				ctx.Source = ctx.PlanarContext.SourceCb;

				AddExternalFormatConverter(ctx);
				ctx.PlanarContext.SourceCb = ctx.Source;
				ctx.Source = ctx.PlanarContext.SourceCr;

				AddExternalFormatConverter(ctx);
				ctx.PlanarContext.SourceCr = ctx.Source;
				ctx.Source = ctx.PlanarContext.SourceY;

				return;
			}

			var ifmt = ctx.Source.Format.FormatGuid;
			if (!externalFormatMap.TryGetValue(ifmt, out var ofmt) || ofmt == ifmt)
				return;

			bool forceSrgb = (ifmt == PixelFormat.Y32BppLinearFloat.FormatGuid || ifmt == PixelFormat.Y16BppLinearUQ15.FormatGuid) && ctx.SourceColorProfile != ColorProfile.sRGB;

			ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, forceSrgb ? ColorProfile.sRGB : ctx.SourceColorProfile, forceSrgb ? ColorProfile.sRGB : ctx.DestColorProfile, ofmt));
		}

		public static void AddHighQualityScaler(PipelineContext ctx, bool hybrid = false)
		{
			bool swap = ctx.Orientation.SwapsDimensions();
			var srect = ctx.Settings.InnerRect;

			int width = swap ? srect.Height : srect.Width, height = swap ? srect.Width : srect.Height;
			if (ctx.Source.Width == width && ctx.Source.Height == height)
				return;

			if (hybrid)
			{
				int ratio = ctx.Settings.HybridScaleRatio;
				if (ratio == 1 || ctx.Source.Format.FormatGuid != Consts.GUID_WICPixelFormat32bppCMYK)
					return;

				width = MathUtil.DivCeiling(ctx.Source.Width, ratio);
				height = MathUtil.DivCeiling(ctx.Source.Height, ratio);
				ctx.Settings.HybridMode = HybridScaleMode.Off;
			}

			var interpolatorx = width == ctx.Source.Width ? InterpolationSettings.NearestNeighbor : hybrid ? InterpolationSettings.Average : ctx.Settings.Interpolation;
			var interpolatory = height == ctx.Source.Height ? InterpolationSettings.NearestNeighbor : hybrid ? InterpolationSettings.Average : ctx.Settings.Interpolation;
			if (interpolatorx.WeightingFunction.Support >= 0.1 || interpolatory.WeightingFunction.Support >= 0.1)
				AddInternalFormatConverter(ctx, allow96bppFloat: true);

			bool offsetX = false, offsetY = false;
			if (ctx.ImageFrame is IYccImageFrame frame && ctx.PlanarContext != null && ctx.Source.Format.Encoding == PixelValueEncoding.Unspecified)
			{
				offsetX = frame.ChromaPosition.HasFlag(ChromaPosition.CositedHorizontal) && ctx.PlanarContext.ChromaSubsampling.IsSubsampledX();
				offsetY = frame.ChromaPosition.HasFlag(ChromaPosition.CositedVertical) && ctx.PlanarContext.ChromaSubsampling.IsSubsampledY();
			}

			var fmt = ctx.Source.Format;
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<float, float>.CreateResize(ctx.Source, width, height, interpolatorx, interpolatory, offsetX, offsetY));
			else if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
				ctx.Source = ctx.AddDispose(ConvolutionTransform<ushort, int>.CreateResize(ctx.Source, width, height, interpolatorx, interpolatory, offsetX, offsetY));
			else
				ctx.Source = ctx.AddDispose(ConvolutionTransform<byte, int>.CreateResize(ctx.Source, width, height, interpolatorx, interpolatory, offsetX, offsetY));

			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
		}

		public static void AddUnsharpMask(PipelineContext ctx)
		{
			var ss = ctx.Settings.UnsharpMask;
			if (!ctx.Settings.Sharpen || ss.Radius <= 0d || ss.Amount <= 0)
				return;

			var fmt = ctx.Source.Format;
			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float)
				ctx.Source = ctx.AddDispose(UnsharpMaskTransform<float, float>.CreateSharpen(ctx.Source, ss));
			else if (fmt.NumericRepresentation == PixelNumericRepresentation.Fixed)
				ctx.Source = ctx.AddDispose(UnsharpMaskTransform<ushort, int>.CreateSharpen(ctx.Source, ss));
			else
				ctx.Source = ctx.AddDispose(UnsharpMaskTransform<byte, int>.CreateSharpen(ctx.Source, ss));
		}

		public static void AddMatte(PipelineContext ctx)
		{
			var fmt = ctx.Source.Format;
			if (ctx.Settings.MatteColor.IsEmpty || fmt.ColorRepresentation != PixelColorRepresentation.Bgr || fmt.AlphaRepresentation == PixelAlphaRepresentation.None)
				return;

			if (fmt.NumericRepresentation == PixelNumericRepresentation.Float && fmt.Encoding == PixelValueEncoding.Companded)
				AddInternalFormatConverter(ctx, PixelValueEncoding.Linear);

			ctx.Source = new MatteTransform(ctx.Source, ctx.Settings.MatteColor);

			if (ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None && ctx.Settings.MatteColor.A == byte.MaxValue)
			{
				var oldFmt = ctx.Source.Format;
				var newFmt = oldFmt == PixelFormat.Pbgra64BppLinearUQ15 ? PixelFormat.Bgr48BppLinearUQ15
					: oldFmt.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA ? PixelFormat.FromGuid(Consts.GUID_WICPixelFormat24bppBGR)
					: throw new NotSupportedException("Unsupported pixel format");

				ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, newFmt.FormatGuid));
			}
		}

		public static void AddPad(PipelineContext ctx)
		{
			if (ctx.Settings.InnerRect == ctx.Settings.OuterRect)
				return;

			AddExternalFormatConverter(ctx);

			ctx.Source = new PadTransformInternal(ctx.Source, ctx.Settings.MatteColor, PixelArea.FromGdiRect(ctx.Settings.InnerRect), PixelArea.FromGdiRect(ctx.Settings.OuterRect));
		}

		public static void AddCropper(PipelineContext ctx)
		{
			var crop = PixelArea.FromGdiRect(ctx.Settings.Crop).DeOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height);
			if (crop == ctx.Source.Area)
				return;

			ctx.Source = new CropTransform(ctx.Source, crop);
			ctx.Settings.Crop = ctx.Source.Area.ReOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height).ToGdiRect();
		}

		public static void AddFlipRotator(PipelineContext ctx, Orientation orientation)
		{
			if (orientation == Orientation.Normal)
				return;

			if (orientation.RequiresCache())
				AddExternalFormatConverter(ctx);

			ctx.Source = new OrientationTransformInternal(ctx.Source, orientation, PixelArea.FromGdiRect(ctx.Settings.Crop));
			ctx.Settings.Crop = ctx.Source.Area.ToGdiRect();
			ctx.Orientation = Orientation.Normal;
		}

		public static void AddExifFlipRotator(PipelineContext ctx) => AddFlipRotator(ctx, ctx.Orientation);

		public static void AddColorProfileReader(PipelineContext ctx)
		{
			var fmt = ctx.ImageFrame is IYccImageFrame ? PixelFormat.FromGuid(PixelFormats.Bgr24bpp) : ctx.Source.Format;

			if (ctx.ImageFrame is WicImageFrame wicFrame)
			{
				ctx.WicContext.SourceColorContext = wicFrame.ColorProfileSource.WicColorContext;
				ctx.SourceColorProfile = wicFrame.ColorProfileSource.ParsedProfile;
			}
			else
			{
				var profile = ColorProfile.Cache.GetOrAdd(ctx.ImageFrame.IccProfile);
				if (profile.IsValid && profile.IsCompatibleWith(fmt) && !profile.IsSrgb)
				{
					ctx.WicContext.SourceColorContext = ctx.WicContext.AddRef(WicColorProfile.CreateContextFromProfile(profile.ProfileBytes));
					ctx.SourceColorProfile = profile;
				}
				else
				{
					var wicProfile = WicColorProfile.GetDefaultFor(fmt);
					ctx.WicContext.SourceColorContext = wicProfile.WicColorContext;
					ctx.SourceColorProfile = wicProfile.ParsedProfile;
				}
			}

			ctx.WicContext.DestColorContext = ctx.Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed ? WicColorProfile.GetDefaultFor(fmt).WicColorContext : ctx.WicContext.SourceColorContext;
			ctx.DestColorProfile = ctx.Settings.ColorProfileMode <= ColorProfileMode.NormalizeAndEmbed ? ColorProfile.GetDefaultFor(fmt) : ctx.SourceColorProfile;
		}

		public static void AddColorspaceConverter(PipelineContext ctx)
		{
			if (ctx.SourceColorProfile is null || ctx.DestColorProfile is null || ctx.SourceColorProfile == ctx.DestColorProfile)
				return;

			if (ctx.SourceColorProfile.ProfileType > ColorProfileType.Matrix || ctx.DestColorProfile.ProfileType > ColorProfileType.Matrix)
			{
				AddExternalFormatConverter(ctx);
				WicTransforms.AddColorspaceConverter(ctx);

				return;
			}

			AddInternalFormatConverter(ctx, PixelValueEncoding.Linear);

			if (ctx.Source.Format.ColorRepresentation == PixelColorRepresentation.Bgr && ctx.SourceColorProfile is MatrixProfile srcProf && ctx.DestColorProfile is MatrixProfile dstProf)
			{
				var matrix = srcProf.Matrix * dstProf.InverseMatrix;
				if (matrix != default && !matrix.IsIdentity)
					ctx.Source = new ColorMatrixTransformInternal(ctx.Source, matrix);
			}
		}

		public static void AddPlanarConverter(PipelineContext ctx)
		{
			Debug.Assert(ctx.PlanarContext != null);

			if (ctx.Source.Format.Encoding == PixelValueEncoding.Linear || ctx.PlanarContext.SourceCb.Format.NumericRepresentation != ctx.Source.Format.NumericRepresentation)
			{
				if (ctx.Source.Format.NumericRepresentation == PixelNumericRepresentation.Float && ctx.PlanarContext.SourceCb.Format.NumericRepresentation == ctx.Source.Format.NumericRepresentation)
					AddInternalFormatConverter(ctx, PixelValueEncoding.Companded);
				else
					AddExternalFormatConverter(ctx, true);
			}

			var matrix = YccMatrix.Rec601;
			bool videoLevels = false;

			if (ctx.ImageFrame is IYccImageFrame frame)
			{
				matrix = frame.RgbYccMatrix;
				videoLevels = !frame.IsFullRange;
			}

			ctx.Source = ctx.AddDispose(new PlanarConversionTransform(ctx.Source, ctx.PlanarContext.SourceCb, ctx.PlanarContext.SourceCr, matrix, videoLevels));
			ctx.PlanarContext = null;
		}
	}
}
