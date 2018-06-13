// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.Compression
{
    internal static partial class ZipFileUtils
    {
        public static void ApplyPermissionsToEntry(ZipArchiveEntry entry, string sourceFileName)
        {
            Interop.Sys.FileStatus output;
            Interop.Sys.Stat(sourceFileName, out output);

            // The Stat Mode lower two bytes represent the filetype and permissions of a file/entry. These directly
            // correspond to the high two bytes of the Unix `zip` external attributes encoding.
            entry.ExternalAttributes |= (output.Mode << 16);
        }

        public static void ApplyPermissionsToFile(ZipArchiveEntry entry, string destinationFileName)
        {
            Interop.Sys.FileStatus output;
            Interop.Sys.Stat(destinationFileName, out output);

            // The Stat Mode lower two bytes represent the filetype and permissions of a file/entry. These directly
            // correspond to the high two bytes of the Unix `zip` external attributes encoding.
            int newMode = entry.ExternalAttributes | (output.Mode << 16);
            Interop.Sys.ChMod(destinationFileName, newMode);
        }
    }
}
