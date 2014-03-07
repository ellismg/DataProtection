﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.AspNet.Security.DataProtection
{
    // http://msdn.microsoft.com/en-us/library/windows/desktop/aa381414(v=vs.85).aspx
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DATA_BLOB
    {
        public uint cbData;
        public byte* pbData;
    }
}