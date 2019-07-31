using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using VADEdit.Converters;

namespace VADEdit
{
    public partial class ImageButton : Button
    {
        #region Events
        public event RoutedEventHandler LongPress;
        public event EventHandler<PressHoldEventArgs> PressHold;
        public new event EventHandler<MouseButtonEventArgs> PreviewMouseLeftButtonDown;
        public new event EventHandler<TouchEventArgs> PreviewTouchDown;
        #endregion

        #region Properties
        #region Background
        public new Brush Background
        {
            get { return (Brush)GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        public static readonly new DependencyProperty BackgroundProperty =
            DependencyProperty.Register("Background", typeof(Brush), typeof(ImageButton), new PropertyMetadata(SystemColors.ControlBrush));

        public Brush DisabledBackground
        {
            get { return (Brush)GetValue(DisabledBackgroundProperty); }
            set { SetValue(DisabledBackgroundProperty, value); }
        }
        public static readonly DependencyProperty DisabledBackgroundProperty =
            DependencyProperty.Register("DisabledBackground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush PressedBackground
        {
            get { return (Brush)GetValue(PressedBackgroundProperty); }
            set { SetValue(PressedBackgroundProperty, value); }
        }
        public static readonly DependencyProperty PressedBackgroundProperty =
            DependencyProperty.Register("PressedBackground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush CheckedBackground
        {
            get { return (Brush)GetValue(CheckedBackgroundProperty); }
            set { SetValue(CheckedBackgroundProperty, value); }
        }
        public static readonly DependencyProperty CheckedBackgroundProperty =
            DependencyProperty.Register("CheckedBackground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush MouseOverBackground
        {
            get { return (Brush)GetValue(MouseOverBackgroundProperty); }
            set { SetValue(MouseOverBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MouseOverBackgroundProperty =
            DependencyProperty.Register("MouseOverBackground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));
        #endregion

        #region Foreground
        public new Brush Foreground
        {
            get { return (Brush)GetValue(ForegroundProperty); }
            set { SetValue(ForegroundProperty, value); }
        }
        public static readonly new DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(Brushes.White));

        public Brush DisabledForeground
        {
            get { return (Brush)GetValue(DisabledForegroundProperty); }
            set { SetValue(DisabledForegroundProperty, value); }
        }
        public static readonly DependencyProperty DisabledForegroundProperty =
            DependencyProperty.Register("DisabledForeground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(SystemColors.GrayTextBrush));

        public Brush PressedForeground
        {
            get { return (Brush)GetValue(PressedForegroundProperty); }
            set { SetValue(PressedForegroundProperty, value); }
        }
        public static readonly DependencyProperty PressedForegroundProperty =
            DependencyProperty.Register("PressedForeground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush CheckedForeground
        {
            get { return (Brush)GetValue(CheckedForegroundProperty); }
            set { SetValue(CheckedForegroundProperty, value); }
        }
        public static readonly DependencyProperty CheckedForegroundProperty =
            DependencyProperty.Register("CheckedForeground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush MouseOverForeground
        {
            get { return (Brush)GetValue(MouseOverForegroundProperty); }
            set { SetValue(MouseOverForegroundProperty, value); }
        }

        public static readonly DependencyProperty MouseOverForegroundProperty =
            DependencyProperty.Register("MouseOverForeground", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));
        #endregion

        #region BorderBrush
        public Brush DisabledBorderBrush
        {
            get { return (Brush)GetValue(DisabledBorderBrushProperty); }
            set { SetValue(DisabledBorderBrushProperty, value); }
        }
        public static readonly DependencyProperty DisabledBorderBrushProperty =
            DependencyProperty.Register("DisabledBorderBrush", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush PressedBorderBrush
        {
            get { return (Brush)GetValue(PressedBorderBrushProperty); }
            set { SetValue(PressedBorderBrushProperty, value); }
        }
        public static readonly DependencyProperty PressedBorderBrushProperty =
            DependencyProperty.Register("PressedBorderBrush", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush CheckedBorderBrush
        {
            get { return (Brush)GetValue(CheckedBorderBrushProperty); }
            set { SetValue(CheckedBorderBrushProperty, value); }
        }
        public static readonly DependencyProperty CheckedBorderBrushProperty =
            DependencyProperty.Register("CheckedBorderBrush", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));

        public Brush MouseOverBorderBrush
        {
            get { return (Brush)GetValue(MouseOverBorderBrushProperty); }
            set { SetValue(MouseOverBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty MouseOverBorderBrushProperty =
            DependencyProperty.Register("MouseOverBorderBrush", typeof(Brush), typeof(ImageButton), new PropertyMetadata(null));
        #endregion

        #region BorderThickness
        public new Thickness BorderThickness
        {
            get { return (Thickness)GetValue(BorderThicknessProperty); }
            set { SetValue(BorderThicknessProperty, value); }
        }
        public static readonly new DependencyProperty BorderThicknessProperty =
            DependencyProperty.Register("BorderThickness", typeof(Thickness), typeof(ImageButton), new PropertyMetadata(new Thickness(0)));

        public Thickness DisabledBorderThickness
        {
            get { return (Thickness)GetValue(DisabledBorderThicknessProperty); }
            set { SetValue(DisabledBorderThicknessProperty, value); }
        }
        public static readonly DependencyProperty DisabledBorderThicknessProperty =
            DependencyProperty.Register("DisabledBorderThickness", typeof(Thickness), typeof(ImageButton), new PropertyMetadata(new Thickness(-1)));

        public Thickness PressedBorderThickness
        {
            get { return (Thickness)GetValue(PressedBorderThicknessProperty); }
            set { SetValue(PressedBorderThicknessProperty, value); }
        }
        public static readonly DependencyProperty PressedBorderThicknessProperty =
            DependencyProperty.Register("PressedBorderThickness", typeof(Thickness?), typeof(ImageButton), new PropertyMetadata(new Thickness(-1)));


        public Thickness CheckedBorderThickness
        {
            get { return (Thickness)GetValue(CheckedBorderThicknessProperty); }
            set { SetValue(CheckedBorderThicknessProperty, value); }
        }
        public static readonly DependencyProperty CheckedBorderThicknessProperty =
            DependencyProperty.Register("CheckedBorderThickness", typeof(Thickness), typeof(ImageButton), new PropertyMetadata(new Thickness(-1)));

        public Thickness MouseOverBorderThickness
        {
            get { return (Thickness)GetValue(MouseOverBorderThicknessProperty); }
            set { SetValue(MouseOverBorderThicknessProperty, value); }
        }

        public static readonly DependencyProperty MouseOverBorderThicknessProperty =
            DependencyProperty.Register("MouseOverBorderThickness", typeof(Thickness), typeof(ImageButton), new PropertyMetadata(new Thickness(-1)));
        #endregion

        #region Icon
        public ImageSource Icon
        {
            get { return (ImageSource)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));

        public ImageSource DisabledIcon
        {
            get { return (ImageSource)GetValue(DisabledIconProperty); }
            set { SetValue(DisabledIconProperty, value); }
        }
        public static readonly DependencyProperty DisabledIconProperty =
            DependencyProperty.Register("DisabledIcon", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));

        public ImageSource PressedIcon
        {
            get { return (ImageSource)GetValue(PressedIconProperty); }
            set { SetValue(PressedIconProperty, value); }
        }
        public static readonly DependencyProperty PressedIconProperty =
            DependencyProperty.Register("PressedIcon", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));

        public ImageSource CheckedIcon
        {
            get { return (ImageSource)GetValue(CheckedIconProperty); }
            set { SetValue(CheckedIconProperty, value); }
        }

        public static readonly DependencyProperty CheckedIconProperty =
            DependencyProperty.Register("CheckedIcon", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));

        public ImageSource MouseOverIcon
        {
            get { return (ImageSource)GetValue(MouseOverIconProperty); }
            set { SetValue(MouseOverIconProperty, value); }
        }

        public static readonly DependencyProperty MouseOverIconProperty =
            DependencyProperty.Register("MouseOverIcon", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));
        #endregion

        #region IconEffect
        public Effect IconEffect
        {
            get { return (Effect)GetValue(IconEffectProperty); }
            set { SetValue(IconEffectProperty, value); }
        }
        public static readonly DependencyProperty IconEffectProperty =
            DependencyProperty.Register("IconEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect DisabledIconEffect
        {
            get { return (Effect)GetValue(DisabledIconEffectProperty); }
            set { SetValue(DisabledIconEffectProperty, value); }
        }
        public static readonly DependencyProperty DisabledIconEffectProperty =
            DependencyProperty.Register("DisabledIconEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect PressedIconEffect
        {
            get { return (Effect)GetValue(PressedIconEffectProperty); }
            set { SetValue(PressedIconEffectProperty, value); }
        }
        public static readonly DependencyProperty PressedIconEffectProperty =
            DependencyProperty.Register("PressedIconEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect CheckedIconEffect
        {
            get { return (Effect)GetValue(CheckedIconEffectProperty); }
            set { SetValue(CheckedIconEffectProperty, value); }
        }
        public static readonly DependencyProperty CheckedIconEffectProperty =
            DependencyProperty.Register("CheckedIconEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect MouseOverIconEffect
        {
            get { return (Effect)GetValue(MouseOverIconEffectProperty); }
            protected set { SetValue(MouseOverIconEffectProperty, value); }
        }

        public static readonly DependencyProperty MouseOverIconEffectProperty =
            DependencyProperty.Register("MouseOverIconEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));
        #endregion

        #region ContentEffect
        public Effect ContentEffect
        {
            get { return (Effect)GetValue(ContentEffectProperty); }
            set { SetValue(ContentEffectProperty, value); }
        }
        public static readonly DependencyProperty ContentEffectProperty =
            DependencyProperty.Register("ContentEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect DisabledContentEffect
        {
            get { return (Effect)GetValue(DisabledContentEffectProperty); }
            set { SetValue(DisabledContentEffectProperty, value); }
        }
        public static readonly DependencyProperty DisabledContentEffectProperty =
            DependencyProperty.Register("DisabledContentEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect PressedContentEffect
        {
            get { return (Effect)GetValue(PressedContentEffectProperty); }
            set { SetValue(PressedContentEffectProperty, value); }
        }
        public static readonly DependencyProperty PressedContentEffectProperty =
            DependencyProperty.Register("PressedContentEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect CheckedContentEffect
        {
            get { return (Effect)GetValue(CheckedContentEffectProperty); }
            set { SetValue(CheckedContentEffectProperty, value); }
        }
        public static readonly DependencyProperty CheckedContentEffectProperty =
            DependencyProperty.Register("CheckedContentEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect MouseOverContentEffect
        {
            get { return (Effect)GetValue(MouseOverContentEffectProperty); }
            set { SetValue(MouseOverContentEffectProperty, value); }
        }

        public static readonly DependencyProperty MouseOverContentEffectProperty =
            DependencyProperty.Register("MouseOverContentEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));


        #endregion

        #region Effect
        public new Effect Effect
        {
            get { return (Effect)GetValue(EffectProperty); }
            set { SetValue(EffectProperty, value); }
        }
        public static readonly new DependencyProperty EffectProperty =
            DependencyProperty.Register("Effect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect DisabledEffect
        {
            get { return (Effect)GetValue(DisabledEffectProperty); }
            set { SetValue(DisabledEffectProperty, value); }
        }
        public static readonly DependencyProperty DisabledEffectProperty =
            DependencyProperty.Register("DisabledEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect PressedEffect
        {
            get { return (Effect)GetValue(PressedEffectProperty); }
            set { SetValue(PressedEffectProperty, value); }
        }
        public static readonly DependencyProperty PressedEffectProperty =
            DependencyProperty.Register("PressedEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect CheckedEffect
        {
            get { return (Effect)GetValue(CheckedEffectProperty); }
            set { SetValue(CheckedEffectProperty, value); }
        }
        public static readonly DependencyProperty CheckedEffectProperty =
            DependencyProperty.Register("CheckedEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));

        public Effect MouseOverEffect
        {
            get { return (Effect)GetValue(MouseOverEffectProperty); }
            set { SetValue(MouseOverEffectProperty, value); }
        }

        public static readonly DependencyProperty MouseOverEffectProperty =
            DependencyProperty.Register("MouseOverEffect", typeof(Effect), typeof(ImageButton), new PropertyMetadata(null));


        #endregion

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(ImageButton), new PropertyMetadata(new CornerRadius(0)));

        public new FontFamily FontFamily
        {
            get { return (FontFamily)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }
        public static readonly new DependencyProperty FontFamilyProperty =
            DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(ImageButton), new PropertyMetadata(new FontFamily("Segoe UI")));

        public new FontWeight FontWeight
        {
            get { return (FontWeight)GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }
        public static readonly new DependencyProperty FontWeightProperty =
            DependencyProperty.Register("FontWeight", typeof(FontWeight), typeof(ImageButton), new PropertyMetadata(FontWeights.Bold));

        public new double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }
        public static readonly new DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(ImageButton), new PropertyMetadata(15.0));

        public bool Checkable
        {
            get { return (bool)GetValue(CheckableProperty); }
            set { SetValue(CheckableProperty, value); }
        }
        public static readonly DependencyProperty CheckableProperty =
            DependencyProperty.Register("Checkable", typeof(bool), typeof(ImageButton), new PropertyMetadata(false, (o, e) => { (o as ImageButton).IsChecked = false; }));

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(ImageButton), new PropertyMetadata(false, null, (o, e) => (bool)e && (o as ImageButton).Checkable));

        public new HorizontalAlignment HorizontalContentAlignment
        {
            get { return (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty); }
            set { SetValue(HorizontalContentAlignmentProperty, value); }
        }
        public static readonly new DependencyProperty HorizontalContentAlignmentProperty =
            DependencyProperty.Register("HorizontalContentAlignment", typeof(HorizontalAlignment), typeof(ImageButton), new PropertyMetadata(HorizontalAlignment.Center));


        public new VerticalAlignment VerticalContentAlignment
        {
            get { return (VerticalAlignment)GetValue(VerticalContentAlignmentProperty); }
            set { SetValue(VerticalContentAlignmentProperty, value); }
        }
        public static readonly new DependencyProperty VerticalContentAlignmentProperty =
            DependencyProperty.Register("VerticalContentAlignment", typeof(VerticalAlignment), typeof(ImageButton), new PropertyMetadata(VerticalAlignment.Center));

        public HorizontalAlignment HorizontalIconAlignment
        {
            get { return (HorizontalAlignment)GetValue(HorizontalIconAlignmentProperty); }
            set { SetValue(HorizontalIconAlignmentProperty, value); }
        }
        public static readonly DependencyProperty HorizontalIconAlignmentProperty =
            DependencyProperty.Register("HorizontalIconAlignment", typeof(HorizontalAlignment), typeof(ImageButton), new PropertyMetadata(HorizontalAlignment.Center));


        public VerticalAlignment VerticalIconAlignment
        {
            get { return (VerticalAlignment)GetValue(VerticalIconAlignmentProperty); }
            set { SetValue(VerticalIconAlignmentProperty, value); }
        }
        public static readonly DependencyProperty VerticalIconAlignmentProperty =
            DependencyProperty.Register("VerticalIconAlignment", typeof(VerticalAlignment), typeof(ImageButton), new PropertyMetadata(VerticalAlignment.Center));

        public double IconWidth
        {
            get { return (double)GetValue(IconWidthProperty); }
            set { SetValue(IconWidthProperty, value); }
        }
        public static readonly DependencyProperty IconWidthProperty =
            DependencyProperty.Register("IconWidth", typeof(double), typeof(ImageButton), new PropertyMetadata(double.NaN));

        public double IconHeight
        {
            get { return (double)GetValue(IconHeightProperty); }
            set { SetValue(IconHeightProperty, value); }
        }
        public static readonly DependencyProperty IconHeightProperty =
            DependencyProperty.Register("IconHeight", typeof(double), typeof(ImageButton), new PropertyMetadata(double.NaN));

        public Stretch IconStretch
        {
            get { return (Stretch)GetValue(IconStretchProperty); }
            set { SetValue(IconStretchProperty, value); }
        }

        public static readonly DependencyProperty IconStretchProperty =
            DependencyProperty.Register("IconStretch", typeof(Stretch), typeof(ImageButton), new PropertyMetadata(Stretch.Uniform));

        public TimeSpan LongPressDelay
        {
            get { return (TimeSpan)GetValue(LongPressDelayProperty); }
            set { SetValue(LongPressDelayProperty, value); }
        }

        public static readonly DependencyProperty LongPressDelayProperty =
            DependencyProperty.Register("LongPressDelay", typeof(TimeSpan), typeof(ImageButton), new PropertyMetadata(TimeSpan.FromSeconds(3)));


        public bool LongPressEnabled
        {
            get { return (bool)GetValue(LongPressEnabledProperty); }
            set { SetValue(LongPressEnabledProperty, value); }
        }

        public static readonly DependencyProperty LongPressEnabledProperty =
            DependencyProperty.Register("LongPressEnabled", typeof(bool), typeof(ImageButton), new PropertyMetadata(false));

        public Thickness ContentMargin
        {
            get { return (Thickness)GetValue(ContentMarginProperty); }
            set { SetValue(ContentMarginProperty, value); }
        }

        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(ImageButton), new PropertyMetadata(new Thickness(-1)));

        public Thickness IconMargin
        {
            get { return (Thickness)GetValue(IconMarginProperty); }
            set { SetValue(IconMarginProperty, value); }
        }

        public static readonly DependencyProperty IconMarginProperty =
            DependencyProperty.Register("IconMargin", typeof(Thickness), typeof(ImageButton), new PropertyMetadata(new Thickness(0)));

        public bool Flipped
        {
            get { return (bool)GetValue(FlippedProperty); }
            set { SetValue(FlippedProperty, value); }
        }

        public static readonly DependencyProperty FlippedProperty =
            DependencyProperty.Register("Flipped", typeof(bool), typeof(ImageButton), new PropertyMetadata(false));

        public double ContentWidth
        {
            get { return (double)GetValue(ContentWidthProperty); }
            set { SetValue(ContentWidthProperty, value); }
        }
        public static readonly DependencyProperty ContentWidthProperty =
            DependencyProperty.Register("ContentWidth", typeof(double), typeof(ImageButton), new PropertyMetadata(double.NaN));

        internal bool IsPressedInternal
        {
            get { return (bool)GetValue(IsPressedInternalProperty); }
            set { SetValue(IsPressedInternalProperty, value); }
        }

        internal static readonly DependencyProperty IsPressedInternalProperty =
            DependencyProperty.Register("IsPressedInternal", typeof(bool), typeof(ImageButton), new PropertyMetadata(false));

        public bool IsMouseOverInternal
        {
            get { return (bool)GetValue(IsMouseOverInternalProperty); }
            set { SetValue(IsMouseOverInternalProperty, value); }
        }

        public static readonly DependencyProperty IsMouseOverInternalProperty =
            DependencyProperty.Register("IsMouseOverInternal", typeof(bool), typeof(ImageButton), new PropertyMetadata(false));

        #endregion

        private static int firstMouseDownHash = -1;

        public ImageButton()
        {
            InitializeComponent();
            DataContext = this;

            // BACKGROUND
            if (DisabledBackground == null)
                BindingOperations.SetBinding(this, DisabledBackgroundProperty, new Binding("Background"));

            if (MouseOverBackground == null)
                BindingOperations.SetBinding(this, MouseOverBackgroundProperty, new Binding("Background"));

            if (PressedBackground == null)
                BindingOperations.SetBinding(this, PressedBackgroundProperty, new Binding("Background"));

            if (CheckedBackground == null)
                BindingOperations.SetBinding(this, CheckedBackgroundProperty, new Binding("PressedBackground"));

            // FOREGROUND
            if (MouseOverForeground == null)
                BindingOperations.SetBinding(this, MouseOverForegroundProperty, new Binding("Foreground"));

            if (PressedForeground == null)
                BindingOperations.SetBinding(this, PressedForegroundProperty, new Binding("Foreground"));

            if (CheckedForeground == null)
                BindingOperations.SetBinding(this, CheckedForegroundProperty, new Binding("PressedForeground"));

            // BORDERBRUSH
            if (DisabledBorderBrush == null)
                BindingOperations.SetBinding(this, DisabledBorderBrushProperty, new Binding("BorderBrush"));

            if (MouseOverBorderBrush == null)
                BindingOperations.SetBinding(this, MouseOverBorderBrushProperty, new Binding("BorderBrush"));

            if (PressedBorderBrush == null)
                BindingOperations.SetBinding(this, PressedBorderBrushProperty, new Binding("BorderBrush"));

            if (CheckedBorderBrush == null)
                BindingOperations.SetBinding(this, CheckedBorderBrushProperty, new Binding("PressedBorderBrush"));

            // BORDERTHICKNESS
            if (DisabledBorderThickness == new Thickness(-1))
                BindingOperations.SetBinding(this, DisabledBorderThicknessProperty, new Binding("BorderThickness"));

            if (MouseOverBorderThickness == new Thickness(-1))
                BindingOperations.SetBinding(this, MouseOverBorderThicknessProperty, new Binding("BorderThickness"));

            if (PressedBorderThickness == new Thickness(-1))
                BindingOperations.SetBinding(this, PressedBorderThicknessProperty, new Binding("BorderThickness"));

            if (CheckedBorderThickness == new Thickness(-1))
                BindingOperations.SetBinding(this, CheckedBorderThicknessProperty, new Binding("PressedBorderThickness"));


            // ICON
            if (DisabledIcon == null)
                BindingOperations.SetBinding(this, DisabledIconProperty, new Binding("Icon"));

            if (MouseOverIcon == null)
                BindingOperations.SetBinding(this, MouseOverIconProperty, new Binding("Icon"));

            if (PressedIcon == null)
                BindingOperations.SetBinding(this, PressedIconProperty, new Binding("Icon"));

            if (CheckedIcon == null)
                BindingOperations.SetBinding(this, CheckedIconProperty, new Binding("PressedIcon"));

            // ICONEFFECT
            if (DisabledIconEffect == null)
                BindingOperations.SetBinding(this, DisabledIconEffectProperty, new Binding("IconEffect"));

            if (MouseOverIconEffect == null)
                BindingOperations.SetBinding(this, MouseOverIconEffectProperty, new Binding("IconEffect"));

            if (PressedIconEffect == null)
                BindingOperations.SetBinding(this, PressedIconEffectProperty, new Binding("IconEffect"));

            if (CheckedIconEffect == null)
                BindingOperations.SetBinding(this, CheckedIconEffectProperty, new Binding("PressedIconEffect"));

            // CONTENTEFFECT
            if (DisabledContentEffect == null)
                BindingOperations.SetBinding(this, DisabledContentEffectProperty, new Binding("ContentEffect"));

            if (PressedContentEffect == null)
                BindingOperations.SetBinding(this, PressedContentEffectProperty, new Binding("ContentEffect"));

            if (MouseOverContentEffect == null)
                BindingOperations.SetBinding(this, MouseOverContentEffectProperty, new Binding("ContentEffect"));

            if (CheckedContentEffect == null)
                BindingOperations.SetBinding(this, CheckedContentEffectProperty, new Binding("PressedContentEffect"));

            // EFFECT
            if (DisabledEffect == null)
                BindingOperations.SetBinding(this, DisabledEffectProperty, new Binding("Effect"));

            if (PressedEffect == null)
                BindingOperations.SetBinding(this, PressedEffectProperty, new Binding("Effect"));

            if (MouseOverEffect == null)
                BindingOperations.SetBinding(this, MouseOverEffectProperty, new Binding("Effect"));

            if (CheckedEffect == null)
                BindingOperations.SetBinding(this, CheckedEffectProperty, new Binding("PressedEffect"));

            // OTHER
            if (ContentMargin == new Thickness(-1))
                BindingOperations.SetBinding(this, ContentMarginProperty, new Binding("Content") { Converter = new ImageButtonContentNullToMarginValueConverter() });

            if (IconHeight == double.NaN)
                BindingOperations.SetBinding(this, IconHeightProperty, new Binding("ActualHeight"));

            if (IconWidth == double.NaN)
                BindingOperations.SetBinding(this, IconWidthProperty, new Binding("ActualWidth"));

            base.PreviewMouseLeftButtonDown += OnPreviewPress;
            PreviewMouseLeftButtonUp += OnPreviewRelease;
            PreviewMouseMove += OnPreviewTouchMove;
            MouseLeave += OnLeave;
            MouseEnter += OnEnter;

            base.PreviewTouchDown += OnPreviewPress;
            PreviewTouchUp += OnPreviewRelease;
            PreviewTouchMove += OnPreviewTouchMove;
            TouchLeave += OnLeave;
            TouchEnter += OnEnter;
        }

        private void OnEnter(object sender, RoutedEventArgs e)
        {
            if (e is MouseEventArgs && Mouse.LeftButton != MouseButtonState.Pressed)
                IsMouseOverInternal = true;

            e.Handled = true;
        }

        private void OnLeave(object sender, RoutedEventArgs e)
        {
            if (firstMouseDownHash == GetHashCode())
            {
                ReleaseAllTouchCaptures();
                ReleaseMouseCapture();

                firstMouseDownHash = -1;
                PressHold?.Invoke(this, new PressHoldEventArgs(PressHoldEventArgs.EventType.Release));
                IsPressedInternal = false;
            }

            IsMouseOverInternal = false;

            e.Handled = true;
        }

        private async void OnPreviewPress(object sender, RoutedEventArgs e)
        {
            if (!IsEnabled)
                return;

            if (firstMouseDownHash == -1)
            {
                firstMouseDownHash = GetHashCode();
                IsPressedInternal = true;

                if (e is MouseButtonEventArgs)
                    PreviewMouseLeftButtonDown?.Invoke(sender, e as MouseButtonEventArgs);
                else if (e is TouchEventArgs)
                    PreviewTouchDown?.Invoke(sender, e as TouchEventArgs);

                PressHold?.Invoke(this, new PressHoldEventArgs(PressHoldEventArgs.EventType.Press));
                if (LongPressEnabled)
                {
                    var longPressMSec = LongPressDelay.TotalMilliseconds;
                    var longPressStep = 10;
                    var longPressStepDelay = longPressMSec / longPressStep;

                    while (longPressMSec > 0)
                    {
                        if (firstMouseDownHash == -1)
                            break;
                        await Task.Delay(TimeSpan.FromMilliseconds(longPressStepDelay));
                        longPressMSec -= longPressStepDelay;
                    }

                    if (firstMouseDownHash == GetHashCode())
                    {
                        ReleaseAllTouchCaptures();
                        ReleaseMouseCapture();

                        firstMouseDownHash = -1;
                        LongPress?.Invoke(this, e);
                        IsPressedInternal = false;
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnPreviewRelease(object sender, RoutedEventArgs e)
        {
            var exec = IsPressedInternal && firstMouseDownHash == GetHashCode();

            ReleaseAllTouchCaptures();
            ReleaseMouseCapture();

            if (firstMouseDownHash == GetHashCode())
                firstMouseDownHash = -1;

            PressHold?.Invoke(this, new PressHoldEventArgs(PressHoldEventArgs.EventType.Release));

            if (exec)
                OnClick();

            if (IsMouseOver && e is MouseButtonEventArgs)
                IsMouseOverInternal = true;

            IsPressedInternal = false;
            e.Handled = true;
        }

        private void OnPreviewTouchMove(object sender, RoutedEventArgs e)
        {
            if (firstMouseDownHash == GetHashCode())
            {
                var cursorLocation = new Point();

                if (e is TouchEventArgs)
                    cursorLocation = (e as TouchEventArgs).GetTouchPoint(this).Position;
                else if (e is MouseEventArgs)
                    cursorLocation = (e as MouseEventArgs).GetPosition(this);

                var x = cursorLocation.X;
                var y = cursorLocation.Y;

                if (x < 0 || x > ActualWidth || y < 0 || y > ActualHeight)
                {
                    firstMouseDownHash = -1;
                    PressHold?.Invoke(this, new PressHoldEventArgs(PressHoldEventArgs.EventType.Release));
                    IsPressedInternal = false;
                    IsMouseOverInternal = false;
                }
                else if (e is MouseEventArgs && Mouse.LeftButton != MouseButtonState.Pressed)
                    IsMouseOverInternal = true;
            }

            e.Handled = true;
        }

        protected override void OnClick()
        {
            if (Checkable)
                IsChecked = !IsChecked;
            base.OnClick();
        }
    }

    public class PressHoldEventArgs : RoutedEventArgs
    {
        public enum EventType
        {
            Press,
            Release
        }

        public EventType Event { get; private set; }

        public PressHoldEventArgs(EventType Event)
        {
            this.Event = Event;
        }
    }
}
