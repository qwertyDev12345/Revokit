#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class ConesIOSExport
{
    [PostProcessBuild(100)]
    public static void Configure(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string file = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument(); plist.ReadFromFile(file);
        plist.root.SetString("CFBundleDisplayName", "Revokit");
        plist.root.SetString("CFBundleName", "Revokit");
        plist.root.SetString("NSPhotoLibraryAddUsageDescription", "Save drill diagrams to your photo library.");
        var types = plist.root.values.TryGetValue("UTExportedTypeDeclarations", out var existing) ? existing.AsArray() : plist.root.CreateArray("UTExportedTypeDeclarations");
        types.values.RemoveAll(value => value is PlistElementDict dictionary && dictionary.values.TryGetValue("UTTypeIdentifier", out var identifier) && identifier.AsString() == "com.cones.drill");
        var type = types.AddDict();
        type.SetString("UTTypeIdentifier", "com.cones.drill"); type.SetString("UTTypeDescription", "Revokit editable drill");
        type.CreateArray("UTTypeConformsTo").AddString("public.json");
        type.CreateDict("UTTypeTagSpecification").CreateArray("public.filename-extension").AddString("cones");
        plist.WriteToFile(file);
        string projectFile = PBXProject.GetPBXProjectPath(path);
        var project = new PBXProject(); project.ReadFromFile(projectFile);
        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AVFoundation.framework", false);
        project.WriteToFile(projectFile);
    }
}
#endif
