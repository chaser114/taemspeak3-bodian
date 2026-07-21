// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace TSLib.Helper
{
	internal static class NativeLibraryLoader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static bool opusResolverRegistered;

#if !NETCOREAPP3_0_OR_GREATER
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("libdl.so.2", EntryPoint = "dlopen")]
		private static extern IntPtr dlopen(string fileName, int flags);

		private const int RtldNow = 2;
#endif

		public static bool DirectLoadLibrary(string lib, Action? dummyLoad = null)
		{
			if (Tools.IsLinux)
			{
				// Debian/Ubuntu package libopus0 only ships libopus.so.0, while DllImport("libopus")
				// looks for libopus / libopus.so. Try known sonames first, then warm the P/Invoke path.
				TryRegisterLinuxOpusResolver(lib);
				foreach (var candidate in LinuxLibCandidates(lib))
				{
					if (TryLoadNative(candidate))
					{
						Log.Debug("Loaded native library \"{0}\" as \"{1}\"", lib, candidate);
						break;
					}
				}

				try
				{
					dummyLoad?.Invoke();
					return true;
				}
				catch (DllNotFoundException ex)
				{
					Log.Error(ex, "Failed to load library \"{0}\". On Debian/Ubuntu install: sudo apt-get install -y libopus0", lib);
					return false;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to load library \"{0}\".", lib);
					return false;
				}
			}

			foreach (var libPath in LibPathOptions(lib))
			{
				Log.Debug("Loading \"{0}\" from \"{1}\"", lib, libPath);
				if (TryLoadNative(libPath))
					return true;
			}
			Log.Error("Failed to load library \"{0}\", error: {1}", lib, Marshal.GetLastWin32Error());
			return false;
		}

		private static bool TryLoadNative(string pathOrName)
		{
			try
			{
#if NETCOREAPP3_0_OR_GREATER
				return NativeLibrary.TryLoad(pathOrName, out _);
#else
				if (Tools.IsLinux)
					return dlopen(pathOrName, RtldNow) != IntPtr.Zero;
				return LoadLibrary(pathOrName) != IntPtr.Zero;
#endif
			}
			catch
			{
				return false;
			}
		}

		private static void TryRegisterLinuxOpusResolver(string lib)
		{
#if !NETCOREAPP3_0_OR_GREATER
			return;
#else
			if (opusResolverRegistered) return;
			if (!string.Equals(lib, "libopus", StringComparison.OrdinalIgnoreCase)
				&& !lib.StartsWith("libopus", StringComparison.OrdinalIgnoreCase))
				return;

			try
			{
				IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
				{
					if (!string.Equals(name, "libopus", StringComparison.OrdinalIgnoreCase))
						return IntPtr.Zero;
					foreach (var candidate in LinuxLibCandidates("libopus"))
					{
						if (NativeLibrary.TryLoad(candidate, out var handle))
							return handle;
					}
					return IntPtr.Zero;
				}

				NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, Resolve);
				var opusAsm = typeof(TSLib.Audio.Opus.NativeMethods).Assembly;
				if (opusAsm != typeof(NativeLibraryLoader).Assembly)
					NativeLibrary.SetDllImportResolver(opusAsm, Resolve);
				opusResolverRegistered = true;
			}
			catch (InvalidOperationException)
			{
				opusResolverRegistered = true;
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Could not register libopus DllImport resolver.");
			}
#endif
		}

		private static IEnumerable<string> LinuxLibCandidates(string lib)
		{
			// Prefer explicit sonames shipped by distro packages.
			yield return "libopus.so.0";
			yield return "libopus.so";
			yield return "libopus";
			yield return lib;
			if (!lib.EndsWith(".so", StringComparison.Ordinal) && lib.IndexOf(".so.", StringComparison.Ordinal) < 0)
			{
				yield return lib + ".so.0";
				yield return lib + ".so";
			}

			// Local package-relative paths (optional bundled libs / symlinks).
			foreach (var path in LibPathOptions("libopus.so.0"))
				yield return path;
			foreach (var path in LibPathOptions("libopus.so"))
				yield return path;
			foreach (var path in LibPathOptions(lib))
				yield return path;

			// Common absolute locations on Debian/Ubuntu/CentOS.
			yield return "/usr/lib/x86_64-linux-gnu/libopus.so.0";
			yield return "/usr/lib/aarch64-linux-gnu/libopus.so.0";
			yield return "/usr/lib64/libopus.so.0";
			yield return "/usr/lib/libopus.so.0";
			yield return "/lib/x86_64-linux-gnu/libopus.so.0";
			yield return "/lib64/libopus.so.0";
		}

		private static IEnumerable<string> LibPathOptions(string lib)
		{
			var fullPath = Directory.GetCurrentDirectory();
			yield return Path.Combine(fullPath, "lib", ArchFolder, lib);
			yield return Path.Combine(fullPath, "lib", lib);
			// When CWD is data/, also try parent package root.
			var parent = Directory.GetParent(fullPath)?.FullName;
			if (!string.IsNullOrEmpty(parent))
			{
				yield return Path.Combine(parent, "lib", ArchFolder, lib);
				yield return Path.Combine(parent, "lib", lib);
			}
			var asmPath = Path.GetDirectoryName(typeof(NativeLibraryLoader).Assembly.Location)!;
			yield return Path.Combine(asmPath, "lib", ArchFolder, lib);
			yield return Path.Combine(asmPath, "lib", lib);
		}

		public static string ArchFolder
		{
			get
			{
				if (IntPtr.Size == 8)
					return "x64";
				if (IntPtr.Size == 4)
					return "x86";
				return "xOther";
			}
		}
	}
}
