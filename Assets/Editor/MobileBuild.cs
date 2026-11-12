using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hollowmark.EditorTools
{
    /// <summary>
    /// One-click phone setup, from the Unity menu bar: <b>Hollowmark &gt; iPhone</b>.
    ///
    ///  iPhone app (needs a Mac with Xcode for the last step):
    ///    1. Set up for iPhone   - player settings, a scene in the build list, switch the platform to iOS
    ///    2. Build Xcode project - writes Builds/iOS, which you open in Xcode on a Mac and run on the phone
    ///
    ///  iPhone browser (no Mac needed):
    ///    1. Set up for phone browser - switch the platform to WebGL with phone-friendly settings
    ///    2. Build for phone browser  - writes Builds/WebGL, a web page you upload and open in Safari
    ///
    /// See IPHONE.md in the project folder for the full steps.
    /// </summary>
    public static class MobileBuild
    {
        // Must be unique on Apple's side. Change it if Xcode says the identifier is already taken.
        const string BundleId = "com.goodlaith01.hollowmark";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string IosFolder = "Builds/iOS";
        const string WebFolder = "Builds/WebGL";

        // ---------------------------------------------------------------- iPhone app

        [MenuItem("Hollowmark/iPhone/1. Set up for iPhone", false, 1)]
        public static void SetUpIos()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                EditorUtility.DisplayDialog("iOS Build Support is missing",
                    "Unity can't make iPhone builds yet.\n\nClose Unity, open Unity Hub, go to Installs, click the gear next to " +
                    "your Unity version, choose Add modules, tick \"iOS Build Support\" and install. Then open the project " +
                    "again and run this menu item once more.", "OK");
                return;
            }
            CommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = true;
            // A swipe from the screen edge during a fight shouldn't throw you out of the game.
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.statusBarHidden = true;
            EnsureScene();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            EditorUtility.DisplayDialog("Ready for iPhone",
                "Done. Next: Hollowmark > iPhone > 2. Build Xcode project.\n\nIt makes the folder " + IosFolder +
                " inside the project. Copy that folder to a Mac and open Unity-iPhone.xcodeproj in Xcode (see IPHONE.md).", "OK");
        }

        [MenuItem("Hollowmark/iPhone/2. Build Xcode project", false, 2)]
        public static void BuildIos()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) SetUpIos();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) return;
            Build(BuildTarget.iOS, BuildTargetGroup.iOS, IosFolder,
                "The Xcode project is in " + IosFolder + ". Copy the whole folder to a Mac, open Unity-iPhone.xcodeproj, " +
                "pick your Apple ID under Signing & Capabilities, plug in the iPhone and press the Play button in Xcode.");
        }

        // ---------------------------------------------------------------- iPhone browser (no Mac)

        [MenuItem("Hollowmark/iPhone browser (no Mac)/1. Set up for phone browser", false, 20)]
        public static void SetUpWeb()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("Web Build Support is missing",
                    "Close Unity, open Unity Hub, go to Installs, click the gear next to your Unity version, choose Add modules, " +
                    "tick \"Web Build Support\" (or \"WebGL Build Support\") and install. Then run this menu item again.", "OK");
                return;
            }
            CommonSettings();
            // Gzip with a fallback works on any free web host (GitHub Pages, itch.io) without server setup.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            EnsureScene();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            EditorUtility.DisplayDialog("Ready for the phone browser",
                "Done. Next: Hollowmark > iPhone browser (no Mac) > 2. Build for phone browser.", "OK");
        }

        [MenuItem("Hollowmark/iPhone browser (no Mac)/2. Build for phone browser", false, 21)]
        public static void BuildWeb()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) SetUpWeb();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) return;
            Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, WebFolder,
                "The web version is in " + WebFolder + ". Upload the whole folder to itch.io (as an HTML game) or GitHub Pages, " +
                "then open the link in Safari on your iPhone and turn the phone sideways (see IPHONE.md).");
        }

        // ---------------------------------------------------------------- Shared

        static void CommonSettings()
        {
            PlayerSettings.productName = "Hollowmark";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
        }

        /// <summary>A build needs at least one scene. The game builds itself at runtime, so an empty scene is enough.</summary>
        static void EnsureScene()
        {
            if (EditorBuildSettings.scenes.Any(s => s.enabled && File.Exists(s.path))) return;
            string path = ScenePath;
            var open = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(open.path)) path = open.path; // use the scene you already have open
            else if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string folder, string doneText)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { EditorUtility.DisplayDialog("No scene", "Run the Set up menu item first.", "OK"); return; }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = folder,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            });
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build finished", doneText, "OK");
                EditorUtility.RevealInFinder(Path.GetFullPath(folder));
            }
            else
                EditorUtility.DisplayDialog("Build failed",
                    "Unity couldn't finish the build (" + report.summary.totalErrors + " errors). Open Window > General > Console, " +
                    "copy the red messages and send them to Claude.", "OK");
        }
    }
}
