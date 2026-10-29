using System;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What a floating number reports; picks its colour and size.</summary>
    public enum DamageKind
    {
        Normal,
        Critical,
        Burn,
        Frost,
        Echo,
        Heal,
        Hero,
    }

    /// <summary>The "Damage numbers" setting, as <see cref="GameSettings.damageNumbers"/> stores it.</summary>
    internal enum DamageNumberMode
    {
        Off = 0,
        /// <summary>Critical hits and hits of <see cref="DamageNumbers.BIG_HIT"/> or more.</summary>
        BigHits = 1,
        All = 2,
    }

    /// <summary>
    /// Floating numbers over the world: 256 pooled elements in the Overlay document, at most 40 new numbers per
    /// second (the rest merge into numbers already up), hits near the same spot within 0.25 s merge and count up
    /// with a 1.2x re-pop, rise 0.9 m over 0.6 s with ease-out, pop 1.4 -> 1.0 in 0.08 s, fade during the last
    /// 0.2 s. Settings: Off / crits and hits of 50+ / All. Per frame it writes each number's left and top; text,
    /// scale (a cached transform string), opacity and visibility only when they change. Main thread.
    /// </summary>
    public sealed class DamageNumbers
    {
        /// <summary>Smallest hit shown while the setting is Big hits.</summary>
        internal const float BIG_HIT = 50.0f;
        private const int CAPACITY = 256;
        private const float SPAWNS_PER_SECOND = 40.0f;
        private const float MERGE_WINDOW = 0.25f;
        private const float MERGE_DISTANCE = 0.9f;
        /// <summary>A merge winds a number that rose past this share of its rise back to it, so it stays up longer.</summary>
        private const float MERGE_REWIND_SHARE = 0.5f;
        private const float RISE_METRES = 0.9f;
        private const float RISE_SECONDS = 0.6f;
        private const float FADE_SECONDS = 0.2f;
        private const float LIFETIME = RISE_SECONDS + FADE_SECONDS;
        private const float POP_SECONDS = 0.08f;
        private const float POP_SCALE = 1.4f;
        private const float MERGE_POP_SCALE = 1.2f;
        private const float HERO_POP_SCALE = 1.2f;
        private const float X_JITTER = 0.3f;
        /// <summary>Heals smaller than this show no number.</summary>
        private const float MIN_HEAL = 0.5f;
        private const float HERO_SHAKE_SECONDS = 0.25f;
        private const float HERO_SHAKE_PIXELS = 2.0f;
        /// <summary>Angular speed of the hero number's shake, radians per second.</summary>
        private const float HERO_SHAKE_SPEED = 90.0f;
        private const float COUNT_UP_RATE = 18.0f;
        private const int SCALE_STEPS = 41;
        private const float SCALE_MIN = 0.8f;
        private const float SCALE_STEP = 0.02f;
        private const string LAYER_ID = "#numbers";
        private const string NUMBER_CLASS = "dmg";
        private const string WORD_CLASS = "word";
        /// <summary>textValue of a number showing its word.</summary>
        private const int WORD_TEXT = -2;
        /// <summary>textValue of a number whose text is not written yet.</summary>
        private const int NO_TEXT = -1;

        private static readonly string[] kindClasses = { "", "crit", "burn", "frost", "echo", "heal", "hero" };
        private static readonly string[] scales = BuildScales();

        private struct Number
        {
            public UIElement element;
            public VisibilityWriter visibility;
            public OpacityWriter opacity;
            public Vector3 anchor;
            public float jitter;
            public float age;
            public float popAge;
            public float popFrom;
            public float amount;
            public float shown;
            public int textValue;
            public DamageKind kind;
            public DamageKind elementKind;
            public int scaleIndex;
            /// <summary>A word shown instead of the amount ("EXECUTED"), or null.</summary>
            public string word;
            public bool elementWord;
        }

        private readonly GameUI ui;
        private readonly Action onHeroHit;
        private readonly Number[] numbers = new Number[CAPACITY];
        private readonly int[] aliveList = new int[CAPACITY];
        private readonly int[] freeList = new int[CAPACITY];
        private readonly UIDocumentComponent component;
        private int aliveCount;
        private int freeCount;
        private UIDocument document;
        private float spawnBudget = SPAWNS_PER_SECOND;
        private bool isReady;

        /// <summary>Numbers in the overlay's document; <paramref name="onHeroHit"/> runs when the hero takes damage.</summary>
        internal DamageNumbers(GameUI ui, UIDocumentComponent overlay, Action onHeroHit)
        {
            this.ui = ui;
            this.onHeroHit = onHeroHit;
            component = overlay;
            if (ReferenceEquals(component, null))
            {
                Log.Warning("DamageNumbers: no Overlay UI Document; damage numbers are off.");
                return;
            }
            component.documentLoaded += OnDocumentLoaded;
        }

        private DamageNumberMode mode => (DamageNumberMode)ui.gameSettings.damageNumbers;

        private void OnDocumentLoaded(UIDocument loaded)
        {
            document = loaded;
            isReady = false;
            aliveCount = 0;
            freeCount = 0;
            UIElement layer = loaded?.Q(LAYER_ID);
            if (layer == null)
            {
                return;
            }
            for (int i = 0; i < CAPACITY; i++)
            {
                numbers[i] = CreateNumber(loaded, layer);
                freeList[freeCount++] = CAPACITY - 1 - i;
            }
            isReady = true;
        }

        private static Number CreateNumber(UIDocument loaded, UIElement layer)
        {
            UIElement element = loaded.CreateElement("div");
            element.SetClass(NUMBER_CLASS, true);
            layer.AppendChild(element);
            var number = new Number { element = element, elementKind = DamageKind.Normal, scaleIndex = -1 };
            number.visibility.Bind(element);
            number.opacity.Bind(element);
            return number;
        }

        public void Dispose()
        {
            isReady = false;
        }

        /// <summary>Hides every number (menus, run start).</summary>
        public void Clear()
        {
            for (int i = aliveCount - 1; i >= 0; i--)
            {
                Release(i);
            }
        }

        /// <summary>A hit of a given kind (burn ticks orange, frost cyan, echo casts violet).</summary>
        public void Show(Vector3 world, float amount, DamageKind kind)
        {
            DamageNumberMode current = mode;
            if (current == DamageNumberMode.Off || amount <= 0.0f)
            {
                return;
            }
            if (current == DamageNumberMode.BigHits && kind != DamageKind.Critical && amount < BIG_HIT)
            {
                return;
            }
            Spawn(world, amount, kind, false);
        }

        /// <summary>Damage the hero took (red, 1.2x, shakes); the overlay's vignette pulses with it.</summary>
        public void ShowHeroDamage(Vector3 world, float amount)
        {
            if (mode == DamageNumberMode.Off)
            {
                return;
            }
            Spawn(world, amount, DamageKind.Hero, true);
            onHeroHit?.Invoke();
        }

        /// <summary>Health the hero regained (green, "+N").</summary>
        public void ShowHeal(Vector3 world, float amount)
        {
            if (mode == DamageNumberMode.Off || amount < MIN_HEAL)
            {
                return;
            }
            Spawn(world, amount, DamageKind.Heal, true);
        }

        /// <summary>
        /// A short word over the world ("EXECUTED") with the motion of a number and the colour of
        /// <paramref name="kind"/>; it skips the per-second budget and never merges. Hidden when numbers are Off.
        /// </summary>
        public void ShowText(Vector3 world, string text, DamageKind kind)
        {
            if (string.IsNullOrEmpty(text) || mode == DamageNumberMode.Off)
            {
                return;
            }
            Spawn(world, 0.0f, kind, true, text);
        }

        public void Update(float unscaledDt)
        {
            spawnBudget = MathF.Min(SPAWNS_PER_SECOND, spawnBudget + SPAWNS_PER_SECOND * unscaledDt);
            if (!isReady || aliveCount == 0)
            {
                return;
            }
            Run run = ui.run;
            CameraComponent camera = run.gameCamera;
            if (ReferenceEquals(camera, null) || !document.IsValid())
            {
                return;
            }
            float dt = Time.deltaTime > 0.0f ? unscaledDt : 0.0f;
            float pixelsPerDp = document.pixelsPerDp > 0.0f ? document.pixelsPerDp : 1.0f;
            Vector3 right = run.cameraRig != null && run.cameraRig.entity.IsValid() ? run.cameraRig.entity.transform.right : Vector3.right;
            for (int i = aliveCount - 1; i >= 0; i--)
            {
                ref Number n = ref numbers[aliveList[i]];
                n.age += dt;
                if (n.age >= LIFETIME)
                {
                    Release(i);
                    continue;
                }
                Step(ref n, dt, camera, right, pixelsPerDp);
            }
        }

        private void Spawn(Vector3 world, float amount, DamageKind kind, bool bypassBudget, string word = null)
        {
            if (!isReady)
            {
                return;
            }
            int merged = word == null ? FindMergeTarget(world, kind, MERGE_WINDOW) : -1;
            if (merged >= 0)
            {
                Merge(aliveList[merged], amount);
                return;
            }
            if (!bypassBudget && !TrySpendBudget(world, amount, kind))
            {
                return;
            }
            if (freeCount == 0)
            {
                Release(GetOldest());
            }
            int index = freeList[--freeCount];
            aliveList[aliveCount++] = index;
            Start(ref numbers[index], world, amount, kind, word);
        }

        /// <summary>Takes one spawn from the budget; over budget the hit merges into any live number near it.</summary>
        private bool TrySpendBudget(Vector3 world, float amount, DamageKind kind)
        {
            if (spawnBudget >= 1.0f)
            {
                spawnBudget -= 1.0f;
                return true;
            }
            int overflow = FindMergeTarget(world, kind, LIFETIME);
            if (overflow >= 0)
            {
                Merge(aliveList[overflow], amount);
            }
            return false;
        }

        private static void Start(ref Number n, Vector3 world, float amount, DamageKind kind, string word)
        {
            n.anchor = world;
            n.jitter = (Random.Float() * 2.0f - 1.0f) * X_JITTER;
            n.age = 0.0f;
            n.popAge = 0.0f;
            n.popFrom = kind == DamageKind.Hero ? HERO_POP_SCALE * POP_SCALE : POP_SCALE;
            n.amount = amount;
            n.shown = amount;
            n.kind = kind;
            n.word = word;
            n.textValue = NO_TEXT;
            if (n.elementWord != (word != null))
            {
                n.elementWord = word != null;
                n.element.SetClass(WORD_CLASS, n.elementWord);
            }
            if (n.elementKind != kind)
            {
                SetKindClass(n.element, n.elementKind, false);
                SetKindClass(n.element, kind, true);
                n.elementKind = kind;
            }
            WriteText(ref n);
        }

        /// <summary>Index into the alive list of a recent number of the same kind near <paramref name="world"/>.</summary>
        private int FindMergeTarget(Vector3 world, DamageKind kind, float maxAge)
        {
            int best = -1;
            float bestDistance = MERGE_DISTANCE * MERGE_DISTANCE;
            for (int i = 0; i < aliveCount; i++)
            {
                ref Number n = ref numbers[aliveList[i]];
                if (n.kind != kind || n.age > maxAge || n.word != null)
                {
                    continue;
                }
                float dx = n.anchor.x - world.x;
                float dz = n.anchor.z - world.z;
                float distance = dx * dx + dz * dz;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private void Merge(int index, float amount)
        {
            ref Number n = ref numbers[index];
            n.amount += amount;
            n.popAge = 0.0f;
            n.popFrom = MERGE_POP_SCALE;
            n.age = MathF.Min(n.age, RISE_SECONDS * MERGE_REWIND_SHARE);
        }

        private int GetOldest()
        {
            int oldest = 0;
            float age = -1.0f;
            for (int i = 0; i < aliveCount; i++)
            {
                float a = numbers[aliveList[i]].age;
                if (a > age)
                {
                    age = a;
                    oldest = i;
                }
            }
            return oldest;
        }

        /// <summary>Frees the number at <paramref name="aliveIndex"/> of the alive list.</summary>
        private void Release(int aliveIndex)
        {
            int index = aliveList[aliveIndex];
            aliveList[aliveIndex] = aliveList[--aliveCount];
            Hide(ref numbers[index]);
            freeList[freeCount++] = index;
        }

        private static void Step(ref Number n, float dt, CameraComponent camera, Vector3 right, float pixelsPerDp)
        {
            float rise = UIMotion.CubicOut(n.age / RISE_SECONDS);
            Vector3 world = n.anchor + new Vector3(0.0f, rise * RISE_METRES, 0.0f) + right * n.jitter;
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0.0f)
            {
                Hide(ref n);
                return;
            }
            if (n.shown < n.amount)
            {
                n.shown = MathF.Min(n.amount, n.shown + (n.amount - n.shown) * MathF.Min(1.0f, dt * COUNT_UP_RATE) + 1.0f);
            }
            WriteText(ref n);
            float x = screen.x;
            if (n.kind == DamageKind.Hero && n.age < HERO_SHAKE_SECONDS)
            {
                x += MathF.Sin(n.age * HERO_SHAKE_SPEED) * HERO_SHAKE_PIXELS * pixelsPerDp;
            }
            n.element.style.left = x / pixelsPerDp;
            n.element.style.top = screen.y / pixelsPerDp;
            n.popAge += dt;
            WriteScale(ref n);
            n.opacity.Write(n.age > RISE_SECONDS ? 1.0f - (n.age - RISE_SECONDS) / FADE_SECONDS : 1.0f);
            n.visibility.Write(true);
        }

        /// <summary>The pop: from popFrom down to 1 over POP_SECONDS, as one of the cached scale strings.</summary>
        private static void WriteScale(ref Number n)
        {
            float pop = n.popAge < POP_SECONDS ? Mathf.Lerp(n.popFrom, 1.0f, n.popAge / POP_SECONDS) : 1.0f;
            int scaleIndex = Math.Clamp((int)MathF.Round((pop - SCALE_MIN) / SCALE_STEP), 0, SCALE_STEPS - 1);
            if (scaleIndex != n.scaleIndex)
            {
                n.scaleIndex = scaleIndex;
                n.element.style["transform"] = scales[scaleIndex];
            }
        }

        private static void WriteText(ref Number n)
        {
            if (n.word != null)
            {
                if (n.textValue != WORD_TEXT)
                {
                    n.textValue = WORD_TEXT;
                    n.element.SetText(n.word);
                }
                return;
            }
            int value = Math.Max(1, (int)MathF.Round(n.shown));
            if (value == n.textValue)
            {
                return;
            }
            n.textValue = value;
            n.element.SetText(FormatAmount(n.kind, value));
        }

        private static string FormatAmount(DamageKind kind, int value)
        {
            switch (kind)
            {
                case DamageKind.Critical:
                    return UIText.Crit(value);
                case DamageKind.Heal:
                    return UIText.Plus(value);
                default:
                    return UIText.Int(value);
            }
        }

        /// <summary>Hides a number; a hidden number is opaque again for its next use.</summary>
        private static void Hide(ref Number n)
        {
            if (n.visibility.Write(false))
            {
                n.opacity.Write(1.0f);
            }
        }

        private static void SetKindClass(UIElement element, DamageKind kind, bool on)
        {
            string name = kindClasses[(int)kind];
            if (name.Length > 0)
            {
                element.SetClass(name, on);
            }
        }

        private static string[] BuildScales()
        {
            var result = new string[SCALE_STEPS];
            for (int i = 0; i < SCALE_STEPS; i++)
            {
                float scale = SCALE_MIN + i * SCALE_STEP;
                result[i] = "scale(" + scale.ToString("0.00", CultureInfo.InvariantCulture) + ")";
            }
            return result;
        }
    }
}
