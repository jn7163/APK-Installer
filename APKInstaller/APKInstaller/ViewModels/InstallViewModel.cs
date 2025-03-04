﻿using AAPTForNet;
using AAPTForNet.Models;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using APKInstaller.Controls.Dialogs;
using APKInstaller.Helpers;
using APKInstaller.Models;
using APKInstaller.Pages;
using APKInstaller.Pages.SettingsPages;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Connectivity;
using Downloader;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;

namespace APKInstaller.ViewModels
{
    public class InstallViewModel : INotifyPropertyChanged, IDisposable
    {
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto, PreserveSig = true, SetLastError = false)]
        private static extern IntPtr GetActiveWindow();

        private InstallPage _page;
        private DeviceData _device;
        private readonly ProtocolForResultsOperation _operation;
        private static readonly string APKTemp = Path.Combine(CachesHelper.TempPath, "NetAPKTemp.apk");
        private static readonly string ADBTemp = Path.Combine(CachesHelper.TempPath, "platform-tools.zip");

#if !DEBUG
        private Uri _url;
        private string _path = string.Empty;
#else
        private Uri _url = new("apkinstaller:?source=https://dl.coolapk.com/down?pn=com.coolapk.market&id=NDU5OQ&h=46bb9d98&from=from-web");
        private string _path = @"C:\Users\qq251\Downloads\Programs\weixin8020android2100_arm64_4.apk";
