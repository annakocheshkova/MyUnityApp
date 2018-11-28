#addin nuget:?package=Cake.FileHelpers
//#addin "Cake.AzureStorage"
//#addin nuget:?package=NuGet.Core
#addin "Cake.Xcode"
//#load "utility.cake"

using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Runtime.Versioning;

var NdkFolder = "android_ndk";

// Task TARGET for build
var Target = Argument("target", Argument("t", "Default"));

// Install Unity Editor for Windows
Task("Install-Unity-Windows").Does(() => {
    const string unityDownloadUrl = @"https://netstorage.unity3d.com/unity/2207421190e9/Windows64EditorInstaller/UnitySetup64-2018.2.9f1.exe";
    const string il2cppSupportDownloadUrl = @"https://netstorage.unity3d.com/unity/2207421190e9/TargetSupportInstaller/UnitySetup-UWP-IL2CPP-Support-for-Editor-2018.2.9f1.exe";
    // const string dotNetSupportDownloadUrl = @"https://netstorage.unity3d.com/unity/2207421190e9/TargetSupportInstaller/UnitySetup-UWP-.NET-Support-for-Editor-2018.2.9f1.exe";

    Information("Downloading Unity Editor...");
    DownloadFile(unityDownloadUrl, "./UnitySetup64.exe");
    Information("Installing Unity Editor...");
    var result = StartProcess("./UnitySetup64.exe", " /S");
    if (result != 0)
    {
        throw new Exception("Failed to install Unity Editor");
    }

    Information("Downloading IL2CPP support...");
    DownloadFile(il2cppSupportDownloadUrl, "./UnityIl2CppSupport.exe");
    Information("Installing IL2CPP support...");
    result = StartProcess("./UnityIl2CppSupport.exe", " /S");
    if (result != 0)
    {
        throw new Exception("Failed to install IL2CPP support");
    }
}).OnError(HandleError);

Task("BuildApp")
    .Does(()=>
{
    BuildApps();
}).OnError(HandleError);

// Downloads the NDK from the specified location.
Task("DownloadNdk")
    .Does(()=>
{
    var ndkUrl = EnvironmentVariable("ANDROID_NDK_URL");
    if (string.IsNullOrEmpty(ndkUrl))
    {
        throw new Exception("Ndk Url is empty string or null");
    }
    var zipDestination = Statics.TemporaryPrefix + "ndk.zip";
    
    // Download required NDK
    DownloadFile(ndkUrl, zipDestination);

    // Something is buggy about the way Cake unzips, so use shell on mac
    if (IsRunningOnUnix())
    {
        CleanDirectory(NdkFolder);
        StartProcess("unzip", new ProcessSettings{ Arguments = $"{zipDestination} -d {NdkFolder}"});
    }
    else
    {
        Unzip(zipDestination, NdkFolder);
    }
}).OnError(HandleError);

void BuildApps(string projectPath = "..")
{
    var buildTarget = Statics.Context.EnvironmentVariable("BUILD_TARGET");
    var buildIOS = true;
    var buildAndroid = true;
    if (buildTarget == "Android") 
    {
        buildIOS = false;
    }
    if (buildTarget == "iOS") 
    {
        buildAndroid = false;
    }
    if (Statics.Context.IsRunningOnUnix())
    {
        if (buildIOS) 
        {
            VerifyIosAppsBuild(projectPath);
        }
        if (buildAndroid) 
        {
            VerifyAndroidAppsBuild(projectPath);
        }
    }
    else
    {
        VerifyWindowsAppsBuild(projectPath);
    }
}

void VerifyIosAppsBuild(string projectPath)
{
    VerifyAppsBuild("ios", projectPath,
    new string[] { "IosMono", "IosIl2CPP" },
    outputDirectory =>
    {
        var directories = GetDirectories(outputDirectory + "/*/*.xcodeproj");
        if (directories.Count == 0)
        {
            throw new Exception("No ios projects found in directory '" + outputDirectory + "'");
        }
        var xcodeProjectPath = directories.Single();
        Statics.Context.Information("Attempting to build '" + xcodeProjectPath.FullPath + "'...");
        BuildXcodeProject(xcodeProjectPath.FullPath);
        Statics.Context.Information("Successfully built '" + xcodeProjectPath.FullPath + "'");
    });
}

