using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireflyFestival.Editor
{
    // Run with -executeMethod FireflyFestival.Editor.FireworksVerification.Run
    // -fireworksOutput <external folder>. Do not pass -quit: the runner exits after Play Mode.
    [InitializeOnLoad]
    public static class FireworksVerification
    {
        const string RunningKey = "FireflyFestival.Verifying";
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static FireworksDirector director;
        static int stage;
        static double stageStart;
        static float pausedTime;
        static int pausedSamples;
        static float[] captures;
        static int nextCapture;
        static int peakShells, peakParticles;

        static FireworksVerification()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Firefly Festival/Verify Full Score and Capture Previews")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before verifying the score.");
            stage = nextCapture = peakShells = peakParticles = 0;
            checks.Clear();
            errors.Clear();
            string output = FireworksSceneBuilder.OutputFolder();
            EditorSceneManager.OpenScene(FireworksSceneBuilder.ScenePath);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "No missing script: " + node.name, false);
            var controller = UnityEngine.Object.FindFirstObjectByType<FireworksDirector>();
            Require(controller != null && controller.score != null && controller.shellPrefab != null, "Scene references are assigned");
            var score = controller.score;
            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
            Require(volume != null && volume.sharedProfile != null && volume.sharedProfile.TryGet<Bloom>(out var bloom) && bloom.intensity.value > .5f,
                "Bloom profile and its component subassets are saved correctly");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Require(pipeline != null && pipeline.scriptableRenderer != null, "URP renderer is available");
            Require(score.music != null && score.music.length > 330 && score.music.length < 340, "The complete supplied 5:35 BGM is assigned");
            Require(score.energy != null && score.energy.Length > 1600 && score.energy.Max() > .9f, "BGM sample analysis contains real audio data");
            Require(score.cues.Length > 80 && score.cues.All(cue => cue.time > 2 && cue.time < score.music.length), "All cues lie inside the music");
            Require(score.cues.SequenceEqual(score.cues.OrderBy(cue => cue.time)), "Cue times are sorted");
            Require(score.cues.Count(cue => cue.paletteIndex == 2) < score.cues.Length * .1f, "Red remains a restrained accent under 10 percent");
            foreach (string dependency in AssetDatabase.GetDependencies(FireworksSceneBuilder.ScenePath, true))
                Require(!dependency.Contains("HandsOnPractice"), "Self-contained dependency: " + dependency, false);
            File.WriteAllText(Path.Combine(output, "scene-validation.txt"), "Scene references and score validated.\nCues: " + score.cues.Length + "\nDuration: " + score.music.length);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void Update()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            try
            {
                if (!EditorApplication.isPlaying) return;
                if (stage == 0)
                {
                    director = UnityEngine.Object.FindFirstObjectByType<FireworksDirector>();
                    if (director == null || !director.Ready) return;
                    stage = 1;
                    stageStart = EditorApplication.timeSinceStartup;
                }
                double age = EditorApplication.timeSinceStartup - stageStart;
                var music = director.GetComponent<AudioSource>();
                if (stage == 1 && age > 3)
                {
                    Require(music.isPlaying && music.timeSamples > 100, "Real AudioSource playback starts and sample position advances");
                    Require(director.Elapsed > 1, "DSP choreography clock advances during real playback");
                    director.TogglePause();
                    pausedTime = director.Elapsed;
                    pausedSamples = music.timeSamples;
                    stage = 2;
                    stageStart = EditorApplication.timeSinceStartup;
                }
                else if (stage == 2 && age > .35)
                {
                    Require(director.Paused && Mathf.Abs(director.Elapsed - pausedTime) < .001f && music.timeSamples == pausedSamples, "Pause freezes the score and music together");
                    director.TogglePause();
                    stage = 3;
                    stageStart = EditorApplication.timeSinceStartup;
                }
                else if (stage == 3 && age > .35)
                {
                    Require(!director.Paused && music.isPlaying && director.Elapsed > pausedTime + .1f, "Resume advances music and score together");
                    director.Restart();
                    stage = 4;
                    stageStart = EditorApplication.timeSinceStartup;
                }
                else if (stage == 4 && age > .15)
                {
                    Require(director.Elapsed < 1 && director.LaunchedCount == 0 && director.ActiveShellCount == 0, "Replay clears shells and returns the score to zero");
                    director.BeginPreview();
                    Time.timeScale = 0;
                    var cues = director.score.cues;
                    captures = new[]
                    {
                        cues[0].time + 1.4f,
                        cues.OrderBy(cue => Mathf.Abs(cue.time - 65)).First().time + 1.15f,
                        cues.First(cue => cue.shape == BloomShape.DoubleRing && cue.time > 130).time + 1.05f,
                        cues.OrderBy(cue => Mathf.Abs(cue.time - 250)).First().time + 1.2f,
                        director.Duration - 9.8f,
                        director.Duration + 9.4f
                    };
                    stage = 5;
                    stageStart = EditorApplication.timeSinceStartup;
                }
                else if (stage == 5)
                {
                    // Fixed small steps exercise every launch, burst and cleanup in the complete score.
                    for (int i = 0; i < 120; i++)
                    {
                        director.PreviewStep(1f / 30);
                        peakShells = Mathf.Max(peakShells, director.ActiveShellCount);
                        if (nextCapture < captures.Length && director.Elapsed >= captures[nextCapture])
                        {
                            int particles = 0;
                            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) particles += ps.particleCount;
                            peakParticles = Mathf.Max(peakParticles, particles);
                            Capture(nextCapture);
                            nextCapture++;
                        }
                        if (director.Elapsed > director.Duration + 9.5f)
                        {
                            Require(director.LaunchedCount == director.score.cues.Length, "Every cue launches over the complete track");
                            Require(director.DroppedCount == 0, "No cues dropped by the object pool");
                            Require(director.ActiveShellCount == 0, "All shells return to the pool after the final embers");
                            Require(director.Finished && director.fireflies.particleCount == 0, "The ending finishes and ambient particles settle to zero");
                            foreach (string path in AssetDatabase.FindAssets("t:Shader", new[] { FireworksSceneBuilder.Root }))
                            {
                                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(path));
                                Require(shader.isSupported && !ShaderUtil.GetShaderMessages(shader).Any(message => message.severity.ToString() == "Error"), "Shader compiled on the graphics device: " + shader.name);
                            }
                            Require(errors.Count == 0, "No runtime errors or exceptions");
                            Finish(true);
                            return;
                        }
                    }
                    if (age > 180) throw new TimeoutException("Full-score verification exceeded three minutes.");
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
                Finish(false);
            }
        }

        static void Capture(int index)
        {
            var camera = director.showCamera;
            float previousAspect = camera.aspect;
            camera.aspect = 16f / 9;
            var renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP cannot render a preview.");
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            string[] names = { "01-first-lights", "02-golden-whisper", "03-rings-in-the-sky", "04-forest-celebration", "05-last-wish", "06-ending" };
            File.WriteAllBytes(Path.Combine(FireworksSceneBuilder.OutputFolder(), names[index] + ".png"), texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.aspect = previousAspect;
            UnityEngine.Object.DestroyImmediate(texture);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            Debug.Log("FIREFLY_CAPTURE: " + names[index] + " at " + director.Elapsed.ToString("F2"));
        }

        static void Require(bool condition, string message, bool record = true)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + message);
            if (record) checks.Add("PASS: " + message);
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(RunningKey, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                errors.Add(message + "\n" + stack);
        }

        static void Finish(bool success)
        {
            SessionState.SetBool(RunningKey, false);
            var report = new StringBuilder();
            report.AppendLine(success ? "PASS — FIREFLY NOCTURNE" : "FAIL — FIREFLY NOCTURNE");
            if (director != null)
            {
                report.AppendLine("Duration: " + director.Duration.ToString("F3") + " seconds");
                report.AppendLine("Cues launched: " + director.LaunchedCount + " / " + director.score.cues.Length);
                report.AppendLine("Dropped cues: " + director.DroppedCount);
                report.AppendLine("Peak active shells: " + peakShells + " / " + director.poolSize);
                report.AppendLine("Particles at busiest captured frame: " + peakParticles);
                report.AppendLine("Captures: " + nextCapture);
            }
            foreach (string check in checks) report.AppendLine(check);
            foreach (string error in errors) report.AppendLine("ERROR: " + error);
            File.WriteAllText(Path.Combine(FireworksSceneBuilder.OutputFolder(), "verification-result.txt"), report.ToString());
            Time.timeScale = 1;
            Debug.Log(success ? "FIREFLY_VERIFICATION_PASSED" : "FIREFLY_VERIFICATION_FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
