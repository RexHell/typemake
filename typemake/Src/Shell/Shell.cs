﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TypeMake
{
    public static class Shell
    {
        private class PushDirectoryDisposee : IDisposable
        {
            public String OriginalDir;
            public void Dispose()
            {
                Environment.CurrentDirectory = Path.GetFullPath(OriginalDir);
            }
        }

        public static IDisposable PushDirectory(String Dir)
        {
            var d = new PushDirectoryDisposee { OriginalDir = Environment.CurrentDirectory };
            Environment.CurrentDirectory = Path.GetFullPath(Dir);
            return d;
        }

        public enum BuildingOperatingSystemType
        {
            Windows,
            Linux,
            Mac,
            Unknown
        }
        public enum BuildingOperatingSystemArchitectureType
        {
            x86,
            x86_64,
            Unknown
        }

        private static Object OperatingSystemLockee = new Object();
        private static BuildingOperatingSystemType? OperatingSystemValue = null;
        public static BuildingOperatingSystemType OperatingSystem
        {
            get
            {
                lock (OperatingSystemLockee)
                {
                    if (OperatingSystemValue == null)
                    {
                        var p = Environment.OSVersion.Platform;
                        if ((p == PlatformID.Win32NT) || (p == PlatformID.Xbox) || (p == PlatformID.WinCE) || (p == PlatformID.Win32Windows) || (p == PlatformID.Win32S))
                        {
                            OperatingSystemValue = BuildingOperatingSystemType.Windows;
                        }
                        else if (p == PlatformID.Unix)
                        {
                            if (File.Exists("/usr/lib/libc.dylib"))
                            {
                                OperatingSystemValue = BuildingOperatingSystemType.Mac;
                            }
                            else
                            {
                                OperatingSystemValue = BuildingOperatingSystemType.Linux;
                            }
                        }
                        else if (p == PlatformID.MacOSX)
                        {
                            OperatingSystemValue = BuildingOperatingSystemType.Mac;
                        }
                        else
                        {
                            OperatingSystemValue = BuildingOperatingSystemType.Unknown;
                        }
                    }
                    return OperatingSystemValue.Value;
                }
            }
        }

        private static Object OperatingSystemArchitectureLockee = new Object();
        private static BuildingOperatingSystemArchitectureType? OperatingSystemArchitectureValue = null;
        public static BuildingOperatingSystemArchitectureType OperatingSystemArchitecture
        {
            get
            {
                lock (OperatingSystemArchitectureLockee)
                {
                    if (OperatingSystemArchitectureValue == null)
                    {
                        if (Environment.Is64BitOperatingSystem)
                        {
                            OperatingSystemArchitectureValue = BuildingOperatingSystemArchitectureType.x86_64;
                        }
                        else
                        {
                            OperatingSystemArchitectureValue = BuildingOperatingSystemArchitectureType.x86;
                        }
                        //other architecture not supported now
                    }
                    return OperatingSystemArchitectureValue.Value;
                }
            }
        }

        public static String TryLocate(String ProgramName)
        {
            foreach (var Dir in Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator))
            {
                var p = Path.Combine(Dir, ProgramName);
                if (File.Exists(p))
                {
                    return GetCaseSensitivePath(Path.GetFullPath(p));
                }
                if (OperatingSystem == BuildingOperatingSystemType.Windows)
                {
                    if (File.Exists(p + ".exe"))
                    {
                        return GetCaseSensitivePath(Path.GetFullPath(p + ".exe"));
                    }
                    else if (File.Exists(p + ".cmd"))
                    {
                        return GetCaseSensitivePath(Path.GetFullPath(p + ".cmd"));
                    }
                    else if (File.Exists(p + ".bat"))
                    {
                        return GetCaseSensitivePath(Path.GetFullPath(p + ".bat"));
                    }
                }
            }
            return null;
        }
        private static string GetCaseSensitivePath(string path)
        {
            var root = Path.GetPathRoot(path);
            foreach (var name in path.Substring(root.Length).Split(Path.DirectorySeparatorChar))
            {
                var l = Directory.GetFileSystemEntries(root, name);
                if (l.Length == 0)
                {
                    break;
                }
                root = l.First();
            }
            root += path.Substring(root.Length);
            return root;
        }

        public static int Execute(String ProgramPath, params String[] Arguments)
        {
            return ExecuteLine(ProgramPath, String.Join(" ", Arguments.Select(arg => EscapeArgument(arg))));
        }
        public static int ExecuteLine(String ProgramPath, String Arguments)
        {
            if (OperatingSystem == BuildingOperatingSystemType.Windows)
            {
                if (ProgramPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || ProgramPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    return ExecuteInner("cmd", Arguments == "" ? EscapeArgument(ProgramPath) : EscapeArgument(ProgramPath) + " " + Arguments);
                }
            }
            return ExecuteInner(ProgramPath, Arguments);
        }
        private static int ExecuteInner(String ProgramPath, String Arguments)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = ProgramPath,
                Arguments = Arguments,
                UseShellExecute = false
            };
            var p = Process.Start(psi);
            p.WaitForExit();
            return p.ExitCode;
        }
        private static String EscapeArgument(String Argument)
        {
            var arg = Argument.Replace("\"", "\"\"\"");
            return arg.Contains(' ') ? "\"" + arg + "\"" : arg;
        }
    }
}