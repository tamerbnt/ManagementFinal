using System.Windows.Controls;

namespace Management.Presentation.Controls.Premium
{
    public partial class ModalShellControl : ContentControl
    {
        public static readonly System.Windows.DependencyProperty TitleProperty =
            System.Windows.DependencyProperty.Register("Title", typeof(string), typeof(ModalShellControl), new System.Windows.PropertyMetadata("Modal Title"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly System.Windows.DependencyProperty SubtitleProperty =
            System.Windows.DependencyProperty.Register("Subtitle", typeof(string), typeof(ModalShellControl), new System.Windows.PropertyMetadata(string.Empty));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly System.Windows.DependencyProperty CancelCommandProperty =
            System.Windows.DependencyProperty.Register("CancelCommand", typeof(System.Windows.Input.ICommand), typeof(ModalShellControl), new System.Windows.PropertyMetadata(null));

        public System.Windows.Input.ICommand CancelCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }

        public static readonly System.Windows.DependencyProperty SaveCommandProperty =
            System.Windows.DependencyProperty.Register("SaveCommand", typeof(System.Windows.Input.ICommand), typeof(ModalShellControl), new System.Windows.PropertyMetadata(null));

        public System.Windows.Input.ICommand SaveCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(SaveCommandProperty);
            set => SetValue(SaveCommandProperty, value);
        }

        public static readonly System.Windows.DependencyProperty PrimaryActionTextProperty =
            System.Windows.DependencyProperty.Register("PrimaryActionText", typeof(string), typeof(ModalShellControl), new System.Windows.PropertyMetadata("Continue"));

        public string PrimaryActionText
        {
            get => (string)GetValue(PrimaryActionTextProperty);
            set => SetValue(PrimaryActionTextProperty, value);
        }

        public static readonly System.Windows.DependencyProperty SecondaryActionTextProperty =
            System.Windows.DependencyProperty.Register("SecondaryActionText", typeof(string), typeof(ModalShellControl), new System.Windows.PropertyMetadata("Cancel"));

        public string SecondaryActionText
        {
            get => (string)GetValue(SecondaryActionTextProperty);
            set => SetValue(SecondaryActionTextProperty, value);
        }

        public static readonly System.Windows.DependencyProperty IsBusyProperty =
            System.Windows.DependencyProperty.Register("IsBusy", typeof(bool), typeof(ModalShellControl), new System.Windows.PropertyMetadata(false));

        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        public static readonly System.Windows.DependencyProperty ExtraFooterContentProperty =
            System.Windows.DependencyProperty.Register("ExtraFooterContent", typeof(object), typeof(ModalShellControl), new System.Windows.PropertyMetadata(null));

        public object ExtraFooterContent
        {
            get => GetValue(ExtraFooterContentProperty);
            set => SetValue(ExtraFooterContentProperty, value);
        }

        public ModalShellControl()
        {
            InitializeComponent();
        }
    }
}
