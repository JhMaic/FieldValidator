#if TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace FieldValidator;

// The class is marked as partial, acting as both an EditorScript (manual run) and a logic library.
[Tool]
public partial class FieldValidatorScript : EditorScript
{
    // ================= Configuration =================
    private const string IgnoreFileName = "res://.fieldignore";
    private static readonly List<string> _ignoredPaths = new();

    // Default paths to ignore if the file is newly created
    private static readonly string[] DefaultIgnores =
    {
        ".godot/",
        ".vs/",
        ".vscode/",
        "addons/"
    };
    // =================================================

    // Cache
    private static readonly Dictionary<string, List<ValidationMember>> _scriptPathToRules = new();
    private static readonly HashSet<ulong> _visitedInstanceIds = new();

    /// <summary>
    ///     [Entry 1] Manual run from Editor (File -> Run)
    /// </summary>
    public override void _Run()
    {
        RunFullValidation(true);
    }

    /// <summary>
    ///     [Entry 2] Called by EditorPlugin. Returns true if validation passes.
    /// </summary>
    public static bool RunFullValidation(bool printSuccess = false)
    {
        // Initialization/Cleanup
        _visitedInstanceIds.Clear();
        _scriptPathToRules.Clear();
        LoadIgnoreConfig();

        if (printSuccess)
            GD.PrintRich("[b][color=magenta]=== 🚀 Manual Full Validation ===[/color][/b]");
        else
            GD.Print("Performing pre-run consistency check...");

        var startTime = DateTime.Now;
        var errorCount = 0;
        var scannedFiles = 0;

        var allFiles = GetAllResourcePaths("res://");

        foreach (var filePath in allFiles)
        {
            if (IsPathIgnored(filePath))
                continue;

            // Exclude non-resource files
            if (filePath.EndsWith(".cs") || filePath.EndsWith(".gdshader") || filePath.EndsWith(".txt") ||
                filePath.EndsWith(".json"))
                continue;

            try
            {
                var res = ResourceLoader.Load(filePath);
                if (res == null)
                    continue;
                scannedFiles++;

                if (res is PackedScene packedScene)
                {
                    var rootNode = packedScene.Instantiate();
                    if (rootNode == null)
                        continue;
                    try
                    {
                        errorCount += ValidateNodeRecursive(rootNode, filePath);
                    }
                    finally
                    {
                        rootNode.Free();
                    }
                }
                else
                {
                    errorCount += ValidateObject(res, filePath, $"[Resource: {filePath}]");
                }
            }
            catch (Exception e)
            {
                GD.PushWarning($"[Skipped] {filePath}: {e.Message}");
            }
        }

        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        if (errorCount > 0)
        {
            GD.PrintRich(
                $"[b][color=red]❌ Validation Failed! Found {errorCount} null reference errors. Execution blocked.[/color][/b]");
            GD.PrintRich($"[b][color=red]Duration: {duration:F2}ms[/color][/b]");
            return false;
        }

        if (printSuccess)
            GD.PrintRich(
                $"[b][color=green]✅ Validation Passed. Scanned files: {scannedFiles} | Duration: {duration:F2}ms[/color][/b]");

        return true;
    }

    private static void LoadIgnoreConfig()
    {
        _ignoredPaths.Clear();

        if (!FileAccess.FileExists(IgnoreFileName))
        {
            using var fileWrite = FileAccess.Open(IgnoreFileName, FileAccess.ModeFlags.Write);
            if (fileWrite != null)
            {
                fileWrite.StoreLine("# Add paths to ignore here (one per line)");
                foreach (var path in DefaultIgnores)
                    fileWrite.StoreLine(path);
            }
        }

        using var fileRead = FileAccess.Open(IgnoreFileName, FileAccess.ModeFlags.Read);
        if (fileRead != null)
            while (fileRead.GetPosition() < fileRead.GetLength())
            {
                var line = fileRead.GetLine().Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Ensure res:// prefix for consistency
                if (!line.StartsWith("res://"))
                    line = "res://" + line;

                _ignoredPaths.Add(line);
            }
    }

    private static bool IsPathIgnored(string path)
    {
        foreach (var ignore in _ignoredPaths)
        {
            if (path == ignore)
                return true;
            var folderIgnore = ignore.EndsWith("/") ? ignore : ignore + "/";
            if (path.StartsWith(folderIgnore))
                return true;
        }

        return false;
    }

