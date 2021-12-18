// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Numerics;
using System.Diagnostics;
using System.ComponentModel;

using TerraFX.Interop;

using PhotoSauce.Interop.Wic;
using PhotoSauce.MagicScaler.Transforms;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Provides a set of methods for constructing a MagicScaler processing pipeline or for all-at-once processing of an image.</summary>
	public static class MagicImageProcessor
	{
		/// <summary>"Use the <c>PhotoSauce.MagicScaler.MaxPooledBufferSize</c> <see cref="AppContext"/> value instead."</summary>
		[Obsolete($"Use {nameof(AppContext)} value {BufferPool.MaxPooledBufferSizeName} instead."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnableLargeBufferPool { get; set; }

		/// <summary>True to allow <a href="https://en.wikipedia.org/wiki/YCbCr">Y'CbCr</a> images to be processed in their native planar format, false to force RGB conversion before processing.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="EnablePlanarPipeline"]/*'/>
		/// <value>Default value: <see langword="true" /></value>
		public static bool EnablePlanarPipeline { get; set; } = true;

		/// <summary>True to check for <c>Orientation</c> tag in XMP metadata in addition to the default Exif metadata location, false to check Exif only.</summary>
		/// <value>Default value: <see langword="false" /></value>
		public static bool EnableXmpOrientation { get; set; }

		/// <summary>"Use the <c>PhotoSauce.MagicScaler.EnablePixelSourceStats</c> <see cref="AppContext"/> switch instead."</summary>
		[Obsolete($"Use {nameof(AppContext)} switch {StatsManager.SwitchName} instead."), EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnablePixelSourceStats { get; set; }

		/// <summary>Overrides the default <a href="https://en.wikipedia.org/wiki/SIMD">SIMD</a> support detection to force floating point processing on or off.</summary>
		/// <include file='Docs/Remarks.xml' path='doc/member[@name="EnableSimd"]/*'/>
		/// <value>Default value: <see langword="true" /> if the runtime/JIT and hardware support hardware-accelerated <see cref="System.Numerics.Vector{T}" />, otherwise <see langword="false" /></value>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool EnableSimd { get; set; } = Vector.IsHardwareAccelerated && (Vector<float>.Count is 4 or 8);

		[StackTraceHidden]
		private static void checkInStream(Stream imgStream)
		{
			if (imgStream is null) throw new ArgumentNullException(nameof(imgStream));
			if (!imgStream.CanSeek || !imgStream.CanRead) throw new ArgumentException("Input Stream must allow Seek and Read", nameof(imgStream));
			if (imgStream.Length <= 0 || imgStream.Position >= imgStream.Length) throw new ArgumentException("Input Stream is empty or positioned at its end", nameof(imgStream));
		}

		[StackTraceHidden]
		private static void checkOutStream(Stream outStream)
		{
			if (outStream is null) throw new ArgumentNullException(nameof(outStream));
			if (!outStream.CanSeek || !outStream.CanWrite) throw new ArgumentException("Output Stream must allow Seek and Write", nameof(outStream));
		}

		/// <summary>All-in-one processing of an image according to the specified <paramref name="settings" />.</summary>
		/// <param name="imgPath">The path to a file containing the input image.</param>
		/// <param name="outStream">The stream to which the output image will be written. The stream must allow Seek and Write.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessImageResult" /> containing the settings used and basic instrumentation for the pipeline.</returns>
		public static ProcessImageResult ProcessImage(string imgPath, Stream outStream, ProcessImageSettings settings)
		{
			if (imgPath is null) throw new ArgumentNullException(nameof(imgPath));
			if (settings is null) throw new ArgumentNullException(nameof(settings));
			checkOutStream(outStream);

			using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
			using var bfs = new PoolBufferedStream(fs);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

#pragma warning disable CS1573 // not all params have docs

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="outPath">The path to which the output image will be written.</param>
		/// <remarks>If <paramref name="outPath"/> already exists, it will be overwritten.</remarks>
		public static ProcessImageResult ProcessImage(string imgPath, string outPath, ProcessImageSettings settings)
		{
			if (imgPath is null) throw new ArgumentNullException(nameof(imgPath));
			if (outPath is null) throw new ArgumentNullException(nameof(outPath));
			if (settings is null) throw new ArgumentNullException(nameof(settings));

			if (settings.SaveFormat == FileFormat.Auto)
			{
				string extension = Path.GetExtension(outPath);
				if (CodecManager.TryGetEncoderForFileExtension(extension, out var info))
					settings.EncoderInfo = info;
				else
					throw new NotSupportedException($"An encoder for file extension '{extension}' could not be found.");
			}

			using var fsi = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
			using var fso = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1);
			using var bfi = new PoolBufferedStream(fsi);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfi));
			buildPipeline(ctx);

			return WriteOutput(ctx, fso);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgBuffer">A buffer containing a supported input image container.</param>
		public static unsafe ProcessImageResult ProcessImage(ReadOnlySpan<byte> imgBuffer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgBuffer.Length is 0) throw new ArgumentNullException(nameof(imgBuffer));
			if (settings is null) throw new ArgumentNullException(nameof(settings));
			checkOutStream(outStream);

			fixed (byte* pbBuffer = imgBuffer)
			{
				using var ums = new UnmanagedMemoryStream(pbBuffer, imgBuffer.Length);
				using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(ums));
				buildPipeline(ctx);

				return WriteOutput(ctx, outStream);
			}
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
		public static ProcessImageResult ProcessImage(Stream imgStream, Stream outStream, ProcessImageSettings settings)
		{
			if (settings is null) throw new ArgumentNullException(nameof(settings));
			checkInStream(imgStream);
			checkOutStream(outStream);

			using var bfs = PoolBufferedStream.WrapIfFile(imgStream);
			using var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs ?? imgStream));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		public static ProcessImageResult ProcessImage(IPixelSource imgSource, Stream outStream, ProcessImageSettings settings)
		{
			if (imgSource is null) throw new ArgumentNullException(nameof(imgSource));
			if (settings is null) throw new ArgumentNullException(nameof(settings));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <inheritdoc cref="ProcessImage(string, Stream, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static ProcessImageResult ProcessImage(IImageContainer imgContainer, Stream outStream, ProcessImageSettings settings)
		{
			if (imgContainer is null) throw new ArgumentNullException(nameof(imgContainer));
			if (settings is null) throw new ArgumentNullException(nameof(settings));
			checkOutStream(outStream);

			using var ctx = new PipelineContext(settings, imgContainer, false);
			buildPipeline(ctx);

			return WriteOutput(ctx, outStream);
		}

		/// <summary>Constructs a new processing pipeline from which pixels can be retrieved.</summary>
		/// <param name="imgPath">The path to a file containing the input image.</param>
		/// <param name="settings">The settings for this processing operation.</param>
		/// <returns>A <see cref="ProcessingPipeline" /> containing the <see cref="IPixelSource" />, settings used, and basic instrumentation for the pipeline.</returns>
		public static ProcessingPipeline BuildPipeline(string imgPath, ProcessImageSettings settings)
		{
			if (imgPath is null) throw new ArgumentNullException(nameof(imgPath));
			if (settings is null) throw new ArgumentNullException(nameof(settings));

			var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
			var bfs = new PoolBufferedStream(fs, true);
			var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs));
			ctx.AddDispose(bfs);
			buildPipeline(ctx, false);

			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgStream">A stream containing a supported input image container. The stream must allow Seek and Read.</param>
		public static ProcessingPipeline BuildPipeline(Stream imgStream, ProcessImageSettings settings)
		{
			if (settings is null) throw new ArgumentNullException(nameof(settings));
			checkInStream(imgStream);

			var bfs = PoolBufferedStream.WrapIfFile(imgStream);
			var ctx = new PipelineContext(settings, CodecManager.GetDecoderForStream(bfs ?? imgStream));
			if (bfs is not null)
				ctx.AddDispose(bfs);

			buildPipeline(ctx, false);

			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgSource">A custom pixel source to use as input.</param>
		public static ProcessingPipeline BuildPipeline(IPixelSource imgSource, ProcessImageSettings settings)
		{
			if (imgSource is null) throw new ArgumentNullException(nameof(imgSource));
			if (settings is null) throw new ArgumentNullException(nameof(settings));

			var ctx = new PipelineContext(settings, new PixelSourceContainer(imgSource));
			buildPipeline(ctx, false);

			return new ProcessingPipeline(ctx);
		}

		/// <inheritdoc cref="BuildPipeline(string, ProcessImageSettings)" />
		/// <param name="imgContainer">A custom <see cref="IImageContainer"/> to use as input.</param>
		public static ProcessingPipeline BuildPipeline(IImageContainer imgContainer, ProcessImageSettings settings)
		{
			if (imgContainer is null) throw new ArgumentNullException(nameof(imgContainer));
			if (settings is null) throw new ArgumentNullException(nameof(settings));

			var ctx = new PipelineContext(settings, imgContainer, false);
			buildPipeline(ctx, false);

			return new ProcessingPipeline(ctx);
		}

#pragma warning restore 1573

		internal static unsafe ProcessImageResult WriteOutput(PipelineContext ctx, Stream ostm)
		{
			MagicTransforms.AddExternalFormatConverter(ctx);

			using var bfs = PoolBufferedStream.WrapIfFile(ostm);
			using var enc = ctx.Settings.EncoderInfo.Factory(bfs ?? ostm, ctx.Settings.EncoderOptions);

			if (ctx.IsAnimatedGifPipeline && enc is WicImageEncoder wenc)
			{
				using var gif = new WicAnimatedGifEncoder(ctx, wenc);
				gif.WriteGlobalMetadata();
				gif.WriteFrames();
			}
			else
			{
				MagicTransforms.AddIndexedColorConverter(ctx);
				enc.WriteFrame(ctx.Source, ctx.Metadata, PixelArea.Default);
			}

			enc.Commit();

			return new ProcessImageResult(ctx.UsedSettings, ctx.Stats);
		}

		private static unsafe void buildPipeline(PipelineContext ctx, bool closedPipeline = true)
		{
			ctx.ImageFrame = ctx.ImageContainer.GetFrame(ctx.Settings.FrameIndex);
			ctx.Settings.ColorProfileMode = closedPipeline ? ctx.Settings.ColorProfileMode : ColorProfileMode.ConvertToSrgb;

			bool processPlanar = false;
			bool outputPlanar = closedPipeline;
			var wicFrame = ctx.ImageFrame as WicImageFrame;

			if (wicFrame is not null)
			{
				processPlanar = EnablePlanarPipeline && wicFrame.SupportsPlanarProcessing && ctx.Settings.Interpolation.WeightingFunction.Support >= 0.5;
				bool profilingPassThrough = processPlanar || (wicFrame.SupportsNativeScale && ctx.Settings.HybridScaleRatio > 1);
				ctx.Source = ctx.AddProfiler(new ComPtr<IWICBitmapSource>(wicFrame.WicSource).AsPixelSource(nameof(IWICBitmapFrameDecode), !profilingPassThrough));
			}
			else if (ctx.ImageFrame is IYccImageFrame yccFrame)
			{
				processPlanar = true;
				outputPlanar = outputPlanar && yccFrame.IsFullRange && yccFrame.RgbYccMatrix.IsRouglyEqualTo(YccMatrix.Rec601);
				ctx.Source = new PlanarPixelSource(yccFrame.PixelSource.AsPixelSource(), yccFrame.PixelSourceCb.AsPixelSource(), yccFrame.PixelSourceCr.AsPixelSource(), !yccFrame.IsFullRange);
			}
			else
			{
				ctx.Source = ctx.ImageFrame.PixelSource.AsPixelSource();
				if (ctx.ImageFrame.PixelSource is IProfileSource prof)
					ctx.AddProfiler(prof);
			}

			ctx.Metadata = new MagicMetadataFilter(ctx);

			MagicTransforms.AddGifFrameBuffer(ctx, !ctx.IsAnimatedGifPipeline);

			ctx.FinalizeSettings();
			ctx.Settings.UnsharpMask = ctx.UsedSettings.UnsharpMask;
			ctx.Settings.JpegQuality = ctx.UsedSettings.JpegQuality;
			ctx.Settings.JpegSubsampleMode = ctx.UsedSettings.JpegSubsampleMode;

			var subsample = ctx.Settings.JpegSubsampleMode;
			if (processPlanar)
			{
				if (wicFrame is not null)
				{
					if (ctx.Settings.ScaleRatio == 1d)
						processPlanar = false;

					if (!ctx.Settings.AutoCrop && ctx.Settings.HybridScaleRatio == 1)
					{
						var orCrop = ((PixelArea)ctx.Settings.Crop).DeOrient(ctx.Orientation, ctx.Source.Width, ctx.Source.Height);

						if (wicFrame.ChromaSubsampling.IsSubsampledX() && ((orCrop.X & 1) != 0 || (orCrop.Width & 1) != 0))
							processPlanar = false;
						if (wicFrame.ChromaSubsampling.IsSubsampledY() && ((orCrop.Y & 1) != 0 || (orCrop.Height & 1) != 0))
							processPlanar = false;
					}
				}

				if (ctx.Settings.SaveFormat == FileFormat.Jpeg && ctx.Orientation.SwapsDimensions())
				{
					if (subsample.IsSubsampledX() && (ctx.Settings.InnerSize.Width & 1) != 0)
						outputPlanar = false;
					if (subsample.IsSubsampledY() && (ctx.Settings.InnerSize.Height & 1) != 0)
						outputPlanar = false;
				}
			}

			MagicTransforms.AddColorProfileReader(ctx);

			if (processPlanar)
			{
				bool savePlanar = outputPlanar
					&& ctx.Settings.SaveFormat == FileFormat.Jpeg
					&& ctx.Settings.OuterSize == ctx.Settings.InnerSize
					&& ctx.DestColorProfile == ctx.SourceColorProfile;

				if (wicFrame is not null)
					WicTransforms.AddPlanarCache(ctx);

				MagicTransforms.AddCropper(ctx);
				MagicTransforms.AddHybridScaler(ctx);
				MagicTransforms.AddHighQualityScaler(ctx, savePlanar ? subsample : ChromaSubsampleMode.Subsample444);
				MagicTransforms.AddUnsharpMask(ctx);

				if (savePlanar)
				{
					MagicTransforms.AddExternalFormatConverter(ctx, true);
					MagicTransforms.AddExifFlipRotator(ctx);
				}
				else
				{
					MagicTransforms.AddPlanarConverter(ctx);
					MagicTransforms.AddColorspaceConverter(ctx);
					MagicTransforms.AddExifFlipRotator(ctx);
					MagicTransforms.AddPad(ctx);
				}
			}
			else
			{
				WicTransforms.AddNativeScaler(ctx);
				MagicTransforms.AddCropper(ctx);
				MagicTransforms.AddHybridScaler(ctx);
				WicTransforms.AddPixelFormatConverter(ctx);
				MagicTransforms.AddHybridScaler(ctx);
				MagicTransforms.AddHighQualityScaler(ctx);
				MagicTransforms.AddColorspaceConverter(ctx);
				MagicTransforms.AddMatte(ctx);
				MagicTransforms.AddUnsharpMask(ctx);
				MagicTransforms.AddExifFlipRotator(ctx);
				MagicTransforms.AddPad(ctx);
			}
		}
	}
}
