﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace TypeMake.Cpp
{
    public static class ConfigurationUtils
    {
        public static IEnumerable<Configuration> Matches(this IEnumerable<Configuration> Configurations, TargetType? TargetType, ToolchainType? Toolchain, CompilerType? Compiler, OperatingSystemType? HostOperatingSystem, ArchitectureType? HostArchitecture, OperatingSystemType? TargetOperatingSystem, ArchitectureType? TargetArchitecture, ConfigurationType? ConfigurationType)
        {
            Func<Configuration, bool> Filter = (Configuration c) =>
                ((TargetType == null) || (c.MatchingTargetTypes == null) || (c.MatchingTargetTypes.Contains(TargetType.Value)))
                && ((Toolchain == null) || (c.MatchingToolchains == null) || (c.MatchingToolchains.Contains(Toolchain.Value)))
                && ((Compiler == null) || (c.MatchingCompilers == null) || (c.MatchingCompilers.Contains(Compiler.Value)))
                && ((HostOperatingSystem == null) || (c.MatchingHostOperatingSystems == null) || (c.MatchingHostOperatingSystems.Contains(HostOperatingSystem.Value)))
                && ((HostArchitecture == null) || (c.MatchingHostArchitectures == null) || (c.MatchingHostArchitectures.Contains(HostArchitecture.Value)))
                && ((TargetOperatingSystem == null) || (c.MatchingTargetOperatingSystems == null) || (c.MatchingTargetOperatingSystems.Contains(TargetOperatingSystem.Value)))
                && ((TargetArchitecture == null) || (c.MatchingTargetArchitectures == null) || (c.MatchingTargetArchitectures.Contains(TargetArchitecture.Value)))
                && ((ConfigurationType == null) || (c.MatchingConfigurationTypes == null) || (c.MatchingConfigurationTypes.Contains(ConfigurationType.Value)));
            return Configurations.Where(Filter);
        }
        public static IEnumerable<Configuration> StrictMatches(this IEnumerable<Configuration> Configurations, TargetType? TargetType, ToolchainType? Toolchain, CompilerType? Compiler, OperatingSystemType? HostOperatingSystem, ArchitectureType? HostArchitecture, OperatingSystemType? TargetOperatingSystem, ArchitectureType? TargetArchitecture, ConfigurationType? ConfigurationType)
        {
            Func<Configuration, bool> Filter = (Configuration c) =>
                ((c.MatchingTargetTypes == null) || ((TargetType != null) && c.MatchingTargetTypes.Contains(TargetType.Value)))
                && ((c.MatchingToolchains == null) || ((Toolchain != null) && c.MatchingToolchains.Contains(Toolchain.Value)))
                && ((c.MatchingCompilers == null) || ((Compiler != null) && c.MatchingCompilers.Contains(Compiler.Value)))
                && ((c.MatchingHostOperatingSystems == null) || ((HostOperatingSystem != null) && c.MatchingHostOperatingSystems.Contains(HostOperatingSystem.Value)))
                && ((c.MatchingHostArchitectures == null) || ((HostArchitecture != null) && c.MatchingHostArchitectures.Contains(HostArchitecture.Value)))
                && ((c.MatchingTargetOperatingSystems == null) || ((TargetOperatingSystem != null) && c.MatchingTargetOperatingSystems.Contains(TargetOperatingSystem.Value)))
                && ((c.MatchingTargetArchitectures == null) || ((TargetArchitecture != null) && c.MatchingTargetArchitectures.Contains(TargetArchitecture.Value)))
                && ((c.MatchingConfigurationTypes == null) || ((ConfigurationType != null) && c.MatchingConfigurationTypes.Contains(ConfigurationType.Value)));
            return Configurations.Where(Filter);
        }
        public static Configuration Merged(this IEnumerable<Configuration> Configurations, TargetType? TargetType, ToolchainType? Toolchain, CompilerType? Compiler, OperatingSystemType? HostOperatingSystem, ArchitectureType? HostArchitecture, OperatingSystemType? TargetOperatingSystem, ArchitectureType? TargetArchitecture, ConfigurationType? ConfigurationType)
        {
            var Matched = Configurations.StrictMatches(TargetType, Toolchain, Compiler, HostOperatingSystem, HostArchitecture, TargetOperatingSystem, TargetArchitecture, ConfigurationType).ToList();
            var conf = new Configuration
            {
                MatchingTargetTypes = Toolchain == null ? null : new List<TargetType> { TargetType.Value },
                MatchingToolchains = Toolchain == null ? null : new List<ToolchainType> { Toolchain.Value },
                MatchingCompilers = Compiler == null ? null : new List<CompilerType> { Compiler.Value },
                MatchingHostOperatingSystems = HostOperatingSystem == null ? null : new List<OperatingSystemType> { HostOperatingSystem.Value },
                MatchingHostArchitectures = HostArchitecture == null ? null : new List<ArchitectureType> { HostArchitecture.Value },
                MatchingTargetOperatingSystems = TargetOperatingSystem == null ? null : new List<OperatingSystemType> { TargetOperatingSystem.Value },
                MatchingTargetArchitectures = TargetArchitecture == null ? null : new List<ArchitectureType> { TargetArchitecture.Value },
                MatchingConfigurationTypes = ConfigurationType == null ? null : new List<ConfigurationType> { ConfigurationType.Value },
                IncludeDirectories = Matched.SelectMany(c => c.IncludeDirectories).Distinct().ToList(),
                Defines = Matched.SelectMany(c => c.Defines).ToList(),
                CommonFlags = Matched.SelectMany(c => c.CommonFlags).ToList(),
                CFlags = Matched.SelectMany(c => c.CFlags).ToList(),
                CppFlags = Matched.SelectMany(c => c.CppFlags).ToList(),
                Options = Matched.SelectMany(c => c.Options).ToDictionary(p => p.Key, p => p.Value),
                LibDirectories = Matched.SelectMany(c => c.LibDirectories).Distinct().ToList(),
                Libs = Matched.SelectMany(c => c.Libs).Distinct().ToList(),
                LinkerFlags = Matched.SelectMany(c => c.LinkerFlags).ToList(),
                Files = Matched.SelectMany(c => c.Files).ToList(),
                OutputDirectory = Matched.Select(c => c.OutputDirectory).Where(v => v != null).LastOrDefault()
            };
            return conf;
        }
    }
}
