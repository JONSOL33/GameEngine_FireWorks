using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireflyFestival
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FireworksDirector : MonoBehaviour
    {
        public FireworksScore score;
        public FireworkShell shellPrefab;
        public ParticleSystem fireflies;
        public Camera showCamera;
        [Range(0, 1)] public float musicVolume = .72f;
        [Range(8, 64)] public int poolSize = 32;
        public bool showOverlay = true;

        public float Elapsed { get; private set; }
        public float Duration => score != null && score.music != null ? score.music.length : 0;
        public bool Paused { get; private set; }
        public bool Finished { get; private set; }
        public bool Ready => music != null && shells.Count == poolSize;
        public int LaunchedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int ActiveShellCount { get { int count = 0; foreach (var shell in shells) if (shell.Active) count++; return count; } }

        readonly List<FireworkShell> shells = new List<FireworkShell>();
        readonly Vector4[] lightPositions = new Vector4[16];
        readonly Vector4[] lightColors = new Vector4[16];
        AudioSource music;
        double startDsp;
        double pausedDsp;
        int nextCue;
        bool muted;
        bool manualClock;
        GUIStyle titleStyle, smallStyle, rightStyle, centerStyle, introStyle;
        Vector3 cameraOrigin;
        Quaternion cameraRotation;
        static readonly int ReflectionPositions = Shader.PropertyToID("_FireworkPositions");
        static readonly int ReflectionColors = Shader.PropertyToID("_FireworkColors");

        void Start()
        {
            if (score == null || score.music == null || shellPrefab == null)
            {
                Debug.LogError("Firefly Festival needs a score, music and shell prefab.", this);
                enabled = false;
                return;
            }
            Application.targetFrameRate = 60;
            music = GetComponent<AudioSource>();
            music.clip = score.music;
            music.loop = false;
            music.playOnAwake = false;
            music.spatialBlend = 0;
            music.volume = musicVolume;
            var pool = new GameObject("Shell Pool (reused throughout the show)").transform;
            pool.SetParent(transform, false);
            for (int i = 0; i < poolSize; i++)
            {
                var shell = Instantiate(shellPrefab, pool);
                shell.name = "Shell " + (i + 1).ToString("00");
                shell.Clear();
                shells.Add(shell);
            }
            if (showCamera != null)
            {
                cameraOrigin = showCamera.transform.position;
                cameraRotation = showCamera.transform.rotation;
            }
            Restart();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.mKey.wasPressedThisFrame) { muted = !muted; music.mute = muted; }
                if (keyboard.hKey.wasPressedThisFrame) showOverlay = !showOverlay;
            }
            if (Paused || manualClock) return;
            float previous = Elapsed;
            // The audio DSP clock keeps choreography independent of frame rate and Time.timeScale.
            Elapsed = Mathf.Clamp((float)(AudioSettings.dspTime - startDsp), 0, Duration + 10);
            Advance(Mathf.Max(0, Elapsed - previous));
        }

        void Advance(float delta)
        {
            foreach (var shell in shells) if (shell.Active) shell.Tick(delta);
            while (nextCue < score.cues.Length && score.cues[nextCue].time - shellPrefab.flightDuration <= Elapsed)
            {
                FireworkCue cue = score.cues[nextCue++];
                FireworkShell available = shells.Find(shell => !shell.Active);
                if (available == null) { DroppedCount++; continue; }
                available.Launch(cue, score.palette[Mathf.Clamp(cue.paletteIndex, 0, score.palette.Length - 1)]);
                // Only age a newly launched shell by the portion of this frame after its launch time.
                available.Tick(Mathf.Max(0, Elapsed - (cue.time - available.flightDuration)));
                LaunchedCount++;
            }
            if (fireflies != null)
            {
                var emission = fireflies.emission;
                float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Duration - 9, Duration + 1, Elapsed));
                emission.rateOverTime = (18f + 18f * Mathf.Clamp01(Elapsed / 70f)) * fade;
                if (Elapsed >= Duration + 9) fireflies.Clear();
            }
            Finished = Elapsed >= Duration + 9;
            UpdateReflections();
            if (showCamera != null)
            {
                showCamera.transform.position = cameraOrigin + new Vector3(Mathf.Sin(Elapsed * .035f) * .65f, Mathf.Sin(Elapsed * .054f) * .22f, 0);
                showCamera.transform.rotation = cameraRotation;
            }
        }

        void UpdateReflections()
        {
            int index = 0;
            foreach (var shell in shells)
            {
                if (shell.ReflectionStrength < .015f || index >= 16) continue;
                Vector3 position = shell.BurstPosition;
                lightPositions[index] = new Vector4(position.x, position.y, position.z, shell.ReflectionStrength);
                Color color = shell.BurstColor;
                lightColors[index++] = new Vector4(color.r, color.g, color.b, 1);
            }
            for (int i = index; i < 16; i++) { lightPositions[i] = Vector4.zero; lightColors[i] = Vector4.zero; }
            Shader.SetGlobalVectorArray(ReflectionPositions, lightPositions);
            Shader.SetGlobalVectorArray(ReflectionColors, lightColors);
        }

        public void TogglePause()
        {
            if (Finished || music == null) return;
            Paused = !Paused;
            if (Paused) { pausedDsp = AudioSettings.dspTime; music.Pause(); }
            else { startDsp += AudioSettings.dspTime - pausedDsp; music.UnPause(); }
            foreach (var shell in shells) shell.SetPaused(Paused);
            if (fireflies != null) { if (Paused) fireflies.Pause(); else fireflies.Play(); }
        }

        public void Restart()
        {
            if (music == null) return;
            foreach (var shell in shells) shell.Clear();
            nextCue = LaunchedCount = DroppedCount = 0;
            Elapsed = 0;
            Paused = Finished = manualClock = false;
            music.Stop();
            music.volume = musicVolume;
            startDsp = AudioSettings.dspTime + .35;
            music.PlayScheduled(startDsp);
            if (fireflies != null)
            {
                fireflies.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = fireflies.emission;
                emission.rateOverTime = 18;
                fireflies.Play();
            }
            UpdateReflections();
        }

        // Used by the Editor's full-score verification and deterministic preview tools.
        // Rendering still uses the real Particle Systems and URP shaders.
        public void BeginPreview()
        {
            Restart();
            music.Stop();
            manualClock = true;
        }

        public void PreviewStep(float delta)
        {
            if (!manualClock || Paused) return;
            Elapsed += delta;
            Advance(delta);
            foreach (var shell in shells)
                foreach (var ps in shell.GetComponentsInChildren<ParticleSystem>())
                    ps.Simulate(delta, false, false, false);
            if (fireflies != null) fireflies.Simulate(delta, false, false, false);
        }

        void OnDestroy()
        {
            Shader.SetGlobalVectorArray(ReflectionPositions, new Vector4[16]);
            Shader.SetGlobalVectorArray(ReflectionColors, new Vector4[16]);
        }

        void OnGUI()
        {
            if (!showOverlay || score == null || music == null) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                rightStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.UpperRight };
                centerStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
                introStyle = new GUIStyle(centerStyle) { fontSize = 37, fontStyle = FontStyle.Normal };
            }
            float width = 1600, height = 1600f * Screen.height / Screen.width;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.width / width));
            GUI.color = new Color(.91f, .92f, .76f, .85f);
            GUI.Label(new Rect(48, 33, 450, 25), "F I R E F L Y   N O C T U R N E", titleStyle);
            GUI.color = new Color(.71f, .80f, .69f, .65f);
            GUI.Label(new Rect(49, 60, 500, 22), "AN OLD LEGEND  /  TTRM", smallStyle);
            GUI.Label(new Rect(1020, 37, 532, 25), score.ChapterAt(Elapsed), rightStyle);

            float introAlpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Elapsed / 2)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7, 12, Elapsed)));
            if (introAlpha > 0)
            {
                GUI.color = new Color(.96f, .92f, .69f, introAlpha);
                GUI.Label(new Rect(200, height * .36f, 1200, 65), "Where fireflies become stars", introStyle);
                GUI.color = new Color(.73f, .83f, .66f, introAlpha * .8f);
                GUI.Label(new Rect(200, height * .36f + 66, 1200, 32), "A little light. An old story. A sky to remember.", centerStyle);
            }
            GUI.color = new Color(.7f, .78f, .67f, .5f);
            GUI.DrawTexture(new Rect(48, height - 48, 1504, 1), Texture2D.whiteTexture);
            GUI.color = new Color(.9f, .81f, .34f, .8f);
            GUI.DrawTexture(new Rect(48, height - 48, 1504 * Mathf.Clamp01(Elapsed / Mathf.Max(1, Duration)), 1.4f), Texture2D.whiteTexture);
            GUI.color = new Color(.72f, .78f, .68f, .65f);
            GUI.Label(new Rect(48, height - 37, 1000, 25), "SPACE  " + (Paused ? "resume" : "pause") + "     R  replay     M  " + (muted ? "unmute" : "mute") + "     H  hide titles", smallStyle);
            GUI.Label(new Rect(1200, height - 37, 352, 25), FormatTime(Elapsed) + "  /  " + FormatTime(Duration), rightStyle);
            if (Paused || Finished)
            {
                GUI.color = new Color(.91f, .91f, .71f, .95f);
                GUI.Label(new Rect(200, height * .47f, 1200, 50), Finished ? "Until the next little light.    /    R to replay" : "P A U S E D", centerStyle);
            }
            GUI.color = Color.white;
            GUI.matrix = previousMatrix;
        }

        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
    }
}