void VerifyAndroidAppsBuild(string projectPath)
{
    var extraArgs = "";
    if (DirectoryExists(NdkFolder))
    {
        var absoluteNdkFolder = Statics.Context.MakeAbsolute(Statics.Context.Directory(NdkFolder));
        extraArgs += "-NdkLocation \"" + absoluteNdkFolder + "\"";
    }
    VerifyAppsBuild("android", projectPath,
    new string[] { "AndroidMono", "AndroidIl2CPP" },
    outputDirectory =>
    {
        var files = Statics.Context.GetFiles(outputDirectory + "/*.apk");
        // Verify that an APK was generated.
        if (files.Count == 0)
        {
            throw new Exception("No apk found in directory '" + outputDirectory + "'");
        }
        foreach (var file in files) {
            Statics.Context.Information("Found apk: " + file.FullPath);
        }
    }, extraArgs);
}

void VerifyWindowsAppsBuild(string projectPath)
{
    VerifyAppsBuild("wsaplayer", projectPath,
    new string[] { "WsaIl2CPPD3D" },
    outputDirectory =>
    {
        Statics.Context.Information("Verifying app build in directory: " + outputDirectory);
        var slnFiles = GetFiles(outputDirectory + "/*/*.sln");
        if (slnFiles.Count() == 0)
        {
            throw new Exception("No .sln files found in the following directory and all it's subdirectories: " + outputDirectory);
        }
        if (slnFiles.Count() > 1)
        {
            throw new Exception(string.Format("Multiple .sln files found in directory {0}: {1}", outputDirectory, string.Join(", ", slnFiles)));
        }
        var solutionFilePath = slnFiles.Single();
        Statics.Context.Information("Attempting to build '" + solutionFilePath.ToString() + "'...");
        Statics.Context.MSBuild(solutionFilePath.ToString(), c => c
        .SetConfiguration("Master")
        .WithProperty("Platform", "x86")
        .SetVerbosity(Verbosity.Minimal)
        .SetMSBuildPlatform(MSBuildPlatform.x86));
        Statics.Context.Information("Successfully built '" + solutionFilePath.ToString() + "'");
    });
}

void VerifyAppsBuild(string platformIdentifier, string projectPath, string[] buildTypes, Action<string> verificatonMethod, string extraArgs = "")
{
    var outputDirectory = GetBuildFolder(projectPath);
    var methodPrefix = "BuildMyUnityApp.BuildMyUnityAppScene";
    foreach (var buildType in buildTypes)
    {
        // Remove all existing builds and create new build.
        Statics.Context.CleanDirectory(outputDirectory);
        ExecuteUnityMethod(methodPrefix + buildType + " " + extraArgs, platformIdentifier);
        verificatonMethod(outputDirectory);

        // Remove all remaining builds.
        //Statics.Context.CleanDirectory(outputDirectory);
    }

    // Remove all remaining builds.
    //Statics.Context.CleanDirectory(outputDirectory);
}

Task("RegisterUnity").Does(()=>
{
    var serialNumber = Argument<string>("UnitySerialNumber");
    var username = Argument<string>("UnityUsername");
    var password = Argument<string>("UnityPassword");

    // This will produce an error, but that's okay because the project "noproject" is used so that the
    // root isn't opened by unity, which could potentially remove important .meta files.
    ExecuteUnityCommand($"-serial {serialNumber} -username {username} -password {password}", "noproject");
}).OnError(HandleError);

Task("UnregisterUnity").Does(()=>
{
    ExecuteUnityCommand("-returnLicense", null);
}).OnError(HandleError);

