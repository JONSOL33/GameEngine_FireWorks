using UnityEngine;

namespace FireflyFestival
{
    // The launch, burst, trails and lingering embers are all native Unity Particle Systems.
    // One reusable shell owns their lifecycle, so long performances do not accumulate objects.
    public sealed class FireworkShell : MonoBehaviour
    {
        public ParticleSystem launchTrail;
        public ParticleSystem sparks;
        public ParticleSystem embers;
        public ParticleSystem flash;
        public float flightDuration = 1.9f;

        public bool Active { get; private set; }
        public float BurstAge => age - flightDuration;
        public Vector3 BurstPosition => cue.position;
        public Color BurstColor { get; private set; }
        public float ReflectionStrength => Active && BurstAge >= 0 ? Mathf.Exp(-BurstAge * .8f) * cue.scale : 0;

        FireworkCue cue;
        Vector3 origin;
        float age;
        float lifetime;
        bool exploded;
        bool secondary;
        System.Random random;

        public void Launch(FireworkCue nextCue, Color color)
        {
            Clear();
            cue = nextCue;
            BurstColor = color;
            random = new System.Random(cue.seed);
            origin = new Vector3(cue.position.x * .8f, .4f, cue.position.z);
            transform.position = origin;
            lifetime = cue.shape == BloomShape.Fireflies ? 9f : cue.shape == BloomShape.Willow ? 7.7f : 5.6f;
            age = 0;
            exploded = secondary = false;
            Active = true;
            var main = launchTrail.main;
            main.startColor = Color.Lerp(color, Color.white, .35f);
            launchTrail.Play();
        }

        public void Tick(float delta)
        {
            if (!Active) return;
            age += delta;
            if (!exploded)
            {
                float t = Mathf.Clamp01(age / flightDuration);
                // A decelerating climb, ending exactly on the musical cue.
                float climb = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(origin, cue.position, climb);
                transform.position += Vector3.right * Mathf.Sin(t * Mathf.PI) * (cue.seed % 2 == 0 ? .9f : -.9f);
                if (t >= 1f)
                {
                    exploded = true;
                    launchTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Burst();
                }
            }
            if (exploded && !secondary && BurstAge > 1.25f && cue.shape == BloomShape.DoubleRing)
            {
                secondary = true;
                EmitRing(embers, 72, 4.4f * cue.scale, BurstColor, true);
            }
            if (BurstAge > lifetime) Clear();
        }

        void Burst()
        {
            var main = sparks.main;
            main.gravityModifier = cue.shape == BloomShape.Willow ? .18f : cue.shape == BloomShape.Fireflies ? -.006f : .075f;
            var noise = sparks.noise;
            noise.enabled = cue.shape == BloomShape.Fireflies;
            var trail = sparks.trails;
            trail.lifetime = cue.shape == BloomShape.Willow ? .85f : .45f;
            sparks.Play();
            embers.Play();
            flash.Play();
            Emit(flash, Vector3.zero, Color.Lerp(BurstColor, Color.white, .65f), 4.5f * cue.scale, .32f);

            int count = Mathf.RoundToInt((cue.shape == BloomShape.Willow ? 165 : 145) * cue.scale);
            if (cue.shape == BloomShape.Ring || cue.shape == BloomShape.DoubleRing)
            {
                EmitRing(sparks, count, 11f * cue.scale, BurstColor, false);
                if (cue.shape == BloomShape.DoubleRing)
                    EmitRing(sparks, count / 2, 6.5f * cue.scale, Color.Lerp(BurstColor, Color.white, .3f), true);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    // Fibonacci sphere with a seeded perturbation gives an even, repeatable bloom.
                    float y = 1f - 2f * (i + .5f) / count;
                    float angle = i * 2.39996323f + cue.seed;
                    float radius = Mathf.Sqrt(1f - y * y);
                    Vector3 direction = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius * .55f);
                    float speed = (cue.shape == BloomShape.Fireflies ? Range(2.7f, 5.8f) : Range(6.5f, 10f)) * cue.scale;
                    float life = cue.shape == BloomShape.Willow ? Range(4.5f, 6.5f) : cue.shape == BloomShape.Fireflies ? Range(5f, 7.5f) : Range(2.5f, 4f);
                    Emit(sparks, direction * speed, Tint(), Range(.46f, .72f) * cue.scale, life);
                }
            }
            for (int i = 0; i < 85 * cue.scale; i++)
            {
                float angle = Range(0, Mathf.PI * 2);
                Vector3 direction = new Vector3(Mathf.Cos(angle), Range(-.4f, .7f), Mathf.Sin(angle) * .55f);
                Emit(embers, direction * Range(.5f, 4.5f) * cue.scale, Tint(), Range(.18f, .32f), Range(3f, 6f));
            }
        }

        void EmitRing(ParticleSystem system, int count, float speed, Color color, bool tilted)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                if (tilted) direction = Quaternion.Euler(0, 38, 28) * direction;
                Emit(system, direction * speed * Range(.96f, 1.04f), color, Range(.42f, .64f) * cue.scale, Range(3.1f, 4f));
            }
        }

        void Emit(ParticleSystem system, Vector3 velocity, Color color, float size, float life)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = transform.position,
                velocity = velocity,
                startColor = color,
                startSize = size,
                startLifetime = life,
                randomSeed = (uint)random.Next(1, int.MaxValue)
            };
            system.Emit(particle, 1);
        }

        Color Tint() => Color.Lerp(BurstColor, new Color(1, .96f, .7f), Range(0, .35f));
        float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        public void SetPaused(bool paused)
        {
            if (!Active) return;
            foreach (var ps in new[] { launchTrail, sparks, embers, flash })
                if (paused) ps.Pause(); else ps.Play();
            if (!paused && exploded) launchTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Clear()
        {
            foreach (var ps in new[] { launchTrail, sparks, embers, flash })
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Active = false;
        }
    }
}
