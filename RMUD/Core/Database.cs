﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Reflection;

namespace RMUD
{
    public static partial class Mud
    {
        public static String StaticPath { get; private set; }
        public static String DynamicPath { get; private set; }
        public static String AccountsPath { get; private set; }
        public static String ChatLogsPath { get; private set; }
        public static String WorldDatabaseURL { get; private set; }
        private static String UsingDeclarations = "using System;\nusing System.Collections.Generic;\nusing RMUD;\nusing System.Linq;\n";

        internal static Dictionary<String, MudObject> NamedObjects = new Dictionary<string, MudObject>();

        internal static void InitializeDatabase(String basePath)
        {
            StaticPath = basePath + "static/";
            DynamicPath = basePath + "dynamic/";
            AccountsPath = basePath + "accounts/";
            ChatLogsPath = basePath + "chatlogs/";

            WorldDatabaseURL = "https://raw.githubusercontent.com/Reddit-Mud/RMUD-DB/master/static/";
        }

        internal static String GetObjectRealPath(String Path)
        {
            return StaticPath + Path + ".cs";
        }
		
        internal static List<String> EnumerateLocalDatabase(String DirectoryPath)
        {
            var path = StaticPath + DirectoryPath;
            var r = new List<String>();
            foreach (var file in System.IO.Directory.EnumerateFiles(path))
                if (System.IO.Path.GetExtension(file) == ".cs")
                    r.Add(file.Substring(StaticPath.Length, file.Length - StaticPath.Length - 3).Replace("\\", "/"));
            foreach (var directory in System.IO.Directory.EnumerateDirectories(path))
                r.AddRange(EnumerateLocalDatabase(directory.Substring(StaticPath.Length)));
            return r;
        }

        internal static List<String> EnumerateGithubDatabase()
        {
            try
            {
                var githubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("Reddit-Mud"));

                var codeSearch = new Octokit.SearchCodeRequest(".cs")
                {
                    Repo = "Reddit-Mud/RMUD-DB",
                    In = new[] { Octokit.CodeInQualifier.Path },
                    Page = 1
                };

                var fileList = new List<String>();
                Octokit.SearchCodeResult codeResult = null;
                do
                {
                    codeResult = githubClient.Search.SearchCode(codeSearch).Result;
                    fileList.AddRange(codeResult.Items.Select(i => i.Path.Substring("static/".Length)));
                    codeSearch.Page += 1;
                } while (fileList.Count < codeResult.TotalCount);

                return fileList;
            }
            catch (Exception e)
            {
                Console.WriteLine("Github filelist discovery failed.");
                Console.WriteLine(e.Message);
                return new List<string>();
            }
        }

        private class FileTableEntry
        {
            public String Path;
            public int FirstLine;
        }

        private enum FileLocation
        {
            Local,
            Github
        }

        internal static void InitialBulkCompile(Action<String> ReportErrors)
        {
            if (NamedObjects.Count != 1) //That is, if anything besides Settings has been loaded...
                throw new InvalidOperationException("Bulk compilation must happen before any other objects are loaded or bad things happen.");


            //var test = new System.Net.WebClient();
            //var ts = test.DownloadString("https://raw.githubusercontent.com/Reddit-Mud/RMUD-DB/master/static/window.cs");

            var fileTable = new List<FileTableEntry>();
            var source = new StringBuilder();
            source.Append(UsingDeclarations + "namespace database {\n");
            int namespaceCount = 0;
            int lineCount = 4;

            var fileList = EnumerateLocalDatabase("");
            foreach (var s in fileList)
            {
                fileTable.Add(new FileTableEntry { Path = s, FirstLine = lineCount });
                lineCount += 4;
                source.AppendFormat("namespace __{0} {{\n", namespaceCount);
                namespaceCount += 1;
                var fileSource = LoadLocalSourceFile(StaticPath + s + ".cs", ReportErrors, null);
                lineCount += fileSource.Count(c => c == '\n');
                source.Append(fileSource);
                source.Append("\n}\n\n");

            }

            source.Append("}\n");

            //System.IO.File.WriteAllText("dump.txt", source.ToString());

            var combinedAssembly = CompileCode(source.ToString(), "/*", i =>
                {
                    var r = fileTable.Reverse<FileTableEntry>().FirstOrDefault(e => e.FirstLine < i);
                    if (r != null) return r.Path;
                    return "";
                });

            if (combinedAssembly != null)
            {
                namespaceCount = 0;
                foreach (var s in fileList)
                {
                    var qualifiedName = String.Format("database.__{0}.{1}", namespaceCount, System.IO.Path.GetFileNameWithoutExtension(s));
                    namespaceCount += 1;
                    var newObject = combinedAssembly.CreateInstance(qualifiedName) as MudObject;
                    if (newObject == null)
                    {
                        ReportErrors(String.Format("Type {0} not found in combined assembly.", qualifiedName));
                        return;
                    }

                    newObject.Path = s;
                    newObject.State = ObjectState.Unitialized;

                    NamedObjects.Upsert(s, newObject);
                }
            }
        }

