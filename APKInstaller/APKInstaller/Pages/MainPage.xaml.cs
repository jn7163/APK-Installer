﻿using APKInstaller.Helpers;
using APKInstaller.Pages.SettingsPages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APKInstaller.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private bool HasBeenSmail;
        private readonly AppWindow AppWindow = WindowHelper.GetAppWindowForCurrentWindow();

        public MainPage()
        {
            InitializeComponent();
            UIHelper.MainPage = this;
            UIHelper.DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            UIHelper.MainWindow.Backdrop.BackdropTypeChanged += OnBackdropTypeChanged;
            if (UIHelper.HasTitleBar)
            {
                LeftPadding.Width = new GridLength(120);
                UIHelper.MainWindow.ExtendsContentIntoTitleBar = true;
                Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
            else
            {
                AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                ActualThemeChanged += (sender, arg) => ThemeHelper.UpdateSystemCaptionButtonColors();
            }
            UIHelper.MainWindow.SetTitleBar(CustomTitleBar);
            _ = CoreAppFrame.Navigate(typeof(InstallPage));
        }

        private void OnBackdropTypeChanged(BackdropHelper sender, object args)
        {
            CustomTitleBar.Background = (BackdropType)args == BackdropType.DefaultColor
                ? (AboutButtonBorder.Background = CoreAppFrame.Background = (SolidColorBrush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"])
                : (AboutButtonBorder.Background = CoreAppFrame.Background = (SolidColorBrush)Application.Current.Resources["ControlFillColorTransparentBrush"]);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as FrameworkElement).Name)
            {
                case "AboutButton":
                    _ = CoreAppFrame.Navigate(typeof(SettingsPage));
                    break;
                default:
                    break;
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (UIHelper.HasTitleBar)
                {
                    if (XamlRoot.Size.Width <= 268)
                    {
                        if (!HasBeenSmail)
                        {
                            HasBeenSmail = true;
                            UIHelper.MainWindow.SetTitleBar(null);
                        }
                    }
                    else if (HasBeenSmail)
                    {
                        HasBeenSmail = false;
                        UIHelper.MainWindow.SetTitleBar(CustomTitleBar);
                    }
                }
                else
                {
                    RectInt32 Rect = new((ActualWidth - CustomTitleBar.ActualWidth).GetActualPixel(), 0, CustomTitleBar.ActualWidth.GetActualPixel(), CustomTitleBar.ActualHeight.GetActualPixel());
                    AppWindow.TitleBar.SetDragRectangles(new RectInt32[] { Rect });
                }
            }
            catch { }
        }
    }
}
