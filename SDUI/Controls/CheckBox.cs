using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using SDUI.Animation;

namespace SDUI.Controls
{
    public class CheckBox : System.Windows.Forms.CheckBox
    {
        // Logical (96 DPI) sizes; scaled with LogicalToDeviceUnits so the box and text fit at 125%/175%.
        private const int BOX_SIZE = 14;
        private const int BOX_LEFT = 3;
        private const int TEXT_GAP = 3;

        private static readonly PointF[] CHECKMARK_LINE = { new(1, 6), new(5, 10), new(12, 3) };

        private readonly Animation.AnimationEngine animationManager;

        private readonly Animation.AnimationEngine rippleAnimationManager;

        private Rectangle boxRectangle;

        private bool ripple;

        [Browsable(false)]
        public int Depth { get; set; }

        [Browsable(false)]
        public Point MouseLocation { get; set; }

        private int _mouseState { get; set; }

        public override bool AutoSize
        {
            get { return base.AutoSize; }
            set
            {
                base.AutoSize = value;
                if (value)
                {
                    Size = new Size(10, 10);
                }
            }
        }

        [Category("Behavior")]
        public bool Ripple
        {
            get { return ripple; }
            set
            {
                if (ripple == value)
                    return;

                ripple = value;
                AutoSize = AutoSize; //Make AutoSize directly set the bounds.

                if (value)
                {
                    Margin = new Padding(0);
                }

                Invalidate();
            }
        }

        public CheckBox()
        {
            //SetExtendedState(ExtendedStates.UserPreferredSizeCache, true);

            SetStyle(
                ControlStyles.UserPaint
                    | ControlStyles.SupportsTransparentBackColor
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.ResizeRedraw,
                true
            );

            SetStyle(ControlStyles.FixedHeight | ControlStyles.Selectable, false);
            
            DoubleBuffered = true;

            animationManager = new Animation.AnimationEngine
            {
                AnimationType = AnimationType.EaseInOut,
                Increment = 0.10,
            };
            rippleAnimationManager = new Animation.AnimationEngine(false)
            {
                AnimationType = AnimationType.Linear,
                Increment = 0.10,
                SecondaryIncrement = 0.07,
            };
            animationManager.OnAnimationProgress += sender => Invalidate();
            rippleAnimationManager.OnAnimationProgress += sender => Invalidate();

            CheckedChanged += (sender, args) =>
            {
                animationManager.StartNewAnimation(Checked ? AnimationDirection.In : AnimationDirection.Out);
            };

            Ripple = false;
            MouseLocation = new Point(-1, -1);
        }

        private bool IsMouseInCheckArea()
        {
            return boxRectangle.Contains(MouseLocation);
        }

        private int TextLeft => LogicalToDeviceUnits(BOX_LEFT + BOX_SIZE + TEXT_GAP);

        public override Size GetPreferredSize(Size proposedSize)
        {
            var w = TextLeft + TextRenderer.MeasureText(Text, Font).Width + LogicalToDeviceUnits(2);
            var h = Math.Max(LogicalToDeviceUnits(Ripple ? 30 : 20), Font.Height + LogicalToDeviceUnits(4));
            return new Size(w, h);
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            if (DesignMode)
                return;

            _mouseState = 0;
            MouseEnter += (sender, args) =>
            {
                _mouseState = 1;
            };
            MouseLeave += (sender, args) =>
            {
                MouseLocation = new Point(-1, -1);
                _mouseState = 0;
            };
            MouseDown += (sender, args) =>
            {
                _mouseState = 2;

                if (Ripple && args.Button == MouseButtons.Left && IsMouseInCheckArea())
                {
                    rippleAnimationManager.SecondaryIncrement = 0;
                    rippleAnimationManager.StartNewAnimation(AnimationDirection.InOutIn, new object[] { Checked });
                }
            };
            MouseUp += (sender, args) =>
            {
                _mouseState = 1;
                rippleAnimationManager.SecondaryIncrement = 0.08;
            };
            MouseMove += (sender, args) =>
            {
                MouseLocation = args.Location;
                Cursor = IsMouseInCheckArea() ? Cursors.Hand : Cursors.Default;
            };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014) // WM_ERASEBKGND
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.SystemDefault;

