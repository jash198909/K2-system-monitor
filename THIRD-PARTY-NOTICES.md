# Third-Party Notices

K2's own application code is licensed under the MIT License (see `LICENSE`).

K2 uses the following third-party libraries. They are **not** part of K2's MIT
code and remain under their respective licenses. The DLLs are shipped/restored
unmodified from NuGet.org.

| DLL | NuGet package | License | Source |
|---|---|---|---|
| LibreHardwareMonitorLib.dll | LibreHardwareMonitorLib 0.9.6 | MPL-2.0 | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| DiskInfoToolkit.dll | DiskInfoToolkit 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD.dll | RAMSPDToolkit-NDD 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| BlackSharp.Core.dll | BlackSharp.Core 1.0.7 | MPL-2.0 | https://github.com/Blacktempel/BlackSharp |
| HidSharp.dll | HidSharp 2.6.4 | Apache-2.0 | https://software.seekye.com/hidsharp |
| SharpDX.dll | SharpDX 4.2.0 | MIT | https://github.com/sharpdx/SharpDX |
| SharpDX.DXGI.dll | SharpDX.DXGI 4.2.0 | MIT | https://github.com/sharpdx/SharpDX |
| SharpDX.Direct3D11.dll | SharpDX.Direct3D11 4.2.0 | MIT | https://github.com/sharpdx/SharpDX |
| System.Management.dll | System.Management 10.0.2 | MIT | https://github.com/dotnet/runtime |
| System.Memory.dll | System.Memory 4.6.3 | MIT | https://github.com/dotnet/runtime |
| System.Buffers.dll | System.Buffers 4.6.1 | MIT | https://github.com/dotnet/runtime |
| System.Numerics.Vectors.dll | System.Numerics.Vectors 4.6.1 | MIT | https://github.com/dotnet/runtime |
| System.Runtime.CompilerServices.Unsafe.dll | System.Runtime.CompilerServices.Unsafe 6.1.2 | MIT | https://github.com/dotnet/runtime |
| System.CodeDom.dll | System.CodeDom 10.0.2 | MIT | https://github.com/dotnet/runtime |
| System.Threading.AccessControl.dll | System.Threading.AccessControl 10.0.3 | MIT | https://github.com/dotnet/runtime |
| System.Security.AccessControl.dll | System.Security.AccessControl 6.0.0 | MIT | https://github.com/dotnet/runtime |
| System.Security.Principal.Windows.dll | System.Security.Principal.Windows 5.0.0 | MIT | https://github.com/dotnet/runtime |
| System.Security.Claims.dll | System.Security.Claims 4.3.0 | MIT | https://github.com/dotnet/runtime |
| PresentMon-2.4.1-x64.exe (tools/) | Intel PresentMon 2.4.1 | MIT | https://github.com/GameTechDev/PresentMon |

Full license texts are in `licenses/`:

- `licenses/MPL-2.0.txt` — Mozilla Public License 2.0
- `licenses/Apache-2.0.txt` — Apache License 2.0
- `licenses/MIT.txt` — MIT License
- `licenses/PresentMon-MIT.txt` — Intel PresentMon MIT License
- `licenses/Microsoft-dotnet-library-license.txt` — Microsoft .NET library license

## PresentMon

K2 uses the official, unmodified Intel PresentMon 2.4.1 command-line tool
(`tools/PresentMon-2.4.1-x64.exe`) to measure frame presentation events and
displayed frame times. PresentMon is not part of K2's MIT source code and is
distributed under the MIT License; the Intel copyright notice and permission
notice are preserved in `licenses/PresentMon-MIT.txt`. K2 communicates with it
only through its documented CSV command-line output.

## MPL-2.0 notes

MPL-2.0 is a file-level license. The MPL-covered libraries are included
unmodified as separate DLL files and remain under MPL-2.0. Their source code is
available at the project links above. K2's own source files are not derived from
those libraries and remain under MIT.

## What is not included

- No fonts are bundled. K2 only references fonts installed in Windows (Segoe UI
  by default).
- No game assets, screenshots, logos or other third-party media are part of the
  source tree.
- No user settings, sensor logs or crash logs are part of the repository; they
  are written to `%LOCALAPPDATA%\K2\`.

## Trademarks

Product and company names mentioned in the documentation belong to their
respective owners. "K2" here is the name of this project; the project is not
affiliated with, endorsed by, or sponsored by any other company using the K2
name.
