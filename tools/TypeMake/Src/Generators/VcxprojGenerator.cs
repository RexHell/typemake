﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TypeMake.Cpp
{
    public class VcxprojGenerator
    {
        private Project Project;
        private String ProjectId;
        private List<ProjectReference> ProjectReferences;
        private PathString InputDirectory;
        private PathString OutputDirectory;
        private String VcxprojTemplateText;
        private String VcxprojFilterTemplateText;
        private OperatingSystemType BuildingOperatingSystem;
        private ArchitectureType BuildingOperatingSystemArchitecture;
        private OperatingSystemType TargetOperatingSystem;

        public VcxprojGenerator(Project Project, String ProjectId, List<ProjectReference> ProjectReferences, PathString InputDirectory, PathString OutputDirectory, String VcxprojTemplateText, String VcxprojFilterTemplateText, OperatingSystemType BuildingOperatingSystem, ArchitectureType BuildingOperatingSystemArchitecture, OperatingSystemType TargetOperatingSystem)
        {
            this.Project = Project;
            this.ProjectId = ProjectId;
            this.ProjectReferences = ProjectReferences;
            this.InputDirectory = InputDirectory.FullPath;
            this.OutputDirectory = OutputDirectory.FullPath;
            this.VcxprojTemplateText = VcxprojTemplateText;
            this.VcxprojFilterTemplateText = VcxprojFilterTemplateText;
            this.BuildingOperatingSystem = BuildingOperatingSystem;
            this.BuildingOperatingSystemArchitecture = BuildingOperatingSystemArchitecture;
            this.TargetOperatingSystem = TargetOperatingSystem;
        }

        public void Generate(bool ForceRegenerate)
        {
            GenerateVcxproj(ForceRegenerate);
            GenerateVcxprojFilters(ForceRegenerate);
        }

        private void GenerateVcxproj(bool ForceRegenerate)
        {
            var VcxprojPath = OutputDirectory / (Project.Name + ".vcxproj");
            var BaseDirPath = VcxprojPath.Parent;

            var xVcxproj = XmlFile.FromString(VcxprojTemplateText);
            Trim(xVcxproj);

            var xn = xVcxproj.Name.Namespace;

            foreach (var ig in xVcxproj.Elements(xn + "ItemGroup").ToArray())
            {
                if (ig.Attribute("Label") != null) { continue; }

                var None = ig.Elements().Where(e => e.Name == xn + "None").ToArray();
                var ClInclude = ig.Elements().Where(e => e.Name == xn + "ClInclude").ToArray();
                var ClCompile = ig.Elements().Where(e => e.Name == xn + "ClCompile").ToArray();
                var ProjectReference = ig.Elements().Where(e => e.Name == xn + "ProjectReference").ToArray();
                foreach (var e in None)
                {
                    e.Remove();
                }
                foreach (var e in ClInclude)
                {
                    e.Remove();
                }
                foreach (var e in ClCompile)
                {
                    e.Remove();
                }
                foreach (var e in ProjectReference)
                {
                    e.Remove();
                }
                if (!ig.HasElements && !ig.HasAttributes)
                {
                    ig.Remove();
                }
            }

            var GlobalsPropertyGroup = xVcxproj.Elements(xn + "PropertyGroup").Where(e => e.Attribute("Label") != null && e.Attribute("Label").Value == "Globals").FirstOrDefault();
            if (GlobalsPropertyGroup == null)
            {
                GlobalsPropertyGroup = new XElement(xn + "PropertyGroup", new XAttribute("Label", "Globals"));
                xVcxproj.Add(GlobalsPropertyGroup);
            }
            var g = "{" + ProjectId.ToUpper() + "}";
            GlobalsPropertyGroup.SetElementValue(xn + "ProjectGuid", g);
            GlobalsPropertyGroup.SetElementValue(xn + "RootNamespace", Project.Name);

            var ExistingConfigurationTypeAndArchitectures = new Dictionary<KeyValuePair<ConfigurationType, ArchitectureType>, String>();
            var ProjectConfigurations = xVcxproj.Elements(xn + "ItemGroup").Where(e => (e.Attribute("Label") != null) && (e.Attribute("Label").Value == "ProjectConfigurations")).SelectMany(e => e.Elements(xn + "ProjectConfiguration")).Select(e => e.Element(xn + "Configuration").Value + "|" + e.Element(xn + "Platform").Value).ToDictionary(s => s);
            foreach (var Architecture in Enum.GetValues(typeof(ArchitectureType)).Cast<ArchitectureType>())
            {
                foreach (var ConfigurationType in Enum.GetValues(typeof(ConfigurationType)).Cast<ConfigurationType>())
                {
                    var Name = ConfigurationType.ToString() + "|" + GetArchitectureString(Architecture);
                    if (ProjectConfigurations.ContainsKey(Name))
                    {
                        ExistingConfigurationTypeAndArchitectures.Add(new KeyValuePair<ConfigurationType, ArchitectureType>(ConfigurationType, Architecture), Name);
                    }
                }
            }

            foreach (var Pair in ExistingConfigurationTypeAndArchitectures)
            {
                var ConfigurationType = Pair.Key.Key;
                var Architecture = Pair.Key.Value;
                var Name = Pair.Value;

                var conf = Project.Configurations.Merged(ToolchainType.Windows_VisualC, CompilerType.VisualC, BuildingOperatingSystem, BuildingOperatingSystemArchitecture, TargetOperatingSystem, Architecture, ConfigurationType);

                var PropertyGroup = xVcxproj.Elements(xn + "PropertyGroup").Where(e => (e.Attribute("Condition") != null) && (e.Attribute("Condition").Value == "'$(Configuration)|$(Platform)'=='" + Name + "'")).LastOrDefault();
                if (PropertyGroup == null)
                {
                    PropertyGroup = new XElement(xn + "PropertyGroup", new XAttribute("Condition", "'$(Configuration)|$(Platform)'=='" + Name + "'"));
                }

                if (!String.IsNullOrEmpty(Project.TargetName) && (Project.TargetName != Project.Name))
                {
                    PropertyGroup.SetElementValue(xn + "TargetName", Project.TargetName);
                }
                if (conf.TargetType == TargetType.Executable)
                {
                    PropertyGroup.SetElementValue(xn + "ConfigurationType", "Application");
                }
                else if (conf.TargetType == TargetType.StaticLibrary)
                {
                    PropertyGroup.SetElementValue(xn + "ConfigurationType", "StaticLibrary");
                }
                else if (conf.TargetType == TargetType.DynamicLibrary)
                {
                    PropertyGroup.SetElementValue(xn + "ConfigurationType", "DynamicLibrary");
                }
                else
                {
                    throw new NotSupportedException("NotSupportedTargetType: " + conf.TargetType.ToString());
                }

                var ItemDefinitionGroup = xVcxproj.Elements(xn + "ItemDefinitionGroup").Where(e => (e.Attribute("Condition") != null) && (e.Attribute("Condition").Value == "'$(Configuration)|$(Platform)'=='" + Name + "'")).LastOrDefault();
                if (ItemDefinitionGroup == null)
                {
                    ItemDefinitionGroup = new XElement(xn + "ItemDefinitionGroup", new XAttribute("Condition", "'$(Configuration)|$(Platform)'=='" + Name + "'"));
                    xVcxproj.Add(ItemDefinitionGroup);
                }
                var ClCompile = ItemDefinitionGroup.Element(xn + "ClCompile");
                if (ClCompile == null)
                {
                    ClCompile = new XElement(xn + "ClCompile");
                    ItemDefinitionGroup.Add(ClCompile);
                }
                var IncludeDirectories = conf.IncludeDirectories.Select(d => d.FullPath.RelativeTo(BaseDirPath).ToString(PathStringStyle.Windows)).ToList();
                if (IncludeDirectories.Count != 0)
                {
                    ClCompile.SetElementValue(xn + "AdditionalIncludeDirectories", String.Join(";", IncludeDirectories) + ";%(AdditionalIncludeDirectories)");
                }
                var Defines = conf.Defines;
                if (Defines.Count != 0)
                {
                    ClCompile.SetElementValue(xn + "PreprocessorDefinitions", String.Join(";", Defines.Select(d => d.Key + (d.Value == null ? "" : "=" + (Regex.IsMatch(d.Value, @"^[0-9]+$") ? d.Value : "\"" + d.Value.Replace("\"", "") + "\"")))) + ";%(PreprocessorDefinitions)");
                }
                var CompilerFlags = conf.CFlags.Concat(conf.CppFlags).ToList();
                if (CompilerFlags.Count != 0)
                {
                    ClCompile.SetElementValue(xn + "AdditionalOptions", "%(AdditionalOptions) " + String.Join(" ", CompilerFlags.Select(f => (f == null ? "" : Regex.IsMatch(f, @"^[0-9]+$") ? f : "\"" + f.Replace("\"", "\"\"\"") + "\""))));
                }

                if ((conf.TargetType == TargetType.Executable) || (conf.TargetType == TargetType.DynamicLibrary))
                {
                    var Link = ItemDefinitionGroup.Element(xn + "Link");
                    if (Link == null)
                    {
                        Link = new XElement(xn + "Link");
                        ItemDefinitionGroup.Add(ClCompile);
                    }
                    var LibDirectories = conf.LibDirectories.Select(d => d.FullPath.RelativeTo(BaseDirPath).ToString(PathStringStyle.Windows)).ToList();
                    if (LibDirectories.Count != 0)
                    {
                        Link.SetElementValue(xn + "AdditionalLibraryDirectories", String.Join(";", LibDirectories) + ";%(AdditionalLibraryDirectories)");
                    }
                    var Libs = conf.Libs.Select(lib => lib.ToString(PathStringStyle.Windows)).Concat(ProjectReferences.Select(p => p.Name + ".lib")).ToList();
                    if (Libs.Count != 0)
                    {
                        Link.SetElementValue(xn + "AdditionalDependencies", String.Join(";", Libs) + ";%(AdditionalDependencies)");
                    }
                    var LinkerFlags = conf.LinkerFlags.ToList();
                    if (LinkerFlags.Count != 0)
                    {
                        ClCompile.SetElementValue(xn + "AdditionalOptions", "%(AdditionalOptions) " + String.Join(" ", LinkerFlags.Select(f => (f == null ? "" : Regex.IsMatch(f, @"^[0-9]+$") ? f : "\"" + f.Replace("\"", "\"\"") + "\""))));
                    }
                }
            }

            var Import = xVcxproj.Elements(xn + "Import").LastOrDefault();

            foreach (var conf in Project.Configurations.Matches(ToolchainType.Windows_VisualC, CompilerType.VisualC, BuildingOperatingSystem, BuildingOperatingSystemArchitecture, TargetOperatingSystem, null, null))
            {
                var Conditions = new List<String>();
                if ((conf.MatchingConfigurationTypes != null) || (conf.MatchingTargetArchitectures != null))
                {
                    var Keys = "";
                    var Values = new List<String> { "" };
                    if (conf.MatchingConfigurationTypes != null)
                    {
                        Keys = (Keys != "" ? Keys + "|" : "") +  "$(Configuration)";
                        Values = conf.MatchingConfigurationTypes.SelectMany(t => Values.Select(v => (v != "" ? v + "|" : "") + t.ToString())).ToList();
                    }
                    if (conf.MatchingTargetArchitectures != null)
                    {
                        Keys = (Keys != "" ? Keys + "|" : "") + "$(Platform)";
                        Values = conf.MatchingTargetArchitectures.SelectMany(a => Values.Select(v => (v != "" ? v + "|" : "") + GetArchitectureString(a))).ToList();
                    }
                    Conditions = Values.Select(v => "'" + Keys + "' == '" + v + "'").ToList();
                }
                else
                {
                    Conditions = new List<string> { null };
                }

                foreach (var Condition in Conditions)
                {
                    var FileItemGroup = new XElement(xn + "ItemGroup");
                    if (Import != null)
                    {
                        Import.AddBeforeSelf(FileItemGroup);
                    }
                    else
                    {
                        xVcxproj.Add(FileItemGroup);
                    }
                    if (Condition != null)
                    {
                        FileItemGroup.Add(new XAttribute("Condition", Condition));
                    }
                    foreach (var f in conf.Files)
                    {
                        var RelativePath = f.Path.FullPath.RelativeTo(BaseDirPath).ToString(PathStringStyle.Windows);
                        XElement x;
                        if (f.Type == FileType.Header)
                        {
                            x = new XElement(xn + "ClInclude", new XAttribute("Include", RelativePath));
                        }
                        else if (f.Type == FileType.CSource)
                        {
                            x = new XElement(xn + "ClCompile", new XAttribute("Include", RelativePath));
                        }
                        else if (f.Type == FileType.CppSource)
                        {
                            x = new XElement(xn + "ClCompile", new XAttribute("Include", RelativePath));
                            x.Add(new XElement(xn + "ObjectFileName", "$(IntDir)" + RelativePath.Replace("..", "__") + ".obj"));
                        }
                        else
                        {
                            x = new XElement(xn + "None", new XAttribute("Include", RelativePath));
                        }
                        FileItemGroup.Add(x);
                    }
                    if (!FileItemGroup.HasElements)
                    {
                        FileItemGroup.Remove();
                    }
                }
            }

            var ProjectItemGroup = new XElement(xn + "ItemGroup");
            if (Import != null)
            {
                Import.AddBeforeSelf(ProjectItemGroup);
            }
            else
            {
                xVcxproj.Add(ProjectItemGroup);
            }
            foreach (var p in ProjectReferences)
            {
                var RelativePath = p.FilePath.FullPath.RelativeTo(BaseDirPath).ToString(PathStringStyle.Windows);
                var x = new XElement(xn + "ProjectReference", new XAttribute("Include", RelativePath));
                x.Add(new XElement(xn + "Project", "{" + p.Id.ToUpper() + "}"));
                x.Add(new XElement(xn + "Name", "{" + p.Name + "}"));
                ProjectItemGroup.Add(x);
            }
            if (!ProjectItemGroup.HasElements)
            {
                ProjectItemGroup.Remove();
            }

            var sVcxproj = XmlFile.ToString(xVcxproj);
            TextFile.WriteToFile(VcxprojPath, sVcxproj, Encoding.UTF8, !ForceRegenerate);
        }

        private void GenerateVcxprojFilters(bool ForceRegenerate)
        {
            var FilterPath = OutputDirectory / (Project.Name + ".vcxproj.filters");
            var BaseDirPath = FilterPath.Parent;

            var xFilter = XmlFile.FromString(VcxprojFilterTemplateText);
            Trim(xFilter);

            var xn = xFilter.Name.Namespace;

            foreach (var ig in xFilter.Elements(xn + "ItemGroup").ToArray())
            {
                if (ig.Attribute("Label") != null) { continue; }

                var None = ig.Elements().Where(e => e.Name == xn + "None").ToArray();
                var ClInclude = ig.Elements().Where(e => e.Name == xn + "ClInclude").ToArray();
                var ClCompile = ig.Elements().Where(e => e.Name == xn + "ClCompile").ToArray();
                var Filter = ig.Elements().Where(e => e.Name == xn + "Filter").ToArray();
                foreach (var e in None)
                {
                    e.Remove();
                }
                foreach (var e in ClInclude)
                {
                    e.Remove();
                }
                foreach (var e in ClCompile)
                {
                    e.Remove();
                }
                foreach (var e in Filter)
                {
                    e.Remove();
                }
                if (!ig.HasElements && !ig.HasAttributes)
                {
                    ig.Remove();
                }
            }

            var FilterItemGroup = new XElement(xn + "ItemGroup");
            var FileItemGroup = new XElement(xn + "ItemGroup");
            xFilter.Add(FilterItemGroup);
            xFilter.Add(FileItemGroup);

            var Files = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            var Filters = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            foreach (var conf in Project.Configurations.Matches(ToolchainType.Windows_VisualC, CompilerType.VisualC, BuildingOperatingSystem, BuildingOperatingSystemArchitecture, TargetOperatingSystem, null, null))
            {
                foreach (var f in conf.Files)
                {
                    if (Files.Contains(f.Path)) { continue; }
                    Files.Add(f.Path);

                    var RelativePath = f.Path.FullPath.RelativeTo(BaseDirPath).ToString(PathStringStyle.Windows);
                    var Dir = f.Path.FullPath.RelativeTo(InputDirectory).Parent.ToString(PathStringStyle.Windows);
                    while (Dir.StartsWith(@"..\"))
                    {
                        Dir = Dir.Substring(3);
                    }
                    if (!Filters.Contains(Dir))
                    {
                        var CurrentDir = Dir.AsPath();
                        var CurrentDirFilter = CurrentDir.ToString(PathStringStyle.Windows);
                        while ((CurrentDirFilter != ".") && !Filters.Contains(CurrentDirFilter))
                        {
                            Filters.Add(CurrentDirFilter);
                            CurrentDir = CurrentDir.Parent;
                            CurrentDirFilter = CurrentDir.ToString(PathStringStyle.Windows);
                        }
                    }

                    XElement x;
                    if (f.Type == FileType.Header)
                    {
                        x = new XElement(xn + "ClInclude", new XAttribute("Include", RelativePath));
                    }
                    else if (f.Type == FileType.CSource)
                    {
                        x = new XElement(xn + "ClCompile", new XAttribute("Include", RelativePath));
                    }
                    else if (f.Type == FileType.CppSource)
                    {
                        x = new XElement(xn + "ClCompile", new XAttribute("Include", RelativePath));
                    }
                    else
                    {
                        x = new XElement(xn + "None", new XAttribute("Include", RelativePath));
                    }
                    if (Dir != ".")
                    {
                        x.Add(new XElement(xn + "Filter", Dir));
                    }
                    FileItemGroup.Add(x);
                }
            }

            foreach (var f in Filters.OrderBy(ff => ff, StringComparer.OrdinalIgnoreCase))
            {
                var g = Guid.ParseExact(Hash.GetHashForPath(f, 32), "N").ToString().ToUpper();
                FilterItemGroup.Add(new XElement(xn + "Filter", new XAttribute("Include", f), new XElement(xn + "UniqueIdentifier", "{" + g + "}")));
            }

            if (!FilterItemGroup.HasElements)
            {
                FilterItemGroup.Remove();
            }
            if (!FileItemGroup.HasElements)
            {
                FileItemGroup.Remove();
            }

            var sFilter = XmlFile.ToString(xFilter);
            TextFile.WriteToFile(FilterPath, sFilter, Encoding.UTF8, !ForceRegenerate);
        }

        private static void Trim(XElement x)
        {
            var TextNodes = x.DescendantNodesAndSelf().Where(n => n.NodeType == XmlNodeType.Text).Select(n => (XText)(n)).ToArray();
            var rWhitespace = new Regex(@"^\s*$", RegexOptions.ExplicitCapture);
            foreach (var tn in TextNodes)
            {
                if (rWhitespace.Match(tn.Value).Success)
                {
                    if (!(tn.Parent != null && !tn.Parent.HasElements))
                    {
                        tn.Remove();
                    }
                }
            }
        }

        public static String GetArchitectureString(ArchitectureType Architecture)
        {
            if (Architecture == ArchitectureType.x86)
            {
                return "Win32";
            }
            else if (Architecture == ArchitectureType.x86_64)
            {
                return "x64";
            }
            else if (Architecture == ArchitectureType.armeabi_v7a)
            {
                return "ARM";
            }
            else if (Architecture == ArchitectureType.arm64_v8a)
            {
                return "ARM64";
            }
            else
            {
                throw new NotSupportedException("NotSupportedArchitecture: " + Architecture.ToString());
            }
        }
    }
}