PhotoSauce.NativeCodecs.Libwebp
===============================

This MagicScaler plugin wraps the [libwebp](https://chromium.googlesource.com/webm/libwebp) reference [WebP](https://developers.google.com/speed/webp) codec.

Windows 10 and 11 include a WebP decoder by default, but it may not function for all users or apps.  This plugin is more capable than the Windows codec and will work anywhere `libwebp` is available.

Requirements
------------

A compatible set of `libwebp` binaries must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

Usage
-----

### Codec Registration

To register the codec, call the `UseLibwebp` extension method from your `CodecManager.Configure` action at app startup.  By default, the plugin will remove/replace the Windows WebP codec if it is present.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libwebp;

CodecManager.Configure(codecs => {
    codecs.UseLibwebp();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

To encode WebP images, the encoder MIME type can be set on `ProcessImageSettings`:

```C#
var settings = new ProcessImageSettings();
settings.TrySetEncoderFormat(ImageMimeTypes.Webp)
```

Or the encoder can be selected by file extension on overloads accepting file paths:

```C#
MagicImageProcessor.ProcessImage(@"\img\input.jpg", @"\img\output.webp", settings);
```