            CheckBoxRenderer.DrawParentBackground(pevent.Graphics, ClientRectangle, this);

            var box = boxRectangle;

            double animationProgress = animationManager.GetProgress();

            var disabledOffColor = ColorScheme.BorderColor;

            int colorAlpha = Enabled ? (int)(animationProgress * 255.0) : disabledOffColor.A;
            int backgroundAlpha = Enabled
                ? (int)(ColorScheme.BorderColor.A * (1.0 - animationProgress))
                : disabledOffColor.A;

            using var brush = new SolidBrush(
                Color.FromArgb(colorAlpha, Enabled ? ColorScheme.AccentColor : disabledOffColor)
            );
            using var pen = new Pen(brush.Color);

            // draw ripple animation
            if (Ripple && rippleAnimationManager.IsAnimating())
            {
                for (int i = 0; i < rippleAnimationManager.GetAnimationCount(); i++)
                {
                    var animationValue = rippleAnimationManager.GetProgress(i);
                    var animationSource = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
                    using var rippleBrush = new SolidBrush(
                        Color.FromArgb(
                            (int)((animationValue * 40)),
                            ((bool)rippleAnimationManager.GetData(i)[0]) ? Color.Black : brush.Color
                        )
                    );
                    var rippleHeight = (Height % 2 == 0) ? Height - 3 : Height - 2;
                    var rippleSize =
                        (rippleAnimationManager.GetDirection(i) == AnimationDirection.InOutIn)
                            ? (int)(rippleHeight * (0.8d + (0.2d * animationValue)))
                            : rippleHeight;

                    using var path = DrawingExtensions.CreateRoundPath(
                        animationSource.X - rippleSize / 2,
                        animationSource.Y - rippleSize / 2,
                        rippleSize,
                        rippleSize,
                        rippleSize / 2
                    );
                    graphics.FillPath(rippleBrush, path);
                }
            }

            var radius = Math.Max(2, LogicalToDeviceUnits(2));
            using (var checkmarkPath = DrawingExtensions.CreateRoundPath(box.X, box.Y, box.Width, box.Height, radius))
            using (var borderBrush = new SolidBrush(ColorScheme.BorderColor))
            using (var borderPen = new Pen(ColorScheme.BorderColor))
            {
                graphics.FillPath(borderBrush, checkmarkPath);
                graphics.DrawPath(borderPen, checkmarkPath);

                graphics.FillPath(brush, checkmarkPath);
                graphics.DrawPath(pen, checkmarkPath);
            }

            if (animationProgress > 0)
            {
                // Check mark revealed left-to-right with the (instant when animations are off) progress
                var scale = box.Width / (float)BOX_SIZE;
                var points = new PointF[CHECKMARK_LINE.Length];
                for (var i = 0; i < points.Length; i++)
                    points[i] = new PointF(box.X + CHECKMARK_LINE[i].X * scale, box.Y + CHECKMARK_LINE[i].Y * scale);

                using var clip = graphics.Clip;
                graphics.SetClip(new RectangleF(box.X, box.Y, (float)(box.Width * animationProgress), box.Height));
                using var markPen = new Pen(Enabled ? Color.White : Color.DarkGray, Math.Max(2f, 2 * scale));
                graphics.DrawLines(markPen, points);
                graphics.Clip = clip;
            }

            // draw checkbox text
            var textColor = Enabled ? ColorScheme.ForeColor : Color.Gray;

            this.DrawString(
                graphics,
                TextAlign,
                textColor,
                new RectangleF(TextLeft, 0, Math.Max(0, Width - TextLeft), Height)
            );

            if (ColorScheme.DrawDebugBorders)
            {
                using var redPen = new Pen(Color.Red, 1);
                redPen.Alignment = PenAlignment.Outset;
                graphics.DrawRectangle(redPen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            UpdateBoxRectangle();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            UpdateBoxRectangle();
        }

        private void UpdateBoxRectangle()
        {
            var size = LogicalToDeviceUnits(BOX_SIZE);
            boxRectangle = new Rectangle(LogicalToDeviceUnits(BOX_LEFT), (Height - size) / 2, size, size);
        }
    }
}
