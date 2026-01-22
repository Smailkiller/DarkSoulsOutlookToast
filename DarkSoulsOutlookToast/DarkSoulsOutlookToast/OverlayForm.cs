using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DarkSoulsOutlookToast
{
    public class OverlayForm : Form
    {
        private readonly bool _isIncoming;
        private readonly string _text;

        private readonly System.Windows.Forms.Timer _timer;
        private readonly DateTime _start;

        // Тайминги (секунды)
        private const float Total = 3.8f;

        // Плавное появление/исчезание баннера
        private const float FadeIn = 0.8f;   // было 0.6f, сделали более плавно
        private const float FadeOut = 0.9f;  // было 0.8f, сделали более плавно

        // Плавный "подъезд" баннера
        private const float SlideIn = 1.1f;  // длительность подъезда по Y

        // Искры только для "отправки"
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly Random _rng = new Random();

        public OverlayForm(string text, bool isIncoming)
        {
            _text = text ?? string.Empty;
            _isIncoming = isIncoming;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;

            // Баннер-окно (не полноэкранный слой)
            var screen = Screen.PrimaryScreen.Bounds;
            int bandH = 160;

            Width = screen.Width;
            Height = bandH;
            Left = screen.Left;
            Top = screen.Top + (screen.Height / 2) - (bandH / 2);

            TopMost = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            BackColor = Color.Black;

            _start = DateTime.UtcNow;

            if (!_isIncoming)
            {
                for (int i = 0; i < 15; i++)
                    _sparks.Add(NewSpark());
            }

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 16; // ~60 FPS
            _timer.Tick += (s, e) =>
            {
                Invalidate();
                ForceTopMost();

                if (Elapsed() >= Total)
                {
                    _timer.Stop();
                    Close();
                }
            };

            // страховка: Esc закрывает
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    try { _timer.Stop(); } catch { }
                    try { Close(); } catch { }
                }
            };
        }

        // Окно не должно блокировать клики, и не должно быть в Alt-Tab
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TRANSPARENT = 0x20;    // клики проходят "сквозь"
                const int WS_EX_TOOLWINDOW = 0x80;     // нет в Alt-Tab
                const int WS_EX_TOPMOST = 0x00000008;  // topmost на уровне стиля
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ForceTopMost();
        }

        public void RunAutoClose()
        {
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float t = Elapsed();
                float alpha = GlobalAlpha(t);

                DrawBand(g, t, alpha);

                if (_isIncoming)
                {
                    DrawDeathText(g, t, alpha);
                }
                else
                {
                    DrawVictoryLine(g, t, alpha);
                    DrawVictoryText(g, t, alpha);
                    DrawSparks(g, alpha);
                }
            }
            catch
            {
                try { Close(); } catch { }
            }
        }

        private float Elapsed()
        {
            return (float)(DateTime.UtcNow - _start).TotalSeconds;
        }

        private float GlobalAlpha(float t)
        {
            if (t < FadeIn)
                return EaseOut(t / FadeIn);

            float tailStart = Total - FadeOut;
            if (t > tailStart)
            {
                float k = (t - tailStart) / FadeOut;
                return 1.0f - EaseIn(k);
            }

            return 1.0f;
        }

        // ===== Баннер: чёрная полоса с мягким появлением и лёгким "подъездом" =====
        private void DrawBand(Graphics g, float t, float alpha)
        {
            // Подъезд: сверху вниз на несколько пикселей
            float slideK = Math.Min(1.0f, t / SlideIn);
            slideK = EaseOut(slideK);

            int offsetY = (int)(14 * (1.0f - slideK)); // было 12, чуть сильнее
            int bandY = offsetY;

            var rect = new Rectangle(0, bandY, Width, Height);

            // Основная тёмная заливка
            using (var br = new SolidBrush(Color.FromArgb((int)(alpha * 235), 0, 0, 0)))
                g.FillRectangle(br, rect);

            // Внутренний мягкий градиент (чтобы полоса была "киношной", а не плоской)
            using (var lg = new LinearGradientBrush(rect,
                       Color.FromArgb((int)(alpha * 30), 255, 255, 255),
                       Color.FromArgb(0, 0, 0, 0),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(lg, rect);
            }

            // Мягкие кромки сверху/снизу
            using (var pen = new Pen(Color.FromArgb((int)(alpha * 45), 255, 255, 255), 1))
            {
                g.DrawLine(pen, 0, rect.Top + 1, Width, rect.Top + 1);
                g.DrawLine(pen, 0, rect.Bottom - 2, Width, rect.Bottom - 2);
            }
        }

        // ===== Получение: красный текст =====
        private void DrawDeathText(Graphics g, float t, float alpha)
        {
            float k = Math.Min(1.0f, t / Total);
            float scale = Lerp(1.08f, 1.00f, EaseOut(k));

            float localAlpha = alpha * (t < 0.05f ? 0f : 1f);
            var fill = Color.FromArgb((int)(localAlpha * 255), 188, 28, 28);

            float baseSize = Width >= 1400 ? 98f : 72f;
            float fontSize = baseSize * scale;

            using (var font = new Font("Garamond", fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                var center = new PointF(Width / 2f, Height / 2f);

                DrawGlowText(
                    g,
                    _text.ToUpperInvariant(),
                    font,
                    center,
                    glowColor: Color.FromArgb((int)(localAlpha * 115), 188, 28, 28),
                    strokeColor: Color.FromArgb((int)(localAlpha * 170), 0, 0, 0),
                    fillColor: fill);
            }
        }

        // ===== Отправка: золотая линия =====
        private void DrawVictoryLine(Graphics g, float t, float alpha)
        {
            float k = Math.Min(1.0f, t / Total);
            float widthK = EaseInOut(k);

            float localAlpha = alpha;
            if (k < 0.15f) localAlpha *= k / 0.15f;
            if (k > 0.85f) localAlpha *= (1.0f - (k - 0.85f) / 0.15f);

            int y = Height / 2;
            int lineH = 3;

            int w = (int)(Width * widthK);
            if (w < 2) return;

            int x = (Width - w) / 2;
            var rect = new Rectangle(x, y - lineH / 2, w, lineH);

            using (var brush = new LinearGradientBrush(
                rect,
                Color.FromArgb(0, 255, 207, 64),
                Color.FromArgb(0, 255, 207, 64),
                0f))
            {
                var cb = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 255, 207, 64),
                        Color.FromArgb((int)(localAlpha * 255), 255, 207, 64),
                        Color.FromArgb(0, 255, 207, 64)
                    },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                brush.InterpolationColors = cb;

                g.FillRectangle(brush, rect);

                using (var halo = new SolidBrush(Color.FromArgb((int)(localAlpha * 50), 255, 207, 64)))
                    g.FillRectangle(halo, new Rectangle(x, y - 10, w, 20));
            }
        }

        // ===== Отправка: золотой текст =====
        private void DrawVictoryText(Graphics g, float t, float alpha)
        {
            float k = Math.Min(1.0f, t / Total);
            float scale = Lerp(0.92f, 1.00f, EaseOut(k));
            float localAlpha = alpha * (t < 0.05f ? 0f : 1f);

            var fill = Color.FromArgb((int)(localAlpha * 255), 255, 207, 64);

            float baseSize = Width >= 1400 ? 88f : 66f;
            float fontSize = baseSize * scale;

            using (var font = new Font("Garamond", fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                var center = new PointF(Width / 2f, Height / 2f);

                DrawGlowText(
                    g,
                    _text.ToUpperInvariant(),
                    font,
                    center,
                    glowColor: Color.FromArgb((int)(localAlpha * 145), 255, 207, 64),
                    strokeColor: Color.FromArgb((int)(localAlpha * 145), 0, 0, 0),
                    fillColor: fill);
            }
        }

        private void DrawSparks(Graphics g, float alpha)
        {
            for (int i = 0; i < _sparks.Count; i++)
            {
                var s = _sparks[i];
                s.Update(0.016f, _rng, Width, Height);
                _sparks[i] = s;

                float a = alpha * s.Alpha;
                int cA = (int)(a * 255);
                if (cA <= 0) continue;

                using (var br = new SolidBrush(Color.FromArgb(cA, 255, 207, 64)))
                    g.FillEllipse(br, s.X, s.Y, s.Size, s.Size);

                using (var br2 = new SolidBrush(Color.FromArgb((int)(a * 60), 255, 207, 64)))
                    g.FillEllipse(br2, s.X - 6, s.Y - 6, s.Size + 12, s.Size + 12);
            }
        }

        private Spark NewSpark()
        {
            float x = (float)(Width * (0.10 + _rng.NextDouble() * 0.80));
            float y = (float)(Height * (0.15 + _rng.NextDouble() * 0.70));
            float vy = (float)(-20 - _rng.NextDouble() * 40);
            float drift = (float)(-10 + _rng.NextDouble() * 20);
            float size = (float)(2 + _rng.NextDouble() * 3);
            float phase = (float)(_rng.NextDouble() * Math.PI * 2);
            return new Spark(x, y, vy, drift, size, phase);
        }

        private void DrawGlowText(Graphics g, string text, Font font, PointF center,
            Color glowColor, Color strokeColor, Color fillColor)
        {
            var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Glow
            for (int r = 6; r >= 1; r--)
            {
                var c = Color.FromArgb(Math.Max(0, glowColor.A / (r + 1)), glowColor);
                using (var b = new SolidBrush(c))
                {
                    g.DrawString(text, font, b, new PointF(center.X + r, center.Y), fmt);
                    g.DrawString(text, font, b, new PointF(center.X - r, center.Y), fmt);
                    g.DrawString(text, font, b, new PointF(center.X, center.Y + r), fmt);
                    g.DrawString(text, font, b, new PointF(center.X, center.Y - r), fmt);
                }
            }

            // Stroke
            using (var strokeBrush = new SolidBrush(strokeColor))
            {
                g.DrawString(text, font, strokeBrush, new PointF(center.X + 2, center.Y + 1), fmt);
                g.DrawString(text, font, strokeBrush, new PointF(center.X - 2, center.Y + 1), fmt);
                g.DrawString(text, font, strokeBrush, new PointF(center.X + 2, center.Y - 1), fmt);
                g.DrawString(text, font, strokeBrush, new PointF(center.X - 2, center.Y - 1), fmt);
            }

            // Fill
            using (var fillBrush = new SolidBrush(fillColor))
                g.DrawString(text, font, fillBrush, center, fmt);
        }

        private float Lerp(float a, float b, float t) => a + (b - a) * t;
        private float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        private float EaseIn(float t) => t * t;
        private float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2f) / 2f;

        private struct Spark
        {
            public float X;
            public float Y;
            public float VY;
            public float Drift;
            public float Size;
            public float Phase;

            public Spark(float x, float y, float vy, float drift, float size, float phase)
            {
                X = x;
                Y = y;
                VY = vy;
                Drift = drift;
                Size = size;
                Phase = phase;
            }

            public float Alpha
            {
                get
                {
                    float a = 0.35f + 0.25f * (float)Math.Sin(Phase);
                    if (a < 0) a = 0;
                    if (a > 1) a = 1;
                    return a;
                }
            }

            public void Update(float dt, Random rng, int w, int h)
            {
                Y += VY * dt;
                X += Drift * dt;
                Phase += dt * 6f;

                if (Y < 0)
                {
                    X = (float)(w * (0.10 + rng.NextDouble() * 0.80));
                    Y = (float)(h * (0.65 + rng.NextDouble() * 0.25));
                    VY = (float)(-20 - rng.NextDouble() * 40);
                    Drift = (float)(-10 + rng.NextDouble() * 20);
                    Size = (float)(2 + rng.NextDouble() * 3);
                    Phase = (float)(rng.NextDouble() * Math.PI * 2);
                }
            }
        }

        // ===== Принудительно держим окно поверх всего =====

        private void ForceTopMost()
        {
            if (!IsHandleCreated) return;

            // Без активации (не ворует фокус)
            SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
    }
}
