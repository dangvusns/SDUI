using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using SDUI.Animation;

namespace SDUI.Controls;

public class Radio : RadioButton
{
    // Logical (96 DPI) sizes; scaled with LogicalToDeviceUnits so the circle and text fit at 125%/175%.
    private const int RADIOBUTTON_SIZE = 15;
    private const int BOX_LEFT = 3;
    private const int TEXT_GAP = 3;

    // animation managers
    private readonly Animation.AnimationEngine animationManager;

    private readonly Animation.AnimationEngine rippleAnimationManager;

    private int _mouseState;

    // size related variables which should be recalculated onsizechanged
    private Rectangle radioButtonBounds;

    private bool ripple;

    [Browsable(false)]
    private Point _mouseLocation { get; set; }

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

    public Radio()
    {
        SetStyle(
            ControlStyles.UserPaint 
                | ControlStyles.SupportsTransparentBackColor 
                | ControlStyles.OptimizedDoubleBuffer 
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.ResizeRedraw,
            true
        );
        
        DoubleBuffered = true;

        animationManager = new Animation.AnimationEngine { AnimationType = AnimationType.EaseInOut, Increment = 0.06 };
        rippleAnimationManager = new Animation.AnimationEngine(false)
        {
            AnimationType = AnimationType.Linear,
            Increment = 0.10,
            SecondaryIncrement = 0.08,
        };
        animationManager.OnAnimationProgress += sender => Invalidate();
        rippleAnimationManager.OnAnimationProgress += sender => Invalidate();

        CheckedChanged += (sender, args) =>
            animationManager.StartNewAnimation(Checked ? AnimationDirection.In : AnimationDirection.Out);

        SizeChanged += OnSizeChanged;

        Ripple = false;
        _mouseLocation = new Point(-1, -1);
    }

    private int TextLeft => LogicalToDeviceUnits(BOX_LEFT + RADIOBUTTON_SIZE + TEXT_GAP);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = TextLeft + TextRenderer.MeasureText(Text, Font).Width + LogicalToDeviceUnits(2);
        var height = Math.Max(LogicalToDeviceUnits(Ripple ? 30 : 20), Font.Height + LogicalToDeviceUnits(4));
        return new Size(width, height);
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
            _mouseLocation = new Point(-1, -1);
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
            _mouseLocation = args.Location;
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

        RadioButtonRenderer.DrawParentBackground(pevent.Graphics, pevent.ClipRectangle, this);

        var box = radioButtonBounds;
        var centerX = box.X + box.Width / 2f;
        var centerY = box.Y + box.Height / 2f;

        var animationProgress = animationManager.GetProgress();

        var disabledOffColor = Color.LightGray;

        var colorAlpha = Enabled ? (int)(animationProgress * 255.0) : disabledOffColor.A;
        var backgroundAlpha = Enabled
            ? (int)(ColorScheme.BorderColor.A * (1.0 - animationProgress))
            : disabledOffColor.A;
        var animationSize = (float)(animationProgress * box.Width * 0.6f);

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
                var animationSource = new Point((int)centerX, (int)centerY);

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

        using var ellipseBrush = new SolidBrush(ColorScheme.BorderColor);
        graphics.FillEllipse(ellipseBrush, box);

        // draw radiobutton circle
        using var uncheckedBrush = new SolidBrush(
            ColorScheme.BackColor.BlendWith(Enabled ? ColorScheme.BorderColor : disabledOffColor, backgroundAlpha)
        );
        var inner = Rectangle.Inflate(box, -1, -1);
        graphics.FillEllipse(uncheckedBrush, inner);

        if (Enabled)
            graphics.FillEllipse(brush, box);

        if (Checked && animationSize > 0)
        {
            using var dotBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(dotBrush, centerX - animationSize / 2, centerY - animationSize / 2, animationSize, animationSize);
        }

        var textColor = Enabled ? ColorScheme.ForeColor : Color.Gray;

        this.DrawString(
            graphics,
            TextAlign,
            textColor,
            new RectangleF(TextLeft, 0, Math.Max(0, Width - TextLeft), Height)
        );
    }

    private bool IsMouseInCheckArea()
    {
        return radioButtonBounds.Contains(_mouseLocation);
    }

    private void OnSizeChanged(object sender, EventArgs eventArgs)
    {
        UpdateCircleBounds();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateCircleBounds();
    }

    private void UpdateCircleBounds()
    {
        var size = LogicalToDeviceUnits(RADIOBUTTON_SIZE);
        radioButtonBounds = new Rectangle(LogicalToDeviceUnits(BOX_LEFT), (Height - size) / 2, size, size);
    }
}