// Clean up files/directories.
Task("clean")
    .IsDependentOn("RemoveTemporaries")
    .Does(() =>
{
    DeleteDirectoryIfExists("externals");
    DeleteDirectoryIfExists("output");
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

void BuildXcodeProject(string projectPath)
{
    var projectFolder = System.IO.Path.GetDirectoryName(projectPath);
    var buildOutputFolder =  System.IO.Path.Combine(projectFolder, "build");
    XCodeBuild(new XCodeBuildSettings {
        Project = projectPath,
        Scheme = "Unity-iPhone",
        Configuration = "Release",
        DerivedDataPath = buildOutputFolder
    });
}

// This file contains various utilities that are or can be used by multiple cake scripts.

// Static variables defined outside of a class can cause issues.
public class Statics
{
    // Cake context.
    public static ICakeContext Context { get; set; }

    // Prefix for temporary intermediates that are created by this script.
    public const string TemporaryPrefix = "CAKE_SCRIPT_TEMP";
}

// Can't reference Context within the class, so set value outside
Statics.Context = Context;

static int ExecuteUnityCommand(string extraArgs, string projectPath = "..")
{
    var projectDir = projectPath == null ? null : Statics.Context.MakeAbsolute(Statics.Context.Directory(projectPath));
    var unityPath = Statics.Context.EnvironmentVariable("UNITY_PATH");

    // If environment variable is not set, use default locations
    if (unityPath == null)
    {
        if (Statics.Context.IsRunningOnUnix())
        {
            unityPath = "/Applications/Unity/Unity.app/Contents/MacOS/Unity";
        }
        else
        {
            unityPath = "C:\\Program Files\\Unity\\Editor\\Unity.exe";
        }
    }

    // Unity log file
    var unityLogFile = "CAKE_SCRIPT_TEMPunity_build_log.log";
    if (System.IO.File.Exists(unityLogFile))
    {
        unityLogFile += "1";
    }
    var unityArgs = "-batchmode -quit -logFile " + unityLogFile;

    // If the command has an associated project, add it to the arguments
    if (projectDir != null)
    {
        unityArgs += " -projectPath " + projectDir;
    }

    unityArgs += " " + extraArgs;

    System.IO.File.Create(unityLogFile).Dispose();
    var logExec = "powershell.exe";
    var logArgs = "Get-Content -Path " + unityLogFile + " -Wait";
    if (Statics.Context.IsRunningOnUnix())
    {
        logExec = "tail";
        logArgs = "-f " + unityLogFile;
    }
    int result = 0;
    using (var unityProcess = Statics.Context.StartAndReturnProcess(unityPath, new ProcessSettings{ Arguments = unityArgs }))
    {
        using (var logProcess = Statics.Context.StartAndReturnProcess(logExec, new ProcessSettings{ Arguments = logArgs, RedirectStandardError = true}))
        {
            unityProcess.WaitForExit();
            result = unityProcess.GetExitCode();
            if (logProcess.WaitForExit(0) && (logProcess.GetExitCode() != 0))
            {
                Statics.Context.Warning("There was an error logging, but command still executed.");
            }
            else
            {
                try
                {
                    logProcess.Kill();
                }
                catch
                {
                    // Log process was stopped right after checking
                }
            }
        }
    }
    DeleteFileIfExists(unityLogFile);
    return result;
}

// appType usually "Puppet" or "Demo"
static string GetBuildFolder(string projectPath)
{
     return projectPath + "/" + Statics.TemporaryPrefix + "MyUnityAppBuilds";
}

static void ExecuteUnityMethod(string buildMethodName, string buildTarget = null, string projectPath = "..")
{
    Statics.Context.Information("Executing method " + buildMethodName + ", this could take a while...");
    var command = "-executeMethod " + buildMethodName;
    if (buildTarget != null)
    {
        command += " -buildTarget " + buildTarget;
    }
    var result = ExecuteUnityCommand(command, projectPath);
    if (result != 0)
    {
        throw new Exception("Failed to execute method " + buildMethodName + ".");
    }
}

// Copy files to a clean directory using string names instead of FilePath[] and DirectoryPath
static void CopyFiles(IEnumerable<string> files, string targetDirectory, bool clean = true)
{
    if (clean)
    {
        CleanDirectory(targetDirectory);
    }
    foreach (var file in files)
    {
        Statics.Context.CopyFile(file, targetDirectory + "/" + System.IO.Path.GetFileName(file));
    }
}

static void DeleteDirectoryIfExists(string directoryName)
{
    if (Statics.Context.DirectoryExists(directoryName))
    {
        Statics.Context.DeleteDirectory(directoryName, new DeleteDirectorySettings() { Recursive = true });
    }
}

static void DeleteFileIfExists(string fileName)
{
    try
    {
        if (Statics.Context.FileExists(fileName))
        {
            Statics.Context.DeleteFile(fileName);
        }
    }
    catch
    {
        Statics.Context.Information("Unable to delete file '" + fileName + "'.");
    }
}

static void CleanDirectory(string directoryName)
{
    DeleteDirectoryIfExists(directoryName);
    Statics.Context.CreateDirectory(directoryName);
}

void HandleError(Exception exception)
{
    RunTarget("clean");
    throw new Exception("Error occurred, see inner exception for details", exception);
}

// Remove all temporary files and folders
Task("RemoveTemporaries").Does(()=>
{
    DeleteFiles(Statics.TemporaryPrefix + "*");
    var dirs = GetDirectories(Statics.TemporaryPrefix + "*");
    foreach (var directory in dirs)
    {
        DeleteDirectory(directory, new DeleteDirectorySettings() { Recursive = true });
    }
});


RunTarget(Target);