        public static void SplitObjectName(String FullName, out String BasePath, out String InstanceName)
        {
            var split = FullName.IndexOf('@');
            if (split > 0)
            {
                BasePath = FullName.Substring(0, split);

                if (split < FullName.Length - 1)
                    InstanceName = FullName.Substring(split + 1);
                else
                    InstanceName = null;
            }
            else
            {
                BasePath = FullName;
                InstanceName = null;
            }
        }

        public static MudObject GetObject(String Path, Action<String> ReportErrors = null)
        {
            Path = Path.Replace('\\', '/');

            String BasePath, InstanceName;
            SplitObjectName(Path, out BasePath, out InstanceName);

            if (!String.IsNullOrEmpty(InstanceName))
            {
                MudObject activeInstance = null;
                if (ActiveInstances.TryGetValue(Path, out activeInstance))
                    return activeInstance;
                else
                    return CreateInstance(Path);
            }
            else
            {
                MudObject r = null;

                if (NamedObjects.ContainsKey(BasePath))
                    r = NamedObjects[BasePath];
                else
                {
                    r = LoadObject(BasePath, ReportErrors);
                    if (r != null) NamedObjects.Upsert(BasePath, r);
                }

                if (r != null && r.State == ObjectState.Unitialized)
                {
                    r.Initialize();
                    r.State = ObjectState.Alive;
                    r.HandleMarkedUpdate();
                }

                return r;
            }
        }
        
        public static String LoadLocalSourceFile(String Path, Action<String> ReportErrors, List<String> FilesLoaded)
        {
            Path = Path.Replace('\\', '/');

            if (!System.IO.File.Exists(Path))
            {
                LogError(String.Format("Could not find {0}", Path));
                if (ReportErrors != null) ReportErrors("Could not find " + Path);
                return "";
            }

            if (FilesLoaded == null) FilesLoaded = new List<string>();
            else if (FilesLoaded.Contains(Path))
            {
                //LogError(String.Format("Circular reference detected in {0}", Path));
                //if (ReportErrors != null) ReportErrors(String.Format("Circular reference detected in {0}", Path));
                return "";
            }

            FilesLoaded.Add(Path);

            var rawSource = new StringBuilder();

            var file = System.IO.File.OpenText(Path);
            while (!file.EndOfStream)
            {
                var line = file.ReadLine();
                if (line.StartsWith("//import"))
                {
                    var splitAt = line.IndexOf(' ');
                    if (splitAt < 0) continue;
                    var importedFilename = line.Substring(splitAt + 1);
                    rawSource.Append(LoadLocalSourceFile(GetObjectRealPath(importedFilename), ReportErrors, FilesLoaded));
                    rawSource.AppendLine();
                }
                else
                {
                    rawSource.Append(line);
                    rawSource.AppendLine();
                }
            }

            return rawSource.ToString();
        }

        public static String LoadRawLocalSourceFile(String Path)
        {
            Path = Path.Replace('\\', '/');
            if (Path.Contains("..")) return "Backtrack path entries are not permitted.";
            var realPath = StaticPath + Path + ".cs";

            if (!System.IO.File.Exists(realPath)) return "File not found.";
            return System.IO.File.ReadAllText(realPath);
        }

        public static Assembly CompileCode(String Source, String ErrorPath, Func<int,String> TranslateBulkFilenames = null)
        {
            CodeDomProvider codeProvider = CodeDomProvider.CreateProvider("CSharp");

            var parameters = new CompilerParameters();
            parameters.GenerateInMemory = true;
            parameters.GenerateExecutable = false;
            parameters.ReferencedAssemblies.Add("RMUD.exe");

            parameters.ReferencedAssemblies.Add("mscorlib.dll");
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("System.Data.Linq.dll");
            parameters.ReferencedAssemblies.Add("System.Data.Entity.dll");

            CompilerResults compilationResults = codeProvider.CompileAssemblyFromSource(parameters, Source);
            bool realError = false;
            if (compilationResults.Errors.Count > 0)
            {
                var errorString = new StringBuilder();
                errorString.AppendLine(String.Format("{0} errors in {1}", compilationResults.Errors.Count, ErrorPath));

                foreach (var error in compilationResults.Errors)
                {
                    var cError = error as System.CodeDom.Compiler.CompilerError;
                    if (!cError.IsWarning) realError = true;

                    var filename = cError.FileName;
                    if (TranslateBulkFilenames != null)
                        filename = TranslateBulkFilenames(cError.Line);

                    errorString.Append(filename + " : " + error.ToString());
                    errorString.AppendLine();
                }
                LogError(errorString.ToString());
            }

            if (realError) return null;
            return compilationResults.CompiledAssembly;
        }

