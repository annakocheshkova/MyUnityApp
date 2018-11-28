// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;

// This class provides methods to build the MyUnityApp app in many different configurations.
// They are meant to be invoked from the build scripts in this repository.
public class BuildMyUnityApp
{
    private static readonly string BuildFolder = "CAKE_SCRIPT_TEMPMyUnityAppBuilds";
    private static readonly string AppIdentifier = "com.microsoft.appcenter.unity.MyUnityApp";

    static BuildMyUnityApp()
    {
        var appIdentifier = System.Environment.GetEnvironmentVariable("APP_IDENTIFIER");
#if UNITY_5_6_OR_NEWER
        PlayerSettings.applicationIdentifier = appIdentifier;
#else
        PlayerSettings.bundleIdentifier = appIdentifier;
#endif
    }

    public static void BuildMyUnityAppSceneAndroidMono()
    {
        BuildMyUnityAppScene(BuildTarget.Android, BuildTargetGroup.Android, ScriptingImplementation.Mono2x, "AndroidMonoBuild.apk");
    }

    public static void BuildMyUnityAppSceneAndroidIl2CPP()
    {
        // Set NDK location if provided
        var args = Environment.GetCommandLineArgs();
        bool next = false;
        foreach (var arg in args)
        {
            if (next)
            {
                var ndkLocation = arg;
                var subdir = System.IO.Directory.GetDirectories(ndkLocation).Single();
                Debug.Log("Setting NDK location to " + subdir);
                EditorPrefs.SetString("AndroidNdkRoot", subdir);
                Debug.Log("NDK Location is now '" + EditorPrefs.GetString("AndroidNdkRoot") + "'");
                break;
            }
            if (arg == "-NdkLocation")
            {
                next = true;
            }
        }
        BuildMyUnityAppScene(BuildTarget.Android, BuildTargetGroup.Android, ScriptingImplementation.IL2CPP, "AndroidIL2CPPBuild.apk");
    }

    public static void BuildMyUnityAppSceneIosMono()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        BuildMyUnityAppScene(BuildTarget.iOS, BuildTargetGroup.iOS, ScriptingImplementation.Mono2x, "iOSMonoBuild");
    }

    public static void BuildMyUnityAppSceneIosIl2CPP()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        BuildMyUnityAppScene(BuildTarget.iOS, BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP, "iOSIL2CPPBuild");
    }

    public static void BuildMyUnityAppSceneIosMonoDeviceSdk()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        BuildMyUnityAppScene(BuildTarget.iOS, BuildTargetGroup.iOS, ScriptingImplementation.Mono2x, "iOSMonoBuild");
    }

    public static void BuildMyUnityAppSceneIosIl2CPPDeviceSdk()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        BuildMyUnityAppScene(BuildTarget.iOS, BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP, "iOSIL2CPPBuild");
    }

    public static void BuildMyUnityAppSceneWsaNetXaml()
    {
        EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.XAML;
        PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Legacy;
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WSA, ApiCompatibilityLevel.NET_4_6);
        BuildMyUnityAppScene(BuildTarget.WSAPlayer, BuildTargetGroup.WSA, ScriptingImplementation.WinRTDotNET, "WSANetBuildXaml");
    }

    public static void BuildMyUnityAppSceneWsaIl2CPPXaml()
    {
        EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.XAML;
        PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Legacy;
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WSA, ApiCompatibilityLevel.NET_4_6);
        BuildMyUnityAppScene(BuildTarget.WSAPlayer, BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP, "WSAIL2CPPBuildXaml");
    }

    public static void BuildMyUnityAppSceneWsaNetD3D()
    {
        EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
        PlayerSettings.WSA.compilationOverrides = PlayerSettings.WSACompilationOverrides.UseNetCore;
        PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Legacy;
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WSA, ApiCompatibilityLevel.NET_4_6);
        BuildMyUnityAppScene(BuildTarget.WSAPlayer, BuildTargetGroup.WSA, ScriptingImplementation.WinRTDotNET, "WSANetBuildD3D");
    }

    public static void BuildMyUnityAppSceneWsaIl2CPPD3D()
    {
        EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
        PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Legacy;
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WSA, ApiCompatibilityLevel.NET_4_6);
        BuildMyUnityAppScene(BuildTarget.WSAPlayer, BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP, "WSAIL2CPPBuildD3D");
    }

    private static void BuildMyUnityAppScene(BuildTarget target, BuildTargetGroup targetGroup, ScriptingImplementation scriptingImplementation, string outputPath)
    {
        PlayerSettings.SetScriptingBackend(targetGroup, scriptingImplementation);
        string scene = System.Environment.GetEnvironmentVariable("UNITY_SCENE");
        var options = new BuildPlayerOptions
        {
            scenes = scene == null ? new string[0] : new string[1]{ scene },
            options = BuildOptions.None,
            locationPathName = Path.Combine(BuildFolder, outputPath),
            target = target
        };
        BuildPipeline.BuildPlayer(options);
    }

    // Increments build version for all platforms
    public static void IncrementVersionNumber()
    {
        var currentVersion = PlayerSettings.bundleVersion;
        Debug.Log("current version: " + currentVersion);
        var minorVersion = int.Parse(currentVersion.Substring(currentVersion.LastIndexOf(".") + 1)) + 1;
        var newVersion = currentVersion.Substring(0, currentVersion.LastIndexOf(".") + 1) + minorVersion;
        Debug.Log("new version: " + newVersion);
        PlayerSettings.bundleVersion = newVersion;
        PlayerSettings.Android.bundleVersionCode++;
    }

    // Sets version number for MyUnityApp app
    public static void SetVersionNumber()
    {
        var currentVersion = PlayerSettings.bundleVersion;
        var MyUnityAppVersion = "1.0";
        PlayerSettings.bundleVersion = MyUnityAppVersion;
        PlayerSettings.Android.bundleVersionCode++;
    }
}
