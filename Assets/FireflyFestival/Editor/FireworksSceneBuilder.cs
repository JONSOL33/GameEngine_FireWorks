using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireflyFestival.Editor
{
    public static class FireworksSceneBuilder
    {
        public const string Root = "Assets/FireflyFestival";
        public const string ScenePath = "Assets/Scenes/GameScene.unity";
        public const string MusicPath = Root + "/Audio/An Old Legend - TTRM.mp3";
        static Material glow, trail;

        [MenuItem("Firefly Festival/Rebuild GameScene")]
        public static void BuildScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string folder in new[] { "Materials", "Meshes", "Prefabs", "Settings", "Audio" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            var score = BuildScore();
            glow = MaterialAsset("Firefly Glow", "Firefly Festival/Particle Glow");
            glow.SetFloat("_Intensity", 14f);
            trail = MaterialAsset("Golden Silk", "Firefly Festival/Silk Trail");
            trail.SetFloat("_Intensity", 5f);
            EditorUtility.SetDirty(glow);
            EditorUtility.SetDirty(trail);
            var shell = BuildShell();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.01f, .025f, .029f);
            RenderSettings.fog = false;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 9, -64);
            camera.transform.LookAt(new Vector3(0, 20, 25));
            camera.fieldOfView = 46;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.003f, .009f, .024f);
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var environment = new GameObject("01 · The moonlit lake").transform;
            BuildLandscape(environment);
            var fireflies = BuildFireflies(new GameObject("02 · Fireflies in the reeds").transform);
            var volume = new GameObject("03 · Gentle bloom & film grade").AddComponent<Volume>();
            volume.isGlobal = true;
            string profilePath = Root + "/Settings/Firefly Night.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            foreach (var oldComponent in AssetDatabase.LoadAllAssetsAtPath(profilePath).OfType<VolumeComponent>())
                UnityEngine.Object.DestroyImmediate(oldComponent, true);
            profile.components.Clear();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(.9f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(.68f);
            bloom.highQualityFiltering.Override(true);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.2f);
            vignette.smoothness.Override(.65f);
            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;

            var director = new GameObject("04 · Fireworks score — An Old Legend").AddComponent<FireworksDirector>();
            director.score = score;
            director.shellPrefab = shell;
            director.fireflies = fireflies;
            director.showCamera = camera;
            var audio = director.GetComponent<AudioSource>();
            audio.clip = score.music;
            audio.playOnAwake = false;
            audio.loop = false;
            audio.volume = .72f;
            audio.spatialBlend = 0;
            audio.priority = 0;

            // A dedicated forward renderer avoids the template's deferred/SSAO cost for an unlit particle scene.
            ConfigurePipeline();
            PlayerSettings.productName = "Firefly Nocturne";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"FIREFLY_SCENE_READY: {score.music.length:F3}s, {score.cues.Length} musical cues; {ScenePath}");
        }

        static FireworksScore BuildScore()
        {
            var importer = AssetImporter.GetAtPath(MusicPath) as AudioImporter;
            if (importer == null) throw new FileNotFoundException("Copy the supplied BGM into " + MusicPath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            clip.LoadAudioData();
            int samplesPerWindow = Mathf.RoundToInt(clip.frequency * .2f);
            int windows = clip.samples / samplesPerWindow;
            var data = new float[samplesPerWindow * clip.channels];
            var rms = new float[windows];
            for (int window = 0; window < windows; window++)
            {
                if (!clip.GetData(data, window * samplesPerWindow)) throw new InvalidOperationException("Cannot read BGM samples.");
                double sum = 0;
                foreach (float sample in data) sum += sample * sample;
                rms[window] = (float)Math.Sqrt(sum / data.Length);
            }
            float max = rms.Max();
            if (max < .0001f) throw new InvalidOperationException("The imported BGM is silent.");
            for (int i = 0; i < windows; i++) rms[i] /= max;
            float duration = clip.length;
            var score = ScriptableObject.CreateInstance<FireworksScore>();
            score.music = clip;
            score.energy = rms;
            score.chapters = new[]
            {
                new ShowChapter { title = "I  /  THE FIRST LITTLE LIGHT", start = 0 },
                new ShowChapter { title = "II  /  A GOLDEN WHISPER", start = duration * .095f },
                new ShowChapter { title = "III  /  THE FOREST AWAKENS", start = duration * .27f },
                new ShowChapter { title = "IV  /  STORIES IN THE SKY", start = duration * .47f },
                new ShowChapter { title = "V  /  A THOUSAND FIREFLIES", start = duration * .70f },
                new ShowChapter { title = "VI  /  ONE LAST WISH", start = duration * .90f },
                new ShowChapter { title = "THE LIGHT STAYS WITH US", start = duration - 9 }
            };
            var random = new System.Random(1117);
            var cues = new List<FireworkCue>();
            float next = 12.6f;
            int number = 0;
            while (next < duration - 16)
            {
                float fraction = next / duration;
                float gap = fraction < .095f ? 8.2f : fraction < .27f ? 5.8f : fraction < .47f ? 4.2f : fraction < .70f ? 3.6f : fraction < .90f ? 2.9f : 2.5f;
                // Choose the strongest local onset near each planned beat of the choreography.
                int center = Mathf.RoundToInt(next / .2f);
                int chosen = center;
                float onset = -1;
                for (int i = Mathf.Max(2, center - 3); i <= Mathf.Min(rms.Length - 2, center + 4); i++)
                {
                    float value = rms[i] + Mathf.Max(0, rms[i] - rms[i - 2]) * 1.8f;
                    if (value > onset) { onset = value; chosen = i; }
                }
                float time = chosen * .2f;
                float energy = rms[chosen];
                int color = number % 11 == 8 && fraction > .27f ? 2 : number % 3 == 1 ? 1 : 0;
                BloomShape shape = fraction < .095f ? BloomShape.Fireflies : (BloomShape)(number % 5);
                float x = fraction < .095f ? Range(random, -18, 18) : Mathf.Sin(number * 2.4f) * 26;
                float y = Range(random, 20, 36);
                float scale = Mathf.Lerp(.75f, 1.12f, energy);
                if (color == 2) scale *= .7f;
                cues.Add(Cue(time, x, y, shape, color, scale, random));
                if (fraction > .27f && (number % 3 == 0 || fraction > .90f))
                    cues.Add(Cue(time + .65f, -x * .8f, y - 4, number % 2 == 0 ? BloomShape.Willow : BloomShape.Ring, color == 0 ? 1 : 0, scale * .85f, random));
                if (fraction > .70f && number % 4 == 2)
                    cues.Add(Cue(time + 1.1f, Range(random, -12, 12), 38, BloomShape.Chrysanthemum, 0, 1.1f, random));
                next = time + gap + Range(random, -.35f, .35f);
                number++;
            }
            // A restrained, symmetrical gold/lime finale leaves room for the track's last notes.
            float finale = duration - 15;
            cues.Add(Cue(finale, -27, 26, BloomShape.Willow, 1, 1.05f, random));
            cues.Add(Cue(finale, 27, 26, BloomShape.Willow, 1, 1.05f, random));
            cues.Add(Cue(finale + 1.5f, -13, 35, BloomShape.DoubleRing, 0, 1.12f, random));
            cues.Add(Cue(finale + 1.5f, 13, 35, BloomShape.DoubleRing, 0, 1.12f, random));
            cues.Add(Cue(finale + 3.5f, 0, 34, BloomShape.Chrysanthemum, 0, 1.12f, random));
            cues.Add(Cue(duration - 8.8f, -15, 22, BloomShape.Fireflies, 1, .8f, random));
            cues.Add(Cue(duration - 8.8f, 15, 22, BloomShape.Fireflies, 0, .8f, random));
            score.cues = cues.OrderBy(cue => cue.time).ToArray();
            score = SaveAsset(score, Root + "/Settings/An Old Legend Score.asset");

            // Stream the final 5:35 track, rather than keeping its full uncompressed PCM in memory.
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .85f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            score.music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            EditorUtility.SetDirty(score);
            return score;
        }

        static FireworkCue Cue(float time, float x, float y, BloomShape shape, int color, float scale, System.Random random)
        {
            return new FireworkCue { time = time, position = new Vector3(x, y, Range(random, 16, 39)), shape = shape, paletteIndex = color, scale = scale, seed = random.Next(1, 999999) };
        }

        static FireworkShell BuildShell()
        {
            var root = new GameObject("Firefly Shell");
            var shell = root.AddComponent<FireworkShell>();
            shell.launchTrail = Particles("01 · Ascending golden spark", root.transform, 300);
            var main = shell.launchTrail.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .46f);
            main.startSpeed = .08f;
            main.startSize = new ParticleSystem.MinMaxCurve(.18f, .3f);
            var emission = shell.launchTrail.emission;
            emission.enabled = true;
            emission.rateOverTime = 95;
            AddTrails(shell.launchTrail, .15f, .7f);

            shell.sparks = Particles("02 · Bloom with silk trails", root.transform, 600);
            main = shell.sparks.main;
            main.gravityModifier = .075f;
            AddTrails(shell.sparks, .28f, .24f);
            var limit = shell.sparks.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = .22f;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
            var noise = shell.sparks.noise;
            noise.strength = .34f;
            noise.frequency = .25f;
            noise.scrollSpeed = .1f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.enabled = false;

            shell.embers = Particles("03 · Drifting firefly embers", root.transform, 400);
            main = shell.embers.main;
            main.gravityModifier = .02f;
            noise = shell.embers.noise;
            noise.enabled = true;
            noise.strength = .28f;
            noise.frequency = .35f;
            noise.scrollSpeed = .15f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            limit = shell.embers.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = .55f;
            shell.flash = Particles("04 · Soft heart of light", root.transform, 12);
            shell.Clear();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Firefly Shell.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<FireworkShell>();
        }

        static ParticleSystem Particles(string name, Transform parent, int maximum)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 12;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maximum;
            main.startSpeed = 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = 4;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .9f, .6f), 1) },
                new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.65f, .5f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .7f), new Keyframe(.15f, 1), new Keyframe(.7f, .65f), new Keyframe(1, 0)));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0;
            renderer.maxParticleSize = .15f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return system;
        }

        static void AddTrails(ParticleSystem system, float lifetime, float width)
        {
            var trails = system.trails;
            trails.enabled = true;
            trails.ratio = 1;
            trails.lifetime = lifetime;
            trails.minVertexDistance = .06f;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(width, AnimationCurve.Linear(0, 1, 1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.7f, .75f, .4f), 1) },
                new[] { new GradientAlphaKey(.8f, 0), new GradientAlphaKey(0, 1) });
            trails.colorOverTrail = gradient;
            system.GetComponent<ParticleSystemRenderer>().trailMaterial = trail;
        }

        static ParticleSystem BuildFireflies(Transform root)
        {
            var system = Particles("Gold & lime lanterns", root, 1600);
            system.transform.position = new Vector3(0, 4, 12);
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9, 18);
            main.startSize = new ParticleSystem.MinMaxCurve(.14f, .32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.06f, .27f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.98f, .8f, .25f), new Color(.58f, .95f, .2f));
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 18;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(105, 9, 95);
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = .5f;
            noise.frequency = .18f;
            noise.scrollSpeed = .12f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            system.useAutoRandomSeed = false;
            system.randomSeed = 1117;
            return system;
        }

        static void BuildLandscape(Transform parent)
        {
            Material sky = MaterialAsset("Moonlit Sky", "Firefly Festival/Night Sky");
            var skyObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyObject.name = "Stars & crescent moon";
            skyObject.transform.SetParent(parent, false);
            skyObject.transform.position = new Vector3(0, 42, 170);
            skyObject.transform.localScale = new Vector3(600, 360, 1);
            skyObject.GetComponent<Renderer>().sharedMaterial = sky;
            UnityEngine.Object.DestroyImmediate(skyObject.GetComponent<Collider>());

            Material water = MaterialAsset("Quiet Lake", "Firefly Festival/Quiet Lake");
            var lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Lake · rippling firework reflections";
            lake.transform.SetParent(parent, false);
            lake.transform.position = new Vector3(0, -.5f, 45);
            lake.transform.localScale = new Vector3(40, 1, 26);
            lake.GetComponent<Renderer>().sharedMaterial = water;
            UnityEngine.Object.DestroyImmediate(lake.GetComponent<Collider>());

            for (int layer = 0; layer < 3; layer++)
            {
                float z = 148 - layer * 23;
                float[] heights = new float[97];
                for (int i = 0; i < heights.Length; i++)
                {
                    float x = i * 3.75f - 180;
                    heights[i] = 4 + Mathf.PerlinNoise(i * .055f + layer * 2.2f, layer + .7f) * (20 - layer * 4) + Mathf.Sin(x * .025f + layer) * 3;
                }
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int i = 0; i < heights.Length; i++)
                {
                    float x = i * 3.75f - 180;
                    vertices.Add(new Vector3(x, -.6f, z));
                    vertices.Add(new Vector3(x, heights[i], z));
                    if (i > 0) { int n = vertices.Count - 4; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
                }
                Color color = layer == 0 ? new Color(.06f, .13f, .16f) : layer == 1 ? new Color(.035f, .085f, .105f) : new Color(.02f, .05f, .06f);
                MeshObject("Mountain ridge " + (layer + 1), vertices, triangles, Solid("Ridge " + layer, color), parent);
            }

            var random = new System.Random(20261117);
            var trees = new List<Vector3>();
            var indices = new List<int>();
            for (int i = 0; i < 180; i++)
            {
                float x = Range(random, -140, 140);
                float z = Range(random, 70, 93);
                float height = Range(random, 2.5f, 9) * Mathf.Lerp(.55f, 1.1f, Mathf.Abs(x) / 120);
                Triangle(trees, indices, new Vector3(x - .2f, -.5f, z), new Vector3(x, height * .9f, z), new Vector3(x + .2f, -.5f, z));
                for (int branch = 0; branch < 4; branch++)
                {
                    float y = .2f + height * branch * .17f;
                    float width = height * (.28f - branch * .045f);
                    Triangle(trees, indices, new Vector3(x - width, y, z), new Vector3(x, y + height * .54f, z), new Vector3(x + width, y, z));
                }
            }
            MeshObject("Distant pine forest", trees, indices, Solid("Forest silhouette", new Color(.01f, .025f, .029f)), parent);

            var reeds = new List<Vector3>();
            indices = new List<int>();
            for (int i = 0; i < 220; i++)
            {
                float x = Range(random, -70, 70);
                float z = Range(random, -37, -24);
                float centerOpening = Mathf.InverseLerp(6, 38, Mathf.Abs(x));
                float height = Range(random, .45f, 3.6f) * Mathf.Lerp(.25f, 1, centerOpening);
                float bend = Range(random, -.9f, .9f);
                Vector3 foot = new Vector3(x, -1.2f, z);
                Vector3 tip = foot + new Vector3(bend, height, 0);
                Triangle(reeds, indices, foot + Vector3.left * .07f, tip, foot + Vector3.right * .07f);
                Vector3 middle = Vector3.Lerp(foot, tip, .48f);
                Triangle(reeds, indices, middle, middle + new Vector3(-.55f, height * .45f, .03f), middle + new Vector3(-.07f, .25f, 0));
                Triangle(reeds, indices, middle, middle + new Vector3(.5f, height * .37f, -.03f), middle + new Vector3(.07f, .2f, 0));
            }
            MeshObject("Foreground reeds", reeds, indices, Solid("Reeds silhouette", new Color(.001f, .004f, .005f)), parent);
        }

        static void ConfigurePipeline()
        {
            string rendererPath = Root + "/Settings/Firefly Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null) renderer = SaveAsset(ScriptableObject.CreateInstance<UniversalRendererData>(), rendererPath);
            var templateRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            renderer.postProcessData = templateRenderer.postProcessData;
            EditorUtility.SetDirty(renderer);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/Settings/Firefly Pipeline.asset");
            if (pipeline == null)
            {
                AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset", Root + "/Settings/Firefly Pipeline.asset");
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/Settings/Firefly Pipeline.asset");
            }
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_RequireDepthTexture").boolValue = false;
            serialized.FindProperty("m_RequireOpaqueTexture").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.vSyncCount = 1;
        }

        static Material MaterialAsset(string name, string shaderName)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            return material;
        }

        static Material Solid(string name, Color color)
        {
            var material = MaterialAsset(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void MeshObject(string name, List<Vector3> vertices, List<int> triangles, Material material, Transform parent)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveAsset(mesh, Root + "/Meshes/" + name + ".asset");
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void Triangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.AddRange(new[] { a, b, c });
            triangles.AddRange(new[] { index, index + 1, index + 2 });
        }

        static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(asset);
            return existing;
        }

        static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        public static string OutputFolder()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-fireworksOutput");
            if ((index < 0 || index + 1 >= arguments.Length) && Application.isBatchMode)
                throw new ArgumentException("Supply -fireworksOutput with a folder outside the project.");
            string output = Path.GetFullPath(index >= 0 && index + 1 < arguments.Length ? arguments[index + 1]
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "output", "FireflyNocturne"));
            string project = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
            if (output.StartsWith(project, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Validation output must stay outside the project.");
            Directory.CreateDirectory(output);
            return output;
        }

        [MenuItem("Firefly Festival/Build Windows Player")]
        public static void BuildWindows()
        {
            string output = OutputFolder();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(output, "Windows", "Firefly Nocturne.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            File.WriteAllText(Path.Combine(output, "build-result.txt"), report.summary.result + "\nBytes: " + report.summary.totalSize + "\nErrors: " + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows player build failed.");
            Debug.Log("FIREFLY_WINDOWS_BUILD_SUCCEEDED");
        }
    }
}