		public static Assembly LoadAndCompileLocalFile(String Path, Action<String> ReportErrors)
		{
            var start = DateTime.Now;
            var fileSource = LoadLocalSourceFile(Path, ReportErrors, new List<String>());
            if (String.IsNullOrEmpty(fileSource)) return null;

            var source = UsingDeclarations + fileSource;

            var assembly = CompileCode(source, Path, i => Path);

            LogError(String.Format("Compiled {0} in {1} milliseconds.", Path, (DateTime.Now - start).TotalMilliseconds));

			return assembly;
		}

        private static MudObject LoadObject(String Path, Action<String> ReportErrors)
        {
            Path = Path.Replace('\\', '/');

            var staticObjectPath = StaticPath + Path + ".cs";
			var assembly = LoadAndCompileLocalFile(staticObjectPath, ReportErrors);
			if (assembly == null) return null;

			var objectLeafName = System.IO.Path.GetFileNameWithoutExtension(Path);
			var newMudObject = assembly.CreateInstance(objectLeafName) as MudObject;
			if (newMudObject != null)
			{
				newMudObject.Path = Path;
                newMudObject.State = ObjectState.Unitialized;
				return newMudObject;
			}
			else
			{
                LogError(String.Format("Type {0} not found in {1}", objectLeafName, staticObjectPath));
				return null;
			}
		}

		internal static MudObject ReloadObject(String Path, Action<String> ReportErrors)
		{
            Path = Path.Replace('\\', '/');

			if (NamedObjects.ContainsKey(Path))
			{
				var existing = NamedObjects[Path];
				var newObject = LoadObject(Path, ReportErrors);
				if (newObject == null)  return null;

                existing.State = ObjectState.Destroyed;
				NamedObjects.Upsert(Path, newObject);
                newObject.Initialize(); 
                newObject.State = ObjectState.Alive;
				newObject.HandleMarkedUpdate();

				//Preserve contents
				if (existing is Container && newObject is Container)
				{
                    (existing as Container).EnumerateObjects(RelativeLocations.EveryMudObject, (MudObject, loc) =>
                        {
                            (newObject as Container).Add(MudObject, loc);
                            if (MudObject is MudObject) (MudObject as MudObject).Location = newObject;
                            return EnumerateObjectsControl.Continue;
                        });
				}

				//Preserve location
				if (existing is MudObject && newObject is MudObject)
				{
					if ((existing as MudObject).Location != null)
					{
                        var loc = ((existing as MudObject).Location as Container).LocationOf(existing);
						MudObject.Move(newObject as MudObject, (existing as MudObject).Location, loc);
						MudObject.Move(existing as MudObject, null, RelativeLocations.None);
					}
				}

                existing.Destroy(false);

				return newObject;
			}
			else
				return GetObject(Path, ReportErrors);
		}

        internal static bool ResetObject(String Path, Action<String> ReportErrors)
        {
            Path = Path.Replace('\\', '/');

            if (NamedObjects.ContainsKey(Path))
            {
                var existing = NamedObjects[Path];
                existing.State = ObjectState.Destroyed;
                
                var newObject = Activator.CreateInstance(existing.GetType()) as MudObject;
                NamedObjects.Upsert(Path, newObject);
                newObject.Initialize();
                newObject.State = ObjectState.Alive;
                newObject.HandleMarkedUpdate();

                //Preserve the location of actors, and actors only.
                if (existing is Container)
                {
                    (existing as Container).EnumerateObjects(RelativeLocations.EveryMudObject, (MudObject, loc) =>
                        {
                            if (MudObject is Actor)
                            {
                                //Can't use MudObject.Move - it will change the list we are iterating.
                                (newObject as Container).Add(MudObject, loc);
                                (MudObject as MudObject).Location = newObject;
                            }
                            else
                            {
                                MudObject.Destroy(true);
                            }
                            return EnumerateObjectsControl.Continue;
                        });
                }

                if (existing is MudObject && (existing as MudObject).Location != null)
                {
                    var loc = ((existing as MudObject).Location as Container).LocationOf(existing);
                    MudObject.Move(newObject as MudObject, (existing as MudObject).Location, loc);
                    MudObject.Move(existing as MudObject, null, RelativeLocations.None);
                }

                existing.Destroy(false);

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