#endif
        private bool NetAPKExist => _path != APKTemp || File.Exists(_path);

        private bool _disposedValue;
        private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse("InstallPage");

        public static InstallViewModel Caches;
        public string InstallFormat => _loader.GetString("InstallFormat");
        public string VersionFormat => _loader.GetString("VersionFormat");
        public string PackageNameFormat => _loader.GetString("PackageNameFormat");

        private static bool IsOnlyWSA => SettingsHelper.Get<bool>(SettingsHelper.IsOnlyWSA);
        private static bool IsCloseAPP => SettingsHelper.Get<bool>(SettingsHelper.IsCloseAPP);
        private static bool ShowDialogs => SettingsHelper.Get<bool>(SettingsHelper.ShowDialogs);
        private static bool AutoGetNetAPK => SettingsHelper.Get<bool>(SettingsHelper.AutoGetNetAPK);

        private ApkInfo _apkInfo = null;
        public ApkInfo ApkInfo
        {
            get => _apkInfo;
            set
            {
                _apkInfo = value;
                RaisePropertyChangedEvent();
            }
        }

        public string ADBPath
        {
            get => SettingsHelper.Get<string>(SettingsHelper.ADBPath);
            set
            {
                SettingsHelper.Set(SettingsHelper.ADBPath, value);
                RaisePropertyChangedEvent();
            }
        }

        public static bool IsOpenApp
        {
            get => SettingsHelper.Get<bool>(SettingsHelper.IsOpenApp);
            set
            {
                SettingsHelper.Set(SettingsHelper.IsOpenApp, value);
            }
        }

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            set
            {
                _isInstalling = value;
                if (value)
                {
                    ProgressHelper.SetState(ProgressState.Indeterminate, true);
                }
                else
                {
                    ProgressHelper.SetState(ProgressState.None, true);
                }
                RaisePropertyChangedEvent();
            }
        }

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            set
            {
                _isInitialized = value;
                if (value)
                {
                    ProgressHelper.SetState(ProgressState.None, true);
                }
                else
                {
                    ProgressHelper.SetState(ProgressState.Indeterminate, true);
                }
                RaisePropertyChangedEvent();
            }
        }

        private string _appName;
        public string AppName
        {
            get => _appName;
            set
            {
                _appName = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _textOutput;
        public string TextOutput
        {
            get => _textOutput;
            set
            {
                _textOutput = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _infoMessage;
        public string InfoMessage
        {
            get => _infoMessage;
            set
            {
                _infoMessage = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _progressText;
        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _actionButtonEnable;
        public bool ActionButtonEnable
        {
            get => _actionButtonEnable;
            set
            {
                _actionButtonEnable = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _secondaryActionButtonEnable;
        public bool SecondaryActionButtonEnable
        {
            get => _secondaryActionButtonEnable;
            set
            {
                _secondaryActionButtonEnable = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _fileSelectButtonEnable;
        public bool FileSelectButtonEnable
        {
            get => _fileSelectButtonEnable;
            set
            {
                _fileSelectButtonEnable = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _downloadButtonEnable;
        public bool DownloadButtonEnable
        {
            get => _downloadButtonEnable;
            set
            {
                _downloadButtonEnable = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _deviceSelectButtonEnable;
        public bool DeviceSelectButtonEnable
        {
            get => _deviceSelectButtonEnable;
            set
            {
                _deviceSelectButtonEnable = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _cancelOperationButtonEnable;
        public bool CancelOperationButtonEnable
        {
            get => _cancelOperationButtonEnable;
            set
            {
                _cancelOperationButtonEnable = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _waitProgressText;
        public string WaitProgressText
        {
            get => _waitProgressText;
            set
            {
                _waitProgressText = value;
                RaisePropertyChangedEvent();
            }
        }

        private double _waitProgressValue = 0;
        public double WaitProgressValue
        {
            get => _waitProgressValue;
            set
            {
                _waitProgressValue = value;
                RaisePropertyChangedEvent();
            }
        }

        private double _appxInstallBarValue = 0;
        public double AppxInstallBarValue
        {
            get => _appxInstallBarValue;
            set
            {
                _appxInstallBarValue = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _waitProgressIndeterminate = true;
        public bool WaitProgressIndeterminate
        {
            get => _waitProgressIndeterminate;
            set
            {
                _waitProgressIndeterminate = value;
                RaisePropertyChangedEvent();
            }
        }

        private bool _appxInstallBarIndeterminate = true;
        public bool AppxInstallBarIndeterminate
        {
            get => _appxInstallBarIndeterminate;
            set
            {
                _appxInstallBarIndeterminate = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _actionButtonText;
        public string ActionButtonText
        {
            get => _actionButtonText;
            set
            {
                _actionButtonText = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _secondaryActionButtonText;
        public string SecondaryActionButtonText
        {
            get => _secondaryActionButtonText;
            set
            {
                _secondaryActionButtonText = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _fileSelectButtonText;
        public string FileSelectButtonText
        {
            get => _fileSelectButtonText;
            set
            {
                _fileSelectButtonText = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _downloadButtonText;
        public string DownloadButtonText
        {
            get => _downloadButtonText;
            set
            {
                _downloadButtonText = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _deviceSelectButtonText;
        public string DeviceSelectButtonText
        {
            get => _deviceSelectButtonText;
            set
            {
                _deviceSelectButtonText = value;
                RaisePropertyChangedEvent();
            }
        }

        private string _cancelOperationButtonText;
        public string CancelOperationButtonText
        {
            get => _cancelOperationButtonText;
            set
            {
                _cancelOperationButtonText = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _textOutputVisibility = Visibility.Collapsed;
        public Visibility TextOutputVisibility
        {
            get => _textOutputVisibility;
            set
            {
                _textOutputVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _installOutputVisibility = Visibility.Collapsed;
        public Visibility InstallOutputVisibility
        {
            get => _installOutputVisibility;
            set
            {
                _installOutputVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _actionVisibility = Visibility.Collapsed;
        public Visibility ActionVisibility
        {
            get => _actionVisibility;
            set
            {
                _actionVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _secondaryActionVisibility = Visibility.Collapsed;
        public Visibility SecondaryActionVisibility
        {
            get => _secondaryActionVisibility;
            set
            {
                _secondaryActionVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _fileSelectVisibility = Visibility.Collapsed;
        public Visibility FileSelectVisibility
        {
            get => _fileSelectVisibility;
            set
            {
                _fileSelectVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _downloadVisibility = Visibility.Collapsed;
        public Visibility DownloadVisibility
        {
            get => _downloadVisibility;
            set
            {
                _downloadVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _deviceSelectVisibility = Visibility.Collapsed;
        public Visibility DeviceSelectVisibility
        {
            get => _deviceSelectVisibility;
            set
            {
                _deviceSelectVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _cancelOperationVisibility = Visibility.Collapsed;
        public Visibility CancelOperationVisibility
        {
            get => _cancelOperationVisibility;
            set
            {
                _cancelOperationVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _messagesToUserVisibility = Visibility.Collapsed;
        public Visibility MessagesToUserVisibility
        {
            get => _messagesToUserVisibility;
            set
            {
                _messagesToUserVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _launchWhenReadyVisibility = Visibility.Collapsed;
        public Visibility LaunchWhenReadyVisibility
        {
            get => _launchWhenReadyVisibility;
            set
            {
                _launchWhenReadyVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _appVersionVisibility;
        public Visibility AppVersionVisibility
        {
            get => _appVersionVisibility;
            set
            {
                _appVersionVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _appPublisherVisibility;
        public Visibility AppPublisherVisibility
        {
            get => _appPublisherVisibility;
            set
            {
                _appPublisherVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        private Visibility _appCapabilitiesVisibility;
        public Visibility AppCapabilitiesVisibility
        {
            get => _appCapabilitiesVisibility;
            set
            {
                _appCapabilitiesVisibility = value;
                RaisePropertyChangedEvent();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChangedEvent([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            if (name != null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        }

        // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        public InstallViewModel(Uri Url, InstallPage Page, ProtocolForResultsOperation Operation = null)
        {
            _url = Url;
            _page = Page;
            Caches = this;
            _path = APKTemp;
            _operation = Operation;
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: false);
        }

        // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        public InstallViewModel(string Path, InstallPage Page, ProtocolForResultsOperation Operation = null)
        {
            _page = Page;
            Caches = this;
            _operation = Operation;
            _path = string.IsNullOrEmpty(Path) ? _path : Path;
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: false);
        }

        public static void SetPage(InstallPage Page) => Caches._page = Page;

        public async Task Refresh(bool force = true)
        {
            IsInitialized = false;
            try
            {
                if (force)
                {
                    await InitilizeADB();
                    await InitilizeUI();
                }
                else
                {
                    await ReinitilizeUI();
                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                PackageError(ex.Message);
                IsInstalling = false;
            }
        }

        private async Task CheckADB(bool force = false)
        {
        checkadb:
            if (force || !File.Exists(ADBPath))
            {
                StackPanel StackPanel = new();
                StackPanel.Children.Add(
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Text = _loader.GetString("AboutADB")
                    });
                StackPanel.Children.Add(
                    new HyperlinkButton
                    {
                        Content = _loader.GetString("ClickToRead"),
                        NavigateUri = new Uri("https://developer.android.google.cn/studio/releases/platform-tools")
                    });
                ContentDialog dialog = new()
                {
                    XamlRoot = _page.XamlRoot,
                    Title = _loader.GetString("ADBMissing"),
                    PrimaryButtonText = _loader.GetString("Download"),
                    SecondaryButtonText = _loader.GetString("Select"),
                    CloseButtonText = _loader.GetString("Cancel"),
                    Content = new ScrollViewer()
                    {
                        Content = StackPanel
                    },
                    DefaultButton = ContentDialogButton.Primary
                };
                ProgressHelper.SetState(ProgressState.None, true);
                ContentDialogResult result = await dialog.ShowAsync();
                ProgressHelper.SetState(ProgressState.Indeterminate, true);
                if (result == ContentDialogResult.Primary)
                {
                downloadadb:
                    if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                    {
                        try
                        {
                            await DownloadADB();
                        }
                        catch (Exception ex)
                        {
                            ContentDialog dialogs = new()
                            {
                                XamlRoot = _page.XamlRoot,
                                Title = _loader.GetString("DownloadFailed"),
                                PrimaryButtonText = _loader.GetString("Retry"),
                                CloseButtonText = _loader.GetString("Cancel"),
                                Content = new TextBlock { Text = ex.Message },
                                DefaultButton = ContentDialogButton.Primary
                            };
                            ProgressHelper.SetState(ProgressState.None, true);
                            ContentDialogResult results = await dialogs.ShowAsync();
                            ProgressHelper.SetState(ProgressState.Indeterminate, true);
                            if (results == ContentDialogResult.Primary)
                            {
                                goto downloadadb;
                            }
                            else
                            {
                                SendResults(new Exception($"ADB {_loader.GetString("DownloadFailed")}"));
                                Application.Current.Exit();
                                return;
                            }
                        }
                    }
                    else
                    {
                        ContentDialog dialogs = new()
                        {
                            XamlRoot = _page.XamlRoot,
                            Title = _loader.GetString("NoInternet"),
                            PrimaryButtonText = _loader.GetString("Retry"),
                            CloseButtonText = _loader.GetString("Cancel"),
                            Content = new TextBlock { Text = _loader.GetString("NoInternetInfo") },
                            DefaultButton = ContentDialogButton.Primary
                        };
                        ProgressHelper.SetState(ProgressState.None, true);
                        ContentDialogResult results = await dialogs.ShowAsync();
                        ProgressHelper.SetState(ProgressState.Indeterminate, true);
                        if (results == ContentDialogResult.Primary)
                        {
                            goto checkadb;
                        }
                        else
                        {
                            SendResults(new Exception($"{_loader.GetString("NoInternet")}, ADB {_loader.GetString("DownloadFailed")}"));
                            Application.Current.Exit();
                            return;
                        }
                    }
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    FileOpenPicker FileOpen = new();
                    FileOpen.FileTypeFilter.Add(".exe");
                    FileOpen.SuggestedStartLocation = PickerLocationId.ComputerFolder;

                    // When running on win32, FileSavePicker needs to know the top-level hwnd via IInitializeWithWindow::Initialize.
                    if (Window.Current == null)
                    {
                        IInitializeWithWindow initializeWithWindowWrapper = FileOpen.As<IInitializeWithWindow>();
                        IntPtr hwnd = GetActiveWindow();
                        initializeWithWindowWrapper.Initialize(hwnd);
                    }

                    StorageFile file = await FileOpen.PickSingleFileAsync();
                    if (file != null)
                    {
                        ADBPath = file.Path;
                    }
                }
                else
                {
                    SendResults(new Exception(_loader.GetString("ADBMissing")));
                    Application.Current.Exit();
                    return;
                }
            }
        }

        private async Task DownloadADB()
        {
            if (!Directory.Exists(ADBTemp[..ADBTemp.LastIndexOf(@"\")]))
            {
                _ = Directory.CreateDirectory(ADBTemp[..ADBTemp.LastIndexOf(@"\")]);
            }
            else if (Directory.Exists(ADBTemp))
            {
                Directory.Delete(ADBTemp, true);
            }
            using (DownloadService downloader = new(DownloadHelper.Configuration))
            {
                bool IsCompleted = false;
                Exception exception = null;
                long ReceivedBytesSize = 0;
                long TotalBytesToReceive = 0;
                double ProgressPercentage = 0;
                double BytesPerSecondSpeed = 0;
                void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
                {
                    exception = e.Error;
                    IsCompleted = true;
                }
                void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
                {
                    ReceivedBytesSize = e.ReceivedBytesSize;
                    ProgressPercentage = e.ProgressPercentage;
                    TotalBytesToReceive = e.TotalBytesToReceive;
                    BytesPerSecondSpeed = e.BytesPerSecondSpeed;
                }
                downloader.DownloadFileCompleted += OnDownloadFileCompleted;
                downloader.DownloadProgressChanged += OnDownloadProgressChanged;
            downloadadb:
                WaitProgressText = _loader.GetString("WaitDownload");
                _ = downloader.DownloadFileTaskAsync("https://dl.google.com/android/repository/platform-tools-latest-windows.zip", ADBTemp);
                while (TotalBytesToReceive <= 0)
                {
                    await Task.Delay(1);
                    if (IsCompleted)
                    {
                        goto downloadfinish;
                    }
                }
                WaitProgressIndeterminate = false;
                ProgressHelper.SetState(ProgressState.Normal, true);
                while (!IsCompleted)
                {
                    ProgressHelper.SetValue(Convert.ToInt32(ReceivedBytesSize), Convert.ToInt32(TotalBytesToReceive), true);
                    WaitProgressText = $"{((double)BytesPerSecondSpeed).GetSizeString()}/s";
                    WaitProgressValue = ProgressPercentage;
                    await Task.Delay(1);
                }
                ProgressHelper.SetState(ProgressState.Indeterminate, true);
                WaitProgressIndeterminate = true;
                WaitProgressValue = 0;
            downloadfinish:
                if (exception != null)
                {
                    ProgressHelper.SetState(ProgressState.Error, true);
                    ContentDialog dialog = new()
                    {
                        XamlRoot = _page.XamlRoot,
                        Content = exception.Message,
                        Title = _loader.GetString("DownloadFailed"),
                        PrimaryButtonText = _loader.GetString("Retry"),
                        CloseButtonText = _loader.GetString("Cancel"),
                        DefaultButton = ContentDialogButton.Primary
                    };
                    ContentDialogResult result = await dialog.ShowAsync();
                    ProgressHelper.SetState(ProgressState.Indeterminate, true);
                    if (result == ContentDialogResult.Primary)
                    {
                        goto downloadadb;
                    }
                    else
                    {
                        SendResults(new Exception($"ADB {_loader.GetString("DownloadFailed")}"));
                        Application.Current.Exit();
                        return;
                    }
                }
                downloader.DownloadProgressChanged -= OnDownloadProgressChanged;
                downloader.DownloadFileCompleted -= OnDownloadFileCompleted;
            }
            WaitProgressText = _loader.GetString("UnzipADB");
            await Task.Delay(1);
            using (IArchive archive = ArchiveFactory.Open(ADBTemp))
            {
                ProgressHelper.SetState(ProgressState.Normal, true);
                WaitProgressIndeterminate = false;
                int Progressed = 0;
                bool IsCompleted = false;
                double ProgressPercentage = 0;
                int TotalCount = archive.Entries.Count();
                _ = Task.Run(() =>
                {
                    foreach (IArchiveEntry entry in archive.Entries)
                    {
                        Progressed = archive.Entries.ToList().IndexOf(entry) + 1;
                        ProgressPercentage = archive.Entries.GetProgressValue(entry);
                        if (!entry.IsDirectory)
                        {
                            entry.WriteToDirectory(ApplicationData.Current.LocalFolder.Path, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                        }
                    }
                    IsCompleted = true;
                });
                while (!IsCompleted)
                {
                    WaitProgressValue = ProgressPercentage;
                    ProgressHelper.SetValue(Progressed, TotalCount, true);
                    WaitProgressText = string.Format(_loader.GetString("UnzippingFormat"), Progressed, TotalCount);
                    await Task.Delay(1);
                }
                WaitProgressValue = 0;
                WaitProgressIndeterminate = true;
                WaitProgressText = _loader.GetString("UnzipComplete");
                ProgressHelper.SetState(ProgressState.Indeterminate, true);
            }
            ADBPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, @"platform-tools\adb.exe");
        }

        private async Task InitilizeADB()
        {
            WaitProgressText = _loader.GetString("Loading");
            if (!string.IsNullOrEmpty(_path) || _url != null)
            {
                IAdbServer ADBServer = AdbServer.Instance;
                if (!ADBServer.GetStatus().IsRunning)
                {
                    WaitProgressText = _loader.GetString("CheckingADB");
                    await CheckADB();
                    Process[] processes = Process.GetProcessesByName("adb");
                startadb:
                    WaitProgressText = _loader.GetString("StartingADB");
                    try
                    {
                        await Task.Run(() => ADBServer.StartServer((processes != null && processes.Any()) ? processes.First().MainModule?.FileName : ADBPath, restartServerIfNewer: false));
                    }
                    catch
                    {
                        if (processes != null && processes.Any())
                        {
                            foreach (Process process in processes)
                            {
                                process.Kill();
                            }
                            processes = null;
                        }
                        await CheckADB(true);
                        goto startadb;
                    }
                }
                WaitProgressText = _loader.GetString("Loading");
                if (!CheckDevice())
                {
                    if (IsOnlyWSA)
                    {
                        if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                        {
                            await AddressHelper.ConnectHyperV();
                            if (!CheckDevice())
                            {
                                new AdvancedAdbClient().Connect(new DnsEndPoint("127.0.0.1", 58526));
                            }
                        }
                        else
                        {
                            new AdvancedAdbClient().Connect(new DnsEndPoint("127.0.0.1", 58526));
                        }
                    }
                }
                ADBHelper.Monitor.DeviceChanged += OnDeviceChanged;
            }
        }

        private async Task InitilizeUI()
        {
            if (!string.IsNullOrEmpty(_path) || _url != null)
            {
                WaitProgressText = _loader.GetString("Loading");
                if (NetAPKExist)
                {
                    try
                    {
                        ApkInfo = await Task.Run(() => { return AAPTool.Decompile(_path); });
                    }
                    catch (Exception ex)
                    {
                        PackageError(ex.Message);
                        IsInitialized = true;
                        return;
                    }
                }
                else
                {
                    ApkInfo ??= new ApkInfo();
                }
                if (string.IsNullOrEmpty(ApkInfo?.PackageName) && NetAPKExist)
                {
                    PackageError(_loader.GetString("InvalidPackage"));
                }
                else
                {
                checkdevice:
                    WaitProgressText = _loader.GetString("Checking");
                    if (CheckDevice() && _device != null)
                    {
                        if (NetAPKExist)
                        {
                            CheckAPK();
                        }
                        else
                        {
                            ResetUI();
                            CheckOnlinePackage();
                        }
                    }
                    else
                    {
                        ResetUI();
                        if (NetAPKExist)
                        {
                            ActionButtonEnable = false;
                            ActionButtonText = _loader.GetString("Install");
                            InfoMessage = _loader.GetString("WaitingDevice");
                            DeviceSelectButtonText = _loader.GetString("Devices");
                            AppName = string.Format(_loader.GetString("WaitingForInstallFormat"), ApkInfo?.AppName);
                            ActionVisibility = DeviceSelectVisibility = MessagesToUserVisibility = Visibility.Visible;
                        }
                        else
                        {
                            CheckOnlinePackage();
                        }
                        if (ShowDialogs && await ShowDeviceDialog())
                        {
                            goto checkdevice;
                        }
                    }
                }
                WaitProgressText = _loader.GetString("Finished");
            }
            else
            {
                ResetUI();
                ApkInfo ??= new ApkInfo();
                AppName = _loader.GetString("NoPackageWranning");
                FileSelectButtonText = _loader.GetString("Select");
                CancelOperationButtonText = _loader.GetString("Close");
                FileSelectVisibility = CancelOperationVisibility = Visibility.Visible;
                AppVersionVisibility = AppPublisherVisibility = AppCapabilitiesVisibility = Visibility.Collapsed;
            }
            IsInitialized = true;
        }

        private async Task<bool> ShowDeviceDialog()
        {
            if (IsOnlyWSA)
            {
                WaitProgressText = _loader.GetString("FindingWSA");
                if ((await PackageHelper.FindPackagesByName("MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe")).isfound)
                {
                    WaitProgressText = _loader.GetString("FoundWSA");
                    ContentDialog dialog = new MarkdownDialog
                    {
                        XamlRoot = _page.XamlRoot,
                        Title = _loader.GetString("HowToConnect"),
                        DefaultButton = ContentDialogButton.Close,
                        CloseButtonText = _loader.GetString("IKnow"),
                        PrimaryButtonText = _loader.GetString("StartWSA"),
                        ContentInfo = new GitInfo("Paving-Base", "APK-Installer", "screenshots", "Documents/Tutorials/How%20To%20Connect%20WSA", "How%20To%20Connect%20WSA.md")
                    };
                    ProgressHelper.SetState(ProgressState.None, true);
                    ContentDialogResult result = await dialog.ShowAsync();
                    ProgressHelper.SetState(ProgressState.Indeterminate, true);
                    if (result == ContentDialogResult.Primary)
                    {
                    startwsa:
                        CancellationTokenSource TokenSource = new(TimeSpan.FromMinutes(5));
                        try
                        {
                            WaitProgressText = _loader.GetString("LaunchingWSA");
                            _ = await Launcher.LaunchUriAsync(new Uri("wsa://"));
                            bool IsWSARunning = false;
                            while (!IsWSARunning)
                            {
                                TokenSource.Token.ThrowIfCancellationRequested();
                                await Task.Run(() =>
                                {
                                    Process[] ps = Process.GetProcessesByName("vmmemWSA");
                                    IsWSARunning = ps != null && ps.Length > 0;
                                });
                            }
                            WaitProgressText = _loader.GetString("WaitingWSAStart");
                            while (!CheckDevice())
                            {
                                TokenSource.Token.ThrowIfCancellationRequested();
                                if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                                {
                                    await AddressHelper.ConnectHyperV();
                                    if (!CheckDevice())
                                    {
                                        new AdvancedAdbClient().Connect(new DnsEndPoint("127.0.0.1", 58526));
                                    }
                                }
                                else
                                {
                                    new AdvancedAdbClient().Connect(new DnsEndPoint("127.0.0.1", 58526));
                                }
                                await Task.Delay(100);
                            }
                            WaitProgressText = _loader.GetString("WSARunning");
                            return true;
                        }
                        catch
                        {
                            ContentDialog dialogs = new()
                            {
                                XamlRoot = _page.XamlRoot,
                                Title = _loader.GetString("CannotConnectWSA"),
                                DefaultButton = ContentDialogButton.Close,
                                CloseButtonText = _loader.GetString("IKnow"),
                                PrimaryButtonText = _loader.GetString("Retry"),
                                Content = _loader.GetString("CannotConnectWSAInfo"),
                            };
                            ProgressHelper.SetState(ProgressState.None, true);
                            ContentDialogResult results = await dialogs.ShowAsync();
                            ProgressHelper.SetState(ProgressState.Indeterminate, true);
                            if (results == ContentDialogResult.Primary)
                            {
                                goto startwsa;
                            }
                        }
                    }
                }
                else
                {
                    ContentDialog dialog = new()
                    {
                        XamlRoot = _page.XamlRoot,
                        Title = _loader.GetString("NoDevice"),
                        DefaultButton = ContentDialogButton.Primary,
                        CloseButtonText = _loader.GetString("IKnow"),
                        PrimaryButtonText = _loader.GetString("InstallWSA"),
                        SecondaryButtonText = _loader.GetString("GoToSetting"),
                        Content = _loader.GetString("NoDeviceInfo"),
                    };
                    ProgressHelper.SetState(ProgressState.None, true);
                    ContentDialogResult result = await dialog.ShowAsync();
                    ProgressHelper.SetState(ProgressState.Indeterminate, true);
                    if (result == ContentDialogResult.Primary)
                    {
                        _ = Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9P3395VX91NR&mode=mini"));
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        UIHelper.Navigate(typeof(SettingsPage), null);
                    }
                }
            }
            else
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = _page.XamlRoot,
                    Title = _loader.GetString("NoDevice"),
                    DefaultButton = ContentDialogButton.Close,
                    CloseButtonText = _loader.GetString("IKnow"),
                    PrimaryButtonText = _loader.GetString("GoToSetting"),
                    Content = _loader.GetString("NoDeviceInfo10"),
                };
                ProgressHelper.SetState(ProgressState.None, true);
                ContentDialogResult result = await dialog.ShowAsync();
                ProgressHelper.SetState(ProgressState.Indeterminate, true);
                if (result == ContentDialogResult.Primary)
                {
                    UIHelper.Navigate(typeof(SettingsPage), null);
                }
            }
            return false;
        }

        private async Task ReinitilizeUI()
        {
            WaitProgressText = _loader.GetString("Loading");
            if ((!string.IsNullOrEmpty(_path) || _url != null) && NetAPKExist)
            {
            checkdevice:
                if (CheckDevice() && _device != null)
                {
                    CheckAPK();
                }
                else if (ShowDialogs)
                {
                    if (await ShowDeviceDialog())
                    {
                        goto checkdevice;
                    }
                }
            }
        }

        private void CheckAPK()
        {
            ResetUI();
            AdvancedAdbClient client = new();
            PackageManager manager = new(client, _device);
            VersionInfo info = null;
            if (ApkInfo != null && !string.IsNullOrEmpty(ApkInfo?.PackageName))
            {
                info = manager.GetVersionInfo(ApkInfo?.PackageName);
            }
            if (info == null)
            {
                ActionButtonText = _loader.GetString("Install");
                AppName = string.Format(_loader.GetString("InstallFormat"), ApkInfo?.AppName);
                ActionVisibility = LaunchWhenReadyVisibility = Visibility.Visible;
            }
            else if (info.VersionCode < int.Parse(ApkInfo?.VersionCode))
            {
                ActionButtonText = _loader.GetString("Update");
                AppName = string.Format(_loader.GetString("UpdateFormat"), ApkInfo?.AppName);
                ActionVisibility = LaunchWhenReadyVisibility = Visibility.Visible;
            }
            else
            {
                ActionButtonText = _loader.GetString("Reinstall");
                SecondaryActionButtonText = _loader.GetString("Launch");
                AppName = string.Format(_loader.GetString("ReinstallFormat"), ApkInfo?.AppName);
                TextOutput = string.Format(_loader.GetString("ReinstallOutput"), ApkInfo?.AppName);
                ActionVisibility = SecondaryActionVisibility = TextOutputVisibility = Visibility.Visible;
            }
        }

        private void CheckOnlinePackage()
        {
            Regex[] UriRegex = new Regex[] { new Regex(@":\?source=(.*)"), new Regex(@"://(.*)") };
            string Uri = UriRegex[0].IsMatch(_url.ToString()) ? UriRegex[0].Match(_url.ToString()).Groups[1].Value : UriRegex[1].Match(_url.ToString()).Groups[1].Value;
            Uri Url = Uri.ValidateAndGetUri();
            if (Url != null)
            {
                _url = Url;
                AppName = _loader.GetString("OnlinePackage");
                DownloadButtonText = _loader.GetString("Download");
                CancelOperationButtonText = _loader.GetString("Close");
                DownloadVisibility = CancelOperationVisibility = Visibility.Visible;
                AppVersionVisibility = AppPublisherVisibility = AppCapabilitiesVisibility = Visibility.Collapsed;
                if (AutoGetNetAPK)
                {
                    LoadNetAPK();
                }
            }
            else
            {
                PackageError(_loader.GetString("InvalidURL"));
            }
        }

        public async void LoadNetAPK()
        {
            IsInstalling = true;
            DownloadVisibility = Visibility.Collapsed;
            try
            {
                await DownloadAPK();
            }
            catch (Exception ex)
            {
                PackageError(ex.Message);
                IsInstalling = false;
                return;
            }

            try
            {
                ApkInfo = await Task.Run(() => { return AAPTool.Decompile(_path); });
            }
            catch (Exception ex)
            {
                PackageError(ex.Message);
                IsInstalling = false;
                return;
            }

            if (string.IsNullOrEmpty(ApkInfo?.PackageName))
            {
                PackageError(_loader.GetString("InvalidPackage"));
            }
            else
            {
                if (CheckDevice() && _device != null)
                {
                    CheckAPK();
                }
                else
                {
                    ResetUI();
                    ActionButtonEnable = false;
                    ActionButtonText = _loader.GetString("Install");
                    InfoMessage = _loader.GetString("WaitingDevice");
                    DeviceSelectButtonText = _loader.GetString("Devices");
                    AppName = string.Format(_loader.GetString("WaitingForInstallFormat"), ApkInfo?.AppName);
                    ActionVisibility = DeviceSelectVisibility = MessagesToUserVisibility = Visibility.Visible;
                }
            }
            IsInstalling = false;
        }

        private async Task DownloadAPK()
        {
            if (_url != null)
            {
                if (!Directory.Exists(APKTemp[..APKTemp.LastIndexOf(@"\")]))
                {
                    _ = Directory.CreateDirectory(APKTemp[..APKTemp.LastIndexOf(@"\")]);
                }
                else if (Directory.Exists(APKTemp))
                {
                    Directory.Delete(APKTemp, true);
                }
                using (DownloadService downloader = new(DownloadHelper.Configuration))
                {
                    bool IsCompleted = false;
                    Exception exception = null;
                    long ReceivedBytesSize = 0;
                    long TotalBytesToReceive = 0;
                    double ProgressPercentage = 0;
                    double BytesPerSecondSpeed = 0;
                    void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
                    {
                        exception = e.Error;
                        IsCompleted = true;
                    }
                    void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
                    {
                        ReceivedBytesSize = e.ReceivedBytesSize;
                        ProgressPercentage = e.ProgressPercentage;
                        TotalBytesToReceive = e.TotalBytesToReceive;
                        BytesPerSecondSpeed = e.BytesPerSecondSpeed;
                    }
                    downloader.DownloadFileCompleted += OnDownloadFileCompleted;
                    downloader.DownloadProgressChanged += OnDownloadProgressChanged;
                downloadapk:
                    ProgressText = _loader.GetString("WaitDownload");
                    _ = downloader.DownloadFileTaskAsync(_url.ToString(), APKTemp);
                    while (TotalBytesToReceive <= 0)
                    {
                        await Task.Delay(1);
                        if (IsCompleted)
                        {
                            goto downloadfinish;
                        }
                    }
                    AppxInstallBarIndeterminate = false;
                    ProgressHelper.SetState(ProgressState.Normal, true);
                    while (!IsCompleted)
                    {
                        ProgressHelper.SetValue(Convert.ToInt32(ReceivedBytesSize), Convert.ToInt32(TotalBytesToReceive), true);
                        ProgressText = $"{ProgressPercentage:N2}% {((double)BytesPerSecondSpeed).GetSizeString()}/s";
                        AppxInstallBarValue = ProgressPercentage;
                        await Task.Delay(1);
                    }
                    ProgressHelper.SetState(ProgressState.Indeterminate, true);
                    ProgressText = _loader.GetString("Loading");
                    AppxInstallBarIndeterminate = true;
                    AppxInstallBarValue = 0;
                downloadfinish:
                    if (exception != null)
                    {
                        ProgressHelper.SetState(ProgressState.Error, true);
                        ContentDialog dialog = new()
                        {
                            XamlRoot = _page.XamlRoot,
                            Content = exception.Message,
                            Title = _loader.GetString("DownloadFailed"),
                            PrimaryButtonText = _loader.GetString("Retry"),
                            CloseButtonText = _loader.GetString("Cancel"),
                            DefaultButton = ContentDialogButton.Primary
                        };
                        ContentDialogResult result = await dialog.ShowAsync();
                        ProgressHelper.SetState(ProgressState.Indeterminate, true);
                        if (result == ContentDialogResult.Primary)
                        {
                            goto downloadapk;
                        }
                        else
                        {
                            SendResults(new Exception($"APK {_loader.GetString("DownloadFailed")}"));
                            Application.Current.Exit();
                            return;
                        }
                    }
                    downloader.DownloadProgressChanged -= OnDownloadProgressChanged;
                    downloader.DownloadFileCompleted -= OnDownloadFileCompleted;
                }
            }
        }

        private void ResetUI()
        {
            ActionVisibility =
            SecondaryActionVisibility =
            FileSelectVisibility =
            DownloadVisibility =
            DeviceSelectVisibility =
            CancelOperationVisibility =
            TextOutputVisibility =
            InstallOutputVisibility =
            LaunchWhenReadyVisibility =
            MessagesToUserVisibility = Visibility.Collapsed;
            AppVersionVisibility =
            AppPublisherVisibility =
            AppCapabilitiesVisibility = Visibility.Visible;
            AppxInstallBarIndeterminate =
            ActionButtonEnable =
            SecondaryActionButtonEnable =
            FileSelectButtonEnable =
            DownloadButtonEnable =
            DeviceSelectButtonEnable =
            CancelOperationButtonEnable = true;
        }

        private void PackageError(string message)
        {
            ResetUI();
            TextOutput = message;
            ApkInfo ??= new ApkInfo();
            ProgressHelper.SetState(ProgressState.Error, true);
            AppName = _loader.GetString("CannotOpenPackage");
            TextOutputVisibility = InstallOutputVisibility = Visibility.Visible;
            AppVersionVisibility = AppPublisherVisibility = AppCapabilitiesVisibility = Visibility.Collapsed;
        }

        private void OnDeviceChanged(object sender, DeviceDataEventArgs e)
        {
            if (IsInitialized && !IsInstalling)
            {
                _page.DispatcherQueue?.EnqueueAsync(() =>
                {
                    if (CheckDevice() && _device != null)
                    {
                        CheckAPK();
                    }
                });
            }
        }

        private bool CheckDevice()
        {
            AdvancedAdbClient client = new();
            List<DeviceData> devices = client.GetDevices();
            ConsoleOutputReceiver receiver = new();
            if (devices.Count <= 0) { return false; }
            foreach (DeviceData device in devices)
            {
                if (device == null || device.State == DeviceState.Offline) { continue; }
                if (IsOnlyWSA)
                {
                    client.ExecuteRemoteCommand("getprop ro.boot.hardware", device, receiver);
                    if (receiver.ToString().Contains("windows"))
                    {
                        _device = device ?? _device;
                        return true;
                    }
                }
                else
                {
                    DeviceData data = SettingsHelper.Get<DeviceData>(SettingsHelper.DefaultDevice);
                    if (data != null && data.Name == device.Name && data.Model == device.Model && data.Product == device.Product)
                    {
                        _device = data;
                        return true;
                    }
                }
            }
            return false;
        }

        public void OpenAPP() => new AdvancedAdbClient().StartApp(_device, ApkInfo?.PackageName);

        public async void InstallAPP()
        {
            try
            {
                IsInstalling = true;
                ProgressText = _loader.GetString("Installing");
                CancelOperationButtonText = _loader.GetString("Cancel");
                CancelOperationVisibility = LaunchWhenReadyVisibility = Visibility.Visible;
                ActionVisibility = SecondaryActionVisibility = TextOutputVisibility = InstallOutputVisibility = Visibility.Collapsed;
                if (ApkInfo.IsSplit)
                {
                    await Task.Run(() => { new AdvancedAdbClient().InstallMultiple(_device, new Stream[] { File.Open(ApkInfo.FullPath, FileMode.Open, FileAccess.Read) }, ApkInfo.PackageName); });
                }
                else if (ApkInfo.IsBundle)
                {
                    await Task.Run(() =>
                    {
                        Stream[] streams = ApkInfo.SplitApks.Select(x => File.Open(x.FullPath, FileMode.Open, FileAccess.Read)).ToArray();
                        new AdvancedAdbClient().InstallMultiple(_device, File.Open(ApkInfo.FullPath, FileMode.Open, FileAccess.Read), streams);
                    });
                }
                else
                {
                    await Task.Run(() => { new AdvancedAdbClient().Install(_device, File.Open(ApkInfo.FullPath, FileMode.Open, FileAccess.Read)); });
                }
                AppName = string.Format(_loader.GetString("InstalledFormat"), ApkInfo?.AppName);
                if (IsOpenApp)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);// 据说如果安装完直接启动会崩溃。。。
                        OpenAPP();
                        if (IsCloseAPP)
                        {
                            await Task.Delay(5000);
                            _page.DispatcherQueue.TryEnqueue(() => Application.Current.Exit());
                        }
                    });
                }
                SendResults();
                IsInstalling = false;
                SecondaryActionVisibility = Visibility.Visible;
                SecondaryActionButtonText = _loader.GetString("Launch");
                CancelOperationVisibility = LaunchWhenReadyVisibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                SendResults(ex);
                IsInstalling = false;
                TextOutput = ex.Message;
                TextOutputVisibility = InstallOutputVisibility = Visibility.Visible;
                ActionVisibility = SecondaryActionVisibility = CancelOperationVisibility = LaunchWhenReadyVisibility = Visibility.Collapsed;
            }
        }

        public async void OpenAPK(string path)
        {
            if (path != null)
            {
                _path = path;
                await Refresh();
            }
        }

        public async void OpenAPK()
        {
            FileOpenPicker FileOpen = new();
            FileOpen.FileTypeFilter.Add(".apk");
            FileOpen.FileTypeFilter.Add(".apks");
            FileOpen.FileTypeFilter.Add(".apkm");
            FileOpen.FileTypeFilter.Add(".xapk");
            FileOpen.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            // When running on win32, FileSavePicker needs to know the top-level hwnd via IInitializeWithWindow::Initialize.
            if (Window.Current == null)
            {
                IInitializeWithWindow initializeWithWindowWrapper = FileOpen.As<IInitializeWithWindow>();
                IntPtr hwnd = GetActiveWindow();
                initializeWithWindowWrapper.Initialize(hwnd);
            }

            StorageFile file = await FileOpen.PickSingleFileAsync();
            if (file != null)
            {
                _path = file.Path;
                await Refresh();
            }
        }

        public async void OpenAPK(DataPackageView data)
        {
            void CreateAPKS(IReadOnlyList<IStorageItem> items)
            {
                List<string> apks = new();
                foreach (IStorageItem item in items)
                {
                    if (item != null)
                    {
                        if (item.Name.ToLower().EndsWith(".apk"))
                        {
                            apks.Add(item.Path);
                            continue;
                        }
                        try
                        {
                            using (IArchive archive = ArchiveFactory.Open(item.Path))
                            {
                                foreach (IArchiveEntry entry in archive.Entries.Where(x => !x.Key.Contains('/')))
                                {
                                    if (entry.Key.ToLower().EndsWith(".apk"))
                                    {
                                        OpenAPK(item.Path);
                                        return;
                                    }
                                }
                            }
                            apks.Add(item.Path);
                            continue;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                if (apks.Count == 1)
                {
                    OpenAPK(apks.First());
                    return;
                }
                else if (apks.Count >= 1)
                {
                    var apklist = apks.Where(x => x.EndsWith(".apk"));
                    if (apklist.Any())
                    {
                        string temp = Path.Combine(CachesHelper.TempPath, "NetAPKTemp.apks");

                        if (!Directory.Exists(temp[..temp.LastIndexOf(@"\")]))
                        {
                            _ = Directory.CreateDirectory(temp[..temp.LastIndexOf(@"\")]);
                        }
                        else if (Directory.Exists(temp))
                        {
                            Directory.Delete(temp, true);
                        }

                        if (File.Exists(temp))
                        {
                            File.Delete(temp);
                        }

                        using (FileStream zip = File.OpenWrite(temp))
                        {
                            using (var zipWriter = WriterFactory.Open(zip, ArchiveType.Zip, CompressionType.Deflate))
                            {
                                foreach (string apk in apks.Where(x => x.EndsWith(".apk")))
                                {
                                    zipWriter.Write(Path.GetFileName(apk), apk);
                                }
                                OpenAPK(temp);
                                return;
                            }
                        }
                    }
                    else
                    {
                        var apkslist = apks.Where(x => !x.EndsWith(".apk"));
                        if (apkslist.Count() == 1)
                        {
                            OpenAPK(apkslist.First());
                            return;
                        }
                    }
                }
            }

            if (data.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items = await data.GetStorageItemsAsync();
                if (items.Count == 1)
                {
                    IStorageItem storageItem = items.First();
                    if (storageItem != null)
                    {
                        if (storageItem is StorageFolder folder)
                        {
                            List<string> apks = new();
                            var files = await folder.GetFilesAsync();
                            CreateAPKS(files);
                        }
                        else
                        {
                            if (storageItem.Name.ToLower().EndsWith(".apk"))
                            {
                                OpenAPK(storageItem.Path);
                                return;
                            }
                            try
                            {
                                using (IArchive archive = ArchiveFactory.Open(storageItem.Path))
                                {
                                    foreach (IArchiveEntry entry in archive.Entries.Where(x => !x.Key.Contains('/')))
                                    {
                                        if (entry.Key.ToLower().EndsWith(".apk"))
                                        {
                                            OpenAPK(storageItem.Path);
                                            return;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                return;
                            }
                        }
                    }
                }
                else if (items.Count >= 1)
                {
                    CreateAPKS(items);
                }
            }
        }

        private void SendResults(Exception exception = null)
        {
            if (_operation == null) { return; }
            ValueSet results = new()
            {
                ["Result"] = exception != null,
                ["Exception"] = exception
            };
            _operation.ReportCompleted(results);
        }

        public void CloseAPP()
        {
            SendResults(new Exception($"{_loader.GetString("Install")} {_loader.GetString("Cancel")}"));
            Application.Current.Exit();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                    ADBHelper.Monitor.DeviceChanged -= OnDeviceChanged;
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}