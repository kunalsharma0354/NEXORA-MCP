namespace NEXORA_MCP
{
    public delegate void AnimationUpdate(float t);

    /// <summary>
    /// Global 60fps animation engine. Starts/stops a shared timer that
    /// drives every registered animation. Easing helpers live in UITheme.
    /// </summary>
    public static class AnimationManager
    {
        private sealed class Animation
        {
            public required Control Target;
            public required AnimationUpdate Update;
            public required Func<float, float> Easing;
            public int DurationMs;
            public long StartMs;
            public Action? OnComplete;
            public bool Completed;
        }

        private static readonly List<Animation> _animations = new();
        private static readonly System.Windows.Forms.Timer _timer;
        private static readonly object _lock = new();

        static AnimationManager()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += OnTick;
        }

        private static void OnTick(object? sender, EventArgs e)
        {
            long now = Environment.TickCount64;
            List<Animation> toRemove = new();

            lock (_lock)
            {
                foreach (var a in _animations)
                {
                    if (a.Completed) { toRemove.Add(a); continue; }

                    float t = (now - a.StartMs) / (float)a.DurationMs;
                    t = Math.Clamp(t, 0f, 1f);
                    float eased = a.Easing(t);
                    try
                    {
                        a.Update(eased);
                        a.Target.Invalidate();
                    }
                    catch { }

                    if (t >= 1f)
                    {
                        a.Completed = true;
                        toRemove.Add(a);
                        try { a.OnComplete?.Invoke(); } catch { }
                    }
                }

                foreach (var a in toRemove)
                    _animations.Remove(a);

                if (_animations.Count == 0)
                    _timer.Stop();
            }
        }

        // ─── Public API ─────────────────────────────────────────────────────
        private static void Start(Control target, AnimationUpdate update, int durationMs,
            Func<float, float> easing, Action? onComplete = null)
        {
            lock (_lock)
            {
                _animations.Add(new Animation
                {
                    Target = target,
                    Update = update,
                    Easing = easing,
                    DurationMs = Math.Max(1, durationMs),
                    StartMs = Environment.TickCount64,
                    OnComplete = onComplete
                });
                _timer.Start();
            }
        }

        /// <summary>Register a UI-thread-safe animation on a control.</summary>
        public static void Animate(Control target, AnimationUpdate update, int durationMs,
            Func<float, float>? easing = null, Action? onComplete = null)
        {
            if (target.InvokeRequired)
            {
                target.BeginInvoke(new Action(() => Animate(target, update, durationMs, easing, onComplete)));
                return;
            }
            Start(target, update, durationMs, easing ?? UITheme.EaseOutCubic, onComplete);
        }

        /// <summary>Animate an integer value from start to end.</summary>
        public static void AnimateInt(Control target, int from, int to, int durationMs,
            Action<int> onValue, Func<float, float>? easing = null, Action? onComplete = null)
        {
            Animate(target, t => onValue((int)Math.Round(from + (to - from) * t)), durationMs, easing, onComplete);
        }

        /// <summary>Animate a float value from start to end.</summary>
        public static void AnimateFloat(Control target, float from, float to, int durationMs,
            Action<float> onValue, Func<float, float>? easing = null, Action? onComplete = null)
        {
            Animate(target, t => onValue(from + (to - from) * t), durationMs, easing, onComplete);
        }
    }
}