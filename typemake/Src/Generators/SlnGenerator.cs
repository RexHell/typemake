﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using TypeMake.Cpp;

namespace TypeMake
{
    public class SlnGenerator
    {
        private String SolutionName;
        private String SolutionId;
        private List<ProjectReference> ProjectReferences;
        private String OutputDirectory;
        private String SlnTemplateText;

        public SlnGenerator(String SolutionName, String SolutionId, List<ProjectReference> ProjectReferences, String OutputDirectory, String SlnTemplateText)
        {
            this.SolutionName = SolutionName;
            this.SolutionId = SolutionId;
            this.ProjectReferences = ProjectReferences;
            this.OutputDirectory = Path.GetFullPath(OutputDirectory);
            this.SlnTemplateText = SlnTemplateText;
        }

        public void Generate(bool ForceRegenerate)
        {
            var s = new SlnFile();
            s.FullPath = Path.Combine(OutputDirectory, SolutionName + ".sln");
            using (var sr = new StringReader(SlnTemplateText))
            {
                s.Read(sr);
            }

            s.Projects.Clear();
            s.ProjectConfigurationsSection.Clear();
            SlnSection NestedProjects = null;
            foreach (var Section in s.Sections.Where(Section => Section.Id == "NestedProjects"))
            {
                Section.Clear();
                NestedProjects = Section;
            }

            if (NestedProjects == null)
            {
                NestedProjects = new SlnSection
                {
                    Id = "NestedProjects"
                };
                s.Sections.Add(NestedProjects);
            }

            var Filters = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var Project in ProjectReferences)
            {
                var Dir = Project.VirtualDir.Replace('/', '\\');
                if (!Filters.ContainsKey(Dir))
                {
                    var CurrentDir = Dir;
                    while (!String.IsNullOrEmpty(CurrentDir) && !Filters.ContainsKey(CurrentDir))
                    {
                        var g = Guid.ParseExact(Hash.GetHashForPath(CurrentDir, 32), "N").ToString().ToUpper();
                        Filters.Add(CurrentDir, g);
                        CurrentDir = Path.GetDirectoryName(CurrentDir);
                        if (!String.IsNullOrEmpty(CurrentDir))
                        {
                            var gUpper = Guid.ParseExact(Hash.GetHashForPath(CurrentDir, 32), "N").ToString().ToUpper();
                            NestedProjects.Properties.SetValue("{" + g + "}", "{" + gUpper + "}");
                        }
                    }
                }

                s.Projects.Add(new SlnProject
                {
                    TypeGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                    Name = Project.Name,
                    FilePath = FileNameHandling.GetRelativePath(Path.GetFullPath(Project.FilePath), OutputDirectory),
                    Id = "{" + Project.Id + "}"
                });

                var conf = new SlnPropertySet("{" + Project.Id + "}");
                foreach (var c in s.SolutionConfigurationsSection)
                {
                    var Value = c.Value.Replace("|x86", "|Win32");
                    conf.SetValue(c.Key + ".ActiveCfg", Value);
                    conf.SetValue(c.Key + ".Build.0", Value);
                }
                s.ProjectConfigurationsSection.Add(conf);

                NestedProjects.Properties.SetValue("{" + Project.Id + "}", "{" + Filters[Dir] + "}");
            }

            foreach (var f in Filters)
            {
                s.Projects.Add(new SlnProject
                {
                    TypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}",
                    Name = Path.GetFileName(f.Key),
                    FilePath = Path.GetFileName(f.Key),
                    Id = "{" + f.Value + "}"
                });
            }

            foreach (var Section in s.Sections.Where(Section => Section.Id == "ExtensibilityGlobals"))
            {
                Section.Properties.SetValue("SolutionGuid", "{" + SolutionId.ToUpper() + "}");
            }

            String Text;
            using (var sw = new StringWriter())
            {
                s.Write(sw);
                Text = sw.ToString();
            }
            TextFile.WriteToFile(s.FullPath, Text, Encoding.UTF8, !ForceRegenerate);
        }

        public static String GetArchitectureString(ArchitectureType Architecture)
        {
            if (Architecture == ArchitectureType.x86)
            {
                return "x86";
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
