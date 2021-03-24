// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See third-party-notices in the repository root for more information.

// Ported from shared/wtypesbase.h in the Windows SDK for Windows 10.0.19041.0
// Original source is Copyright © Microsoft. All rights reserved.

// <auto-generated />
#pragma warning disable CS0649

namespace TerraFX.Interop
{
    internal unsafe partial struct BLOB
    {
        [NativeTypeName("ULONG")]
        public uint cbSize;

        [NativeTypeName("BYTE *")]
        public byte* pBlobData;
    }
}