    private static List<string> GetAllResourcePaths(string path)
    {
        var files = new List<string>();
        if (IsPathIgnored(path))
            return files;

        using var dir = DirAccess.Open(path);
        if (dir != null)
        {
            dir.ListDirBegin();
            var fileName = dir.GetNext();
            while (fileName != "")
            {
                var fullPath = path == "res://" ? path + fileName : path + "/" + fileName;
                if (dir.CurrentIsDir())
                {
                    if (fileName != "." && fileName != ".." && fileName != ".godot" && fileName != ".vs" &&
                        fileName != ".vscode")
                        if (!IsPathIgnored(fullPath))
                            files.AddRange(GetAllResourcePaths(fullPath));
                }
                else
                {
                    if (!IsPathIgnored(fullPath))
                        if (fileName.EndsWith(".tscn") || fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                            files.Add(fullPath);
                }

                fileName = dir.GetNext();
            }
        }

        return files;
    }

    private static int ValidateNodeRecursive(Node node, string sourceFile)
    {
        var errors = 0;
        var context = $"[Scene: {sourceFile}] :: [Node: {node.Name}]";
        errors += ValidateObject(node, sourceFile, context);
        foreach (var child in node.GetChildren())
            errors += ValidateNodeRecursive(child, sourceFile);
        return errors;
    }

    private static int ValidateObject(GodotObject obj, string filePath, string context)
    {
        if (obj == null)
            return 0;
        var id = obj.GetInstanceId();
        if (_visitedInstanceIds.Contains(id))
            return 0;
        _visitedInstanceIds.Add(id);

        var scriptVariant = obj.GetScript();
        var csharpScript = scriptVariant.Obj as CSharpScript;
        if (csharpScript == null)
            return 0;

        var rules = GetRulesFromScript(csharpScript);
        if (rules.Count == 0)
            return 0;

        var localErrors = 0;

        foreach (var rule in rules)
        {
            var valueVariant = obj.Get(rule.MemberName);
            var rawValue = valueVariant.Obj;

            if (rule.IsMustSet)
                if (IsGodotNull(rawValue))
                {
                    PrintValidationError(context, rule.MemberName, "Assignment required (MustSet)");
                    localErrors++;
                    continue;
                }

            if (rule.IsMemberMustSet)
            {
                if (IsGodotNull(rawValue))
                    continue;
                var collection = rawValue as IEnumerable;
                if (collection != null)
                {
                    var index = 0;
                    foreach (var item in collection)
                    {
                        var realItem = item;
                        if (item is Variant vItem)
                            realItem = vItem.Obj;

                        if (IsGodotNull(realItem))
                        {
                            PrintValidationError(context, rule.MemberName,
                                $"Array element at [{index}] is null (MemberMustSet)");
                            localErrors++;
                        }
                        else if (realItem is Resource resItem)
                        {
                            localErrors += ValidateObject(resItem, filePath, $"{context}->{rule.MemberName}[{index}]");
                        }

                        index++;
                    }
                }
            }
            else if (!IsGodotNull(rawValue) && rawValue is Resource subRes)
            {
                localErrors += ValidateObject(subRes, filePath, $"{context}->{rule.MemberName}");
            }
        }

        return localErrors;
    }

    private static List<ValidationMember> GetRulesFromScript(CSharpScript script)
    {
        var path = script.ResourcePath;
        if (_scriptPathToRules.TryGetValue(path, out var cachedRules))
            return cachedRules;

        var newRules = new List<ValidationMember>();
        try
        {
            var dummyInstance = (GodotObject)script.New();
            if (dummyInstance != null)
                try
                {
                    var realType = dummyInstance.GetType();
                    var currentType = realType;
                    while (currentType != null && currentType != typeof(GodotObject) && currentType != typeof(object))
                    {
                        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.DeclaredOnly;
                        var members = currentType.GetMembers(flags)
                            .Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property);

                        foreach (var m in members)
                        {
                            if (!Attribute.IsDefined(m, typeof(ExportAttribute)))
                                continue;
                            var must = Attribute.IsDefined(m, typeof(MustSetAttribute));
                            var memberMust = Attribute.IsDefined(m, typeof(MemberMustSetAttribute));
                            if (must || memberMust)
                                newRules.Add(new ValidationMember(m.Name, must, memberMust));
                        }

                        currentType = currentType.BaseType;
                    }
                }
                finally
                {
                    if (dummyInstance is Node nodeInstance)
                        nodeInstance.Free();
                }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Analysis Failed] {path}: {ex.Message}");
        }

        _scriptPathToRules[path] = newRules;
        return newRules;
    }

    private static bool IsGodotNull(object? obj)
    {
        if (obj == null)
            return true;
        if (obj is GodotObject go)
            return !IsInstanceValid(go);
        if (obj is Variant v)
            return v.VariantType == Variant.Type.Nil || IsGodotNull(v.Obj);
        return false;
    }

    private static void PrintValidationError(string context, string memberName, string error)
    {
        GD.PrintRich($"[color=red][❌ Error][/color] {context} :: [b]{memberName}[/b] -> {error}");
    }

    private readonly record struct ValidationMember(string MemberName, bool IsMustSet, bool IsMemberMustSet);
}
#endif