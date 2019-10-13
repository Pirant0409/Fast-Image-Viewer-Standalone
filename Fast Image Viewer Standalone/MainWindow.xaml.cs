﻿using FIVStandard.Comparers;
using FIVStandard.Modules;
using FIVStandard.Views;
using Gu.Localization;
using MahApps.Metro.Controls;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;

namespace FIVStandard
{
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        private ThumbnailItemData imageItem;

        public ThumbnailItemData ImageItem
        {
            get
            {
                return imageItem;
            }
            set
            {
                if (imageItem == value) return;

                imageItem = value;
                //UpdateCurrentImage();
                OnPropertyChanged();
            }
        }

        public ListCollectionView ImagesDataView { get; }

        private ObservableCollection<ThumbnailItemData> imagesData = new ObservableCollection<ThumbnailItemData>();

        public ObservableCollection<ThumbnailItemData> ImagesData
        {
            get
            {
                return imagesData;
            }
        }

        //public List<string> ImagesFound { get; set; } = new List<string>();

        private bool IsAnimated { get; set; } = false;

        private bool IsPaused { get; set; } = false;

        #region Image Properties
        private Uri _mediaSource = null;

        public Uri MediaSource
        {
            get
            {
                return _mediaSource;
            }
            set
            {
                _mediaSource = value;
                OnPropertyChanged();
            }
        }

        private BitmapImage _imageSource = null;

        public BitmapImage ImageSource
        {
            get
            {
                return _imageSource;
            }
            set
            {
                _imageSource = value;
                OnPropertyChanged();
            }
        }

        private int _imgWidth = 0;
        public int ImgWidth
        {
            get
            {
                return _imgWidth;
            }
            set
            {
                _imgWidth = value;
                OnPropertyChanged();
                OnPropertyChanged("ImgResolution");
            }
        }

        private int _imgHeight = 0;
        public int ImgHeight
        {
            get
            {
                return _imgHeight;
            }
            set
            {
                _imgHeight = value;
                OnPropertyChanged();
                OnPropertyChanged("ImgResolution");
            }
        }

        public string ImgResolution
        {
            get
            {
                if (_imgWidth == 0 || _imgHeight == 0)
                    return "owo";
                else
                    return $"{_imgWidth}x{_imgHeight}";
            }
        }

        public Rotation ImageRotation { get; set; } = Rotation.Rotate0;
        #endregion

        public UpdateCheck AppUpdater { get; set; }

        public SettingsManager Settings { get; set; }

        public CopyFileToClipboard ToClipboard { get; set; }

        public string StartupPath;//program startup path

        //public static MainWindow AppWindow;//used for debugging ZoomBorder

        private readonly string[] filters = new string[] { ".jpg", ".jpeg", ".png", ".gif"/*, ".tiff"*/, ".bmp"/*, ".svg"*/, ".ico"/*, ".mp4", ".avi" */, ".JPG", ".JPEG", ".GIF", ".BMP", ".ICO", ".PNG" };//TODO: doesnt work: tiff svg
        private readonly OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "Images (*.JPG, *.JPEG, *.PNG, *.GIF, *.BMP, *ICO)|*.JPG;*.JPEG;*.PNG;*.GIF;*.BMP;*.ICO"/* + "|All files (*.*)|*.*" */};

        private System.Windows.Controls.Button editingButton = null;

        private bool IsDeletingFile { get; set; } = false;

        private string ActiveFile { get; set; } = "";//file name + extension
        private string ActiveFolder { get; set; } = "";//directory
        public string ActivePath { get; set; } = "";//directory + file name + extension

        private readonly FileSystemWatcher fsw = new FileSystemWatcher()
        {
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite
            , IncludeSubdirectories = false
        };

        public readonly Notifier notifier = new Notifier(cfg =>
        {
            cfg.PositionProvider = new WindowPositionProvider(
                parentWindow: Application.Current.MainWindow,
                corner: Corner.BottomRight,
                offsetX: 10,
                offsetY: 10);

            cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                notificationLifetime: TimeSpan.FromSeconds(5),
                maximumNotificationCount: MaximumNotificationCount.FromCount(4));

            cfg.Dispatcher = Application.Current.Dispatcher;
        });

        public MainWindow()
        {
            InitializeComponent();

            ImagesDataView = CollectionViewSource.GetDefaultView(imagesData) as ListCollectionView;
            //ImagesDataView.SortDescriptions.Add(new SortDescription { PropertyName = "ThumbnailName", Direction = ListSortDirection.Ascending });

            ImagesDataView.CustomSort = new NaturalOrderComparer(false);

            AppUpdater = new UpdateCheck(this);
            Settings = new SettingsManager(this);
            ToClipboard = new CopyFileToClipboard();

            //create new watcher events for used directory
            //fsw.Changed += Fsw_Updated;
            fsw.Created += Fsw_Created;
            fsw.Deleted += Fsw_Deleted;
            fsw.Renamed += Fsw_Renamed;

            DataContext = this;

            Settings.Load();

            //AppWindow = this;//used for debugging ZoomBorder
        }

        private void OnAppLoaded(object sender, RoutedEventArgs e)
        {
            if(Settings.CheckForUpdatesStartToggle)
                AppUpdater.CheckForUpdates(UpdateCheckType.SilentVersionCheck);

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length > 0)//get startup path
            {
                StartupPath = Path.GetDirectoryName(args[0]);

#if DEBUG
                string path = @"D:\Google Drive\temp\alltypes\3.png";

                OpenNewFile(path);
#endif
            }

            if (args.Length > 1)
            {
                OpenNewFile(args[1]);
            }

            /*notifier.ShowInformation("");
            notifier.ShowSuccess("");
            notifier.ShowWarning("");
            notifier.ShowError("");*/
        }

        /*private void Fsw_Updated(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string cultureChangeType;
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                        cultureChangeType = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.ChangedWatcher));
                        break;
                    default:
                        cultureChangeType = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.AllWatcher));
                        break;
                }
                notifier.ShowInformation($"{cultureChangeType} \"{e.Name}\"");

                GetDirectoryFiles(ActiveFolder);

                if (ImagesData.Count < 1)
                {
                    ClearAllMedia();
                    return;
                }

                FindIndexInFiles(ActiveFile);
                //SetTitleInformation();

                ChangeImage(0, false);
            });
        }*/

        private void Fsw_Created(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ThumbnailItemData tt = new ThumbnailItemData
                {
                    ThumbnailName = e.Name,
                    ThumbnailImage = LoadThumbnail(e.FullPath)
                };
                ImagesData.Add(tt);

                //ImagesData = ImagesData.OrderByAlphaNumeric((a) => a.ThumbnailName).ToList();//sort back changed list

                ChangeImage(0, false);

                FindIndexInFiles(ActiveFile);

                notifier.ShowInformation($"{Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.CreatedWatcher))} \"{e.Name}\"");
            });
        }

        private void Fsw_Deleted(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < ImagesDataView.Count; i++)
                {
                    if (e.Name == ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName)
                    {
                        ImagesData.RemoveAt(i);

                        break;
                    }
                }

                ChangeImage(0, false);

                FindIndexInFiles(ActiveFile);

                notifier.ShowInformation($"{Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.DeletedWatcher))} \"{e.Name}\"");
            });
        }

        private void Fsw_Renamed(object sender, RenamedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < ImagesDataView.Count; i++)
                {
                    if (e.OldName == ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName)
                    {
                        ImagesData.RemoveAt(i);

                        ThumbnailItemData tt = new ThumbnailItemData
                        {
                            ThumbnailName = e.Name,
                            ThumbnailImage = LoadThumbnail(e.FullPath)
                        };
                        ImagesData.Add(tt);

                        //ImagesData = ImagesData.OrderByAlphaNumeric((a) => a.ThumbnailName).ToList();//sort back changed list

                        //if the viewed item is the changed one, update it
                        if (imageItem.ThumbnailName == e.OldName)
                        {
                            ActiveFile = ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName;
                        }

                        ChangeImage(0, false);

                        FindIndexInFiles(ActiveFile);

                        break;
                    }
                }

                notifier.ShowInformation($"{Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.RenamedWatcher))} \"{e.OldName}\" -> \"{e.Name}\"");
            });
        }

        public void OpenNewFile(string path)
        {
            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            ActiveFile = Path.GetFileName(path);
            ActiveFolder = Path.GetDirectoryName(path);
            ActivePath = path;

            fsw.Path = ActiveFolder;
            fsw.EnableRaisingEvents = true;//File Watcher is enabled/disabled

            GetDirectoryFiles(ActiveFolder);

            FindIndexInFiles(ActiveFile);

            NewUri(path);
        }

        private void GetDirectoryFiles(string searchFolder)
        {
            ImagesData.Clear();
            List<string> filesFound = new List<string>();

            //filesFound.AddRange(Directory.GetFiles(searchFolder, "*.*", SearchOption.TopDirectoryOnly));
            //filesFound.AddRange(Directory.EnumerateFiles(searchFolder).OrderBy(filename => filename));
            //filesFound.OrderBy(p => p.Substring(0)).ToList();//probably doesnt work
            filesFound.AddRange(Directory.EnumerateFiles(searchFolder));

            int c = filesFound.Count;
            for (int i = 0; i < c; i++)
            {
                if (filters.Any(Path.GetExtension(filesFound[i]).Contains))
                {
                    filesFound[i] = Path.GetFileName(filesFound[i]);

                    ThumbnailItemData tt = new ThumbnailItemData
                    {
                        ThumbnailName = filesFound[i],
                        //ThumbnailImage = LoadThumbnailAsync(Path.Combine(ActiveFolder, ImagesFound[i]))
                    };
                    ImagesData.Add(tt);
                }
            }

            //ImagesFound.Sort(new NameComparer());
            //ImagesData = ImagesData.OrderByAlphaNumeric((a) => a.ThumbnailName).ToList();

            tokenSource2?.Cancel();
            LoadAllThumbnailsAsync();
        }

        /*void UpdateCurrentImage()
        {
            ChangeImage(ImagesDataView.CurrentPosition, true);
        }*/

        private void FindIndexInFiles(string openedFile)
        {
            int L = ImagesDataView.Count;
            for (int i = 0; i < L; i++)
            {
                if(openedFile == ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName)
                {
                    ImagesDataView.MoveCurrentToPosition(i);
                    thumbnailList.SelectedIndex = ImagesDataView.CurrentPosition;
                    ActiveFile = ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName;
                    ActivePath = Path.Combine(ActiveFolder, ActiveFile);

                    SetTitleInformation();

                    break;
                }
            }
        }

        private void SetTitleInformation()
        {
            Title = $"[{ImagesDataView.CurrentPosition + 1}/{ImagesDataView.Count}] {imageItem.ThumbnailName} ||| {ImagesDataView.CurrentPosition}";
        }

        /// <summary>
        /// Clear all data (as if program is opened without opening an image)
        /// </summary>
        private void ClearAllMedia()
        {
            ImagesData.Clear();
            MediaSource = null;
            ImageSource = null;
            ImgWidth = 0;
            ImgHeight = 0;
            Title = "FIV";
        }

        private void ChangeImage(int jump, bool moveToIndex)
        {
            if (ImagesData.Count == 0)//no more images in the folder - go back to default null
            {
                ClearAllMedia();
                return;
            }

            int jumpIndex = jump;

            if (moveToIndex)
            {
                //ImagesDataView.MoveCurrentToPosition(jumpIndex);
            }
            else
            {
                jumpIndex += ImagesDataView.CurrentPosition;

                //wrap around a limit between 0 and how many images there are (minus 1)
                if (jumpIndex < 0) jumpIndex = ImagesData.Count - 1;
                if (jumpIndex >= ImagesDataView.Count) jumpIndex = 0;

                ImagesDataView.MoveCurrentToPosition(jumpIndex);
            }

            //ImageItem = ((ThumbnailItemData)ImagesDataView.GetItemAt(jumpIndex));

            if (!FileSystem.FileExists(Path.Combine(ActiveFolder, imageItem.ThumbnailName)))//keep moving onward until we find an existing file
            {
                //remove nonexistent file from list - if there are more than 1
                if (ImagesData.Count > 1)
                {
                    ImagesData.RemoveAt(ImagesDataView.CurrentPosition);
                    SetTitleInformation();
                }

                ChangeImage(jump, false);

                return;
            }

            ActiveFile = ImageItem.ThumbnailName;
            ActivePath = Path.Combine(ActiveFolder, ActiveFile);

            NewUri(ActivePath);

            SetTitleInformation();
        }

        private void TogglePause()
        {
            /*controller = ImageBehavior.GetAnimationController(MainImage);

            if (!isAnimated) return;

            if (controller.IsPaused)
                controller.Play();
            else
                controller.Pause();*/

            if (IsAnimated)
            {
                if (IsPaused)
                {
                    MediaView.Play();
                    IsPaused = false;
                }
                else
                {
                    MediaView.Pause();
                    IsPaused = true;
                }
            }
        }

        private void NewUri(string path)
        {
#if DEBUG
            Stopwatch stopwatch = new Stopwatch();//DEBUG
            stopwatch.Start();//DEBUG
#endif
            string pathext = Path.GetExtension(path);
            if (pathext == ".gif"/* || pathext == ".mp4" || pathext == ".avi"*/)
            {
                IsAnimated = true;
            }
            else
                IsAnimated = false;

            Uri uri = new Uri(path, UriKind.Absolute);

            if (IsAnimated)
            {
                borderImg.Visibility = Visibility.Hidden;
                border.Visibility = Visibility.Visible;

                ImageSource = null;
                MediaSource = uri;

                IsPaused = false;

                MediaView.Play();
                border.Reset();
            }
            else
            {
                borderImg.Visibility = Visibility.Visible;
                border.Visibility = Visibility.Hidden;

                GetImageInformation(ActivePath);

                //MediaView?.Close();
                MediaSource = null;
                ImageSource = LoadImage(path);

                borderImg.Reset();
            }

#if DEBUG
            stopwatch.Stop();//DEBUG
            notifier.ShowError($"NewUri time: {stopwatch.ElapsedMilliseconds}ms");//DEBUG
#endif

        }

        public BitmapImage LoadImage(string path)
        {
            BitmapImage imgTemp = new BitmapImage();
            FileStream stream = File.OpenRead(path);
            imgTemp.BeginInit();
            imgTemp.CacheOption = BitmapCacheOption.OnLoad;//TODO: remove this so it loads faster - needs to make workaround for deleting file from file lockup
            //imgTemp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;//TODO: remove this so it loads faster - needs to make workaround for deleting file
            imgTemp.StreamSource = stream;

            if (Settings.DownsizeImageToggle)
            {
                Rect r = WpfScreen.GetScreenFrom(this).ScreenBounds;
                /*if (ImgWidth > borderImg.ActualWidth)
                    imgTemp.DecodePixelWidth = (int)r.Width;
                else if (ImgHeight > borderImg.ActualHeight)
                    imgTemp.DecodePixelHeight = (int)r.Height;*/

                imgTemp.DecodePixelWidth = (int)(ImgWidth * ScaleToBox(ImgWidth, (int)r.Width, ImgHeight, (int)r.Height));
            }
            if (ImageRotation != Rotation.Rotate0)
                imgTemp.Rotation = ImageRotation;

            imgTemp.EndInit();
            imgTemp.Freeze();
            stream.Close();
            stream.Dispose();

            return imgTemp;
        }

        CancellationTokenSource tokenSource2;
        CancellationToken ct;

        public Task LoadAllThumbnailsAsync()
        {
            tokenSource2 = new CancellationTokenSource();
            ct = tokenSource2.Token;

            return Task.Run(() =>
            {
                try
                {
                    int c = ImagesData.Count;
                    for (int i = 0; i < c; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        BitmapImage imgTemp = new BitmapImage();
                        FileStream stream = File.OpenRead(Path.Combine(ActiveFolder, ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName));
                        imgTemp.BeginInit();
                        imgTemp.CacheOption = BitmapCacheOption.OnLoad;
                        imgTemp.StreamSource = stream;

                        imgTemp.DecodePixelWidth = 80;
                        //imgTemp.DecodePixelHeight = 80;

                        using (var imageStream = File.OpenRead(Path.Combine(ActiveFolder, ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailName)))
                        {
                            System.Drawing.Image img = System.Drawing.Image.FromStream(imageStream);

                            //ImgWidth = img.Width;
                            //ImgHeight = img.Height;
                            try
                            {
                                ExifOrientations eo = GetImageOreintation(img);
                                Rotation imgRotation = OrientationDictionary[(int)eo];//eo angle from index

                                if (imgRotation != Rotation.Rotate0)
                                    imgTemp.Rotation = imgRotation;
                            }
                            catch
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    string cultureTranslated = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.ImgOrientationFailedMsg));
                                    notifier.ShowError(cultureTranslated);
                                });
                            }
                            img.Dispose();
                        }

                        imgTemp.EndInit();
                        imgTemp.Freeze();
                        stream.Close();
                        stream.Dispose();

                        ((ThumbnailItemData)ImagesDataView.GetItemAt(i)).ThumbnailImage = imgTemp;
                    }
                }
                catch
                {

                }
            }, tokenSource2.Token);
        }

        public BitmapImage LoadThumbnail(string path)
        {
            BitmapImage imgTemp = new BitmapImage();
            FileStream stream = File.OpenRead(path);
            imgTemp.BeginInit();
            imgTemp.CacheOption = BitmapCacheOption.OnLoad;
            imgTemp.StreamSource = stream;

            imgTemp.DecodePixelWidth = 80;
            imgTemp.DecodePixelHeight = 80;

            using (var imageStream = File.OpenRead(path))
            {
                System.Drawing.Image img = System.Drawing.Image.FromStream(imageStream);

                //ImgWidth = img.Width;
                //ImgHeight = img.Height;
                try
                {
                    ExifOrientations eo = GetImageOreintation(img);
                    Rotation imgRotation = OrientationDictionary[(int)eo];//eo angle from index

                    if (imgRotation != Rotation.Rotate0)
                        imgTemp.Rotation = imgRotation;
                }
                catch
                {
                    string cultureTranslated = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.ImgOrientationFailedMsg));
                    notifier.ShowError(cultureTranslated);
                }
                img.Dispose();
            }

            imgTemp.EndInit();
            imgTemp.Freeze();
            stream.Close();
            stream.Dispose();

            return imgTemp;
        }

        private double ScaleToBox(double w, double sw, double h, double sh)
        {
            double scaleWidth = sw / w;
            double scaleHeight = sh / h;

            double scale = Math.Min(scaleWidth, scaleHeight);

            return scale;
        }

        public void ExploreFile()
        {
            try
            {
                if (File.Exists(ActivePath))
                {
                    //Clean up file path so it can be navigated OK
                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", Path.GetFullPath(ActivePath)));
                }
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                notifier.ShowInformation(e.Message);
            }
        }

        private Task DeleteToRecycleAsync(string path)
        {
            if (!File.Exists(path)) return Task.CompletedTask;

            return Task.Run(() =>
            {
                try
                {
                    IsDeletingFile = true;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        string cultureTranslated = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.Deleting));
                        Title = $"{cultureTranslated} {ActiveFile}...";
                    });

                    if (FileSystem.FileExists(path))
                    {
                        FileSystem.DeleteFile(path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);

                        //remove deleted item from list
                        /*Application.Current.Dispatcher.Invoke(() => this is done in the file watcher now
                        {
                            ImagesFound.RemoveAt(ImageIndex);
                            ChangeImage(-1);//go back to a previous file after deletion
                            //SetTitleInformation();
                        });*/
                    }
                    else
                    {
                        //MessageBox.Show("File not found: " + path);
                        string cultureTranslated = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.FileNotFoundMsg));
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            notifier.ShowWarning($"{cultureTranslated}: {path}");
                        });
                    }

                    IsDeletingFile = false;
                }
                catch (Exception e)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        //MessageBox.Show(e.Message + "\nIndex: " + ImageIndex);
                        notifier.ShowError(e.Message + "\nIndex: " + ImagesDataView.CurrentPosition);
                    });
                }
            });
        }

        /// <summary>
        /// Gets the gif image information (width, height, orientation)
        /// </summary>
        private void GetImageInformation(string path)
        {
            if (ImagesData.Count == 0) return;

            using (var imageStream = File.OpenRead(path))
            {
                //var decoder = BitmapDecoder.Create(imageStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.Default);
                //ImgWidth = decoder.Frames[0].PixelWidth;
                //ImgHeight = decoder.Frames[0].PixelHeight;

                System.Drawing.Image img = System.Drawing.Image.FromStream(imageStream);
                
                ImgWidth = img.Width;
                ImgHeight = img.Height;
                try
                {
                    ExifOrientations eo = GetImageOreintation(img);
                    ImageRotation = OrientationDictionary[(int)eo];//eo angle from index

#if DEBUG
                    //notifier.ShowInformation($"Image Orientation: [angle: {ImageRotation}] {eo}");
#endif
                }
                catch
                {
                    string cultureTranslated = Translator.Translate(Properties.Resources.ResourceManager, nameof(Properties.Resources.ImgOrientationFailedMsg));
                    notifier.ShowError(cultureTranslated);
                }
                img.Dispose();
            }

            /*if (MediaView.NaturalDuration.HasTimeSpan)//used for videos (avi mp4 etc.)
            {
                TimeSpan ts = MediaView.NaturalDuration.TimeSpan;

                if(ts.TotalSeconds > 0)
                {
                    ImageInfoText.Text += "\nDuration ";

                    if (ts.Hours > 0)
                        ImageInfoText.Text += $"{ts.Hours}H ";

                    if (ts.Minutes > 0)
                        ImageInfoText.Text += $"{ts.Minutes}m ";

                    if (ts.Seconds > 0)
                        ImageInfoText.Text += $"{ts.Seconds}s";
                }
            }*/
        }

        private void ImageCopyToClipboardCall()
        {
            //ToClipboard.CopyToClipboard(ActivePath);
            if (IsAnimated)
                ToClipboard.ImageCopyToClipboard(new BitmapImage(MediaSource));
            else
                ToClipboard.ImageCopyToClipboard(ImageSource);
        }

        /*private void FileCopyToClipboardCall()
        {
            ToClipboard.FileCopyToClipboard(ActivePath);
        }*/

        private void FileCutToClipboardCall()
        {
            ToClipboard.FileCutToClipBoard(ActivePath);
        }

        #region XAML events
        private void OnClipEnded(object sender, RoutedEventArgs e)
        {
            MediaView.Position = new TimeSpan(0, 0, 1);
            MediaView.Play();
        }

        private void OnDonateClick(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=6ZXTCHB3JXL4Q&source=url");
            Process.Start(sInfo);
        }

        private void OnClipOpened(object sender, RoutedEventArgs e)
        {
            GetImageInformation(ActivePath);
        }

        private void OnCopyToClipboard(object sender, RoutedEventArgs e)
        {
            ImageCopyToClipboardCall();
        }

        private void OnCutToClipboard(object sender, RoutedEventArgs e)
        {
            FileCutToClipboardCall();
        }

        private void OnLanguageClick(object sender, RoutedEventArgs e)
        {
            if (Settings.ShownLanguageDropIndex >= Settings.ShownLanguage.Count - 1)
                Settings.ShownLanguageDropIndex = 0;
            else
                Settings.ShownLanguageDropIndex++;

            ShownLanguageDrop.SelectedIndex = Settings.ShownLanguageDropIndex;

            //ChangeAccent();//called in OnAccentChanged
        }

        private void OnAccentClick(object sender, RoutedEventArgs e)
        {
            if (Settings.ThemeAccentDropIndex >= Settings.ThemeAccents.Count - 1)
                Settings.ThemeAccentDropIndex = 0;
            else
                Settings.ThemeAccentDropIndex++;

            ThemeAccentDrop.SelectedIndex = Settings.ThemeAccentDropIndex;

            //ChangeAccent();//called in OnAccentChanged
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            DeleteToRecycleAsync(ActivePath);
        }

        private void OnOpenFileLocation(object sender, RoutedEventArgs e)
        {
            ExploreFile();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Settings.ShortcutButtonsOn == false)
            {
                if (e.Key == Key.System || e.Key == Key.LWin || e.Key == Key.RWin) return;//blacklisted keys

                if(e.Key != Key.Escape)
                {
                    editingButton.Tag = e.Key;
                    //MessageBox.Show(((int)e.Key).ToString());

                    Settings.UpdateAllKeysProperties();
                }

                Settings.ShortcutButtonsOn = true;

                return;
            }

            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            if (e.Key == Settings.GoForwardKey)
            {
                ChangeImage(1, false);//go forward
            }
            if (e.Key == Settings.GoBackwardKey)
            {
                ChangeImage(-1, false);//go back
            }

            if (e.Key == Settings.PauseKey)
            {
                TogglePause();
            }

            if (e.Key == Settings.DeleteKey && ImagesData.Count > 0)
            {
                DeleteToRecycleAsync(ActivePath);
            }

            if (e.Key == Settings.StretchImageKey)
            {
                Settings.StretchImageToggle = !Settings.StretchImageToggle;
            }

            if (e.Key == Settings.DownsizeImageKey)
            {
                Settings.DownsizeImageToggle = !Settings.DownsizeImageToggle;
            }

            if (e.Key == Settings.ExploreFileKey)
            {
                ExploreFile();
            }

            if(e.Key == Settings.CopyImageToClipboardKey)
            {
                ImageCopyToClipboardCall();
            }

            if(e.Key == Settings.CutFileToClipboardKey)
            {
                FileCutToClipboardCall();
            }
        }

        private void OnClick_Prev(object sender, RoutedEventArgs e)
        {
            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            ChangeImage(-1, false);//go back
        }

        private void OnClick_Next(object sender, RoutedEventArgs e)
        {
            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            ChangeImage(1, false);//go forward
        }

        private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            if (e.ChangedButton == MouseButton.XButton1)
            {
                ChangeImage(-1, false);//go back
            }
            if (e.ChangedButton == MouseButton.XButton2)
            {
                ChangeImage(1, false);//go forward
            }
        }

        private void OnOpenBrowseImage(object sender, RoutedEventArgs e)
        {
            if (IsDeletingFile || Settings.ShortcutButtonsOn == false) return;

            Nullable<bool> result = openFileDialog.ShowDialog();
            if (result == true)
            {
                OpenNewFile(openFileDialog.FileName);
            }
            /*else
            {
                //cancelled dialog
            }*/

            //GC.Collect();
        }

        private void OnShortcutClick(object sender, RoutedEventArgs e)
        {
            editingButton = (System.Windows.Controls.Button)sender;

            Settings.ShortcutButtonsOn = false;//disable the buttons until done editing

            //TODO: put text when editing for user to know; save changed buttons; add reset button for key resets

            //Binding myBinding = BindingOperations.GetBinding(b, System.Windows.Controls.Button.ContentProperty);
            //string p = myBinding.Path.Path;
        }

        private void OnRemoveShortcutClick(object sender, RoutedEventArgs e)
        {
            editingButton = (System.Windows.Controls.Button)sender;

            editingButton.Tag = Key.None;

            Settings.UpdateAllKeysProperties();
        }

        private void OnResetSettingsClick(object sender, RoutedEventArgs e)
        {
            Settings.ResetToDefault();
        }

        private void OnCheckUpdateClick(object sender, RoutedEventArgs e)
        {
            AppUpdater.CheckForUpdates(UpdateCheckType.SilentVersionCheck);
        }

        private void OnForceDownloadSetupClick(object sender, RoutedEventArgs e)
        {
            AppUpdater.CheckForUpdates(UpdateCheckType.FullUpdateForced);
        }

        private void ThumbnailList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ImagesDataView.CurrentPosition < 0) return;

            ListBox box = (ListBox)sender;

            ChangeImage(0, true);

            box.ScrollIntoView(imagesData[ImagesDataView.CurrentPosition]);
        }
        #endregion

        /*private int ParseStringToOnlyInt(string input)
        {
            return int.Parse(string.Join("", input.Where(x => char.IsDigit(x))));
        }*/

        // Orientations
        public const int OrientationId = 0x0112;// 274 / 0x0112

        public enum ExifOrientations
        {
            Unknown = 0,//0
            TopLeft = 1,//0
            TopRight = 2,//90
            BottomRight = 3,//180
            BottomLeft = 4,//270
            LeftTop = 5,//0
            RightTop = 6,//90
            RightBottom = 7,//180
            LeftBottom = 8,//270
        }

        readonly Dictionary<int, Rotation> OrientationDictionary = new Dictionary<int, Rotation>()
        {
            {0, Rotation.Rotate0},
            {1, Rotation.Rotate0},
            {2, Rotation.Rotate90},
            {3, Rotation.Rotate180},
            {4, Rotation.Rotate270},
            {5, Rotation.Rotate0},
            {6, Rotation.Rotate90},
            {7, Rotation.Rotate180},
            {8, Rotation.Rotate270}
        };

        // Return the image's orientation
        public static ExifOrientations GetImageOreintation(System.Drawing.Image img)
        {
            // Get the index of the orientation property
            int orientation_index = Array.IndexOf(img.PropertyIdList, OrientationId);

            // If there is no such property, return Unknown
            if (orientation_index < 0) return ExifOrientations.Unknown;

            // Return the orientation value
            return (ExifOrientations)
                img.GetPropertyItem(OrientationId).Value[0];
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
