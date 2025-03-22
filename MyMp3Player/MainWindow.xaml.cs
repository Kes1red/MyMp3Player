using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using MyMp3Player.Commands;
using MyMp3Player.Data;
using Path = System.IO.Path;



namespace MyMp3Player
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private List<SongItem> _shuffledPlaylist = new List<SongItem>();
        private List<SongItem> _playedInShuffle = new List<SongItem>();
        
        
        private readonly PlaylistDatabase _playlistDb = new PlaylistDatabase();
        
        //поле для хранения путей
        private HashSet<string> _existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private readonly DispatcherTimer _progressTimer = new DispatcherTimer();
        
        public string CurrentSongTitle => CurrentSong?.Title ?? "Нет трека";
        public string CurrentArtist => CurrentSong?.Artist ?? "Неизвестный исполнитель";
        
        string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlist.db");
        
        //для системы частиц
        private Random _random = new Random();
        private DispatcherTimer _particleTimer;
        private List<Ellipse> _particles = new List<Ellipse>();
        private const int MaxParticles = 50; // Максимальное количество частиц
        
        
        private int _currentSongIndex = -1;
        private double _volume = 0.5;
        private double _playbackProgress;
        
        private double _lastVolumeBeforeMute = 0.5;
        private bool _isMuted;

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    OnPropertyChanged();
                }
            }
        }
        

        private string GetMuteIcon()
        {
            if (IsMuted) return "🔇";
            return Volume switch
            {
                >= 0.7 => "🔊",
                >= 0.3 => "🔉",
                _ => "🔈"
            };
        }
        
        public double TotalDuration {
            get => _mediaPlayer.NaturalDuration.HasTimeSpan 
                ? _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds 
                : 0;
            set => OnPropertyChanged();
        }
        
        public double CurrentPosition
        {
            get => _mediaPlayer.Position.TotalSeconds;
            set
            {
                if (!_isDragging && Math.Abs(_mediaPlayer.Position.TotalSeconds - value) > 0.1)
                {
                    _mediaPlayer.Position = TimeSpan.FromSeconds(value);
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<SongItem> Playlist { get; } = new ObservableCollection<SongItem>();
        
        public ICommand ToggleMuteCommand => new RelayCommand(() =>
        {
            if (IsMuted)
            {
                // Восстанавливаем предыдущую громкость при отключении мута
                Volume = _lastVolumeBeforeMute > 0 ? _lastVolumeBeforeMute : 0.5;
                IsMuted = false;
            }
            else
            {
                // Сохраняем текущую громкость перед включением мута
                _lastVolumeBeforeMute = Volume;
                IsMuted = true;
            }
    
            // Обновляем MediaPlayer
            _mediaPlayer.Volume = IsMuted ? 0 : Volume;
    
            // Уведомляем UI об изменениях
            OnPropertyChanged(nameof(IsMuted));
        });

        public ICommand AddTrackCommand => new RelayCommand(AddTracks);
        public ICommand PlayPauseCommand => new RelayCommand(PlayPause);
        public ICommand NextCommand => new RelayCommand(NextTrack, () => CanNavigate());
        public ICommand PreviousCommand => new RelayCommand(PreviousTrack, () => CanNavigate());
        
        private bool CanNavigate()
        {
            return Playlist.Count > 0 && CurrentSong != null;
        }
        
        public ICommand UpdateSoundIconCommand => new RelayCommand(() => 
        {
            OnPropertyChanged(nameof(MuteIcon));
        });
        public ICommand ToggleRepeatCommand => new RelayCommand(() =>
        {
            IsRepeatEnabled = !IsRepeatEnabled;
        });

        public ICommand ToggleShuffleCommand => new RelayCommand(() =>
        {
            IsShuffleEnabled = !IsShuffleEnabled;
        });
        
        
        private void ToggleMute()
        {
            IsMuted = !IsMuted;
        }
        
        
        private void UpdateMuteState()
        {
            if (IsMuted)
            {
                _lastVolumeBeforeMute = Volume;
                _mediaPlayer.Volume = 0;
            }
            else
            {
                _mediaPlayer.Volume = _lastVolumeBeforeMute;
                Volume = _lastVolumeBeforeMute; // Обновляем привязанное свойство
            }
        }
        
        private bool _isRepeatEnabled;
        public bool IsRepeatEnabled
        {
            get => _isRepeatEnabled;
            set
            {
                _isRepeatEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isShuffleEnabled;
        //public string ShuffleIcon => IsShuffleEnabled ? "🔀" : "➡";
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                if (_isShuffleEnabled != value)
                {
                    _isShuffleEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsShuffleEnabled));
            
                    if (value)
                    {
                        // Инициализируем перемешанный плейлист
                        _shuffledPlaylist = Playlist.OrderBy(x => _random.Next()).ToList();
                        _playedInShuffle.Clear();
                    }
                }
            }
        }

        
        
        
        private bool _isDragging;
        private TimeSpan _savedPosition;
        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
            _progressTimer.Stop(); // Останавливаем автообновление
        }

        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            _mediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            _progressTimer.Start(); // Возобновляем автообновление
            UpdateProgress();
        }
        
        
        
        
        public object MuteIcon
        {
            get
            {
                string resourceKey;
        
                if (IsMuted || Volume <= 0)
                {
                    resourceKey = "VolumeMuteIcon";
                }
                else if (Volume < 0.3)
                {
                    resourceKey = "VolumeLowIcon";
                }
                else if (Volume < 0.7)
                {
                    resourceKey = "VolumeMediumIcon";
                }
                else
                {
                    resourceKey = "VolumeHighIcon";
                }
        
                return Application.Current.Resources[resourceKey];
            }
        }

        
        public double Volume 
        {
            get => _volume;
            set 
            {
                value = Math.Clamp(value, 0, 1);
        
                if (Math.Abs(_volume - value) > 0.01)
                {
                    _volume = value;
            
                    // Применяем громкость к плееру, учитывая состояние мута
                    if (!IsMuted)
                    {
                        _mediaPlayer.Volume = value;
                    }
            
                    // Если установили громкость больше 0, автоматически снимаем мут
                    if (IsMuted && value > 0) 
                    {
                        IsMuted = false;
                        _mediaPlayer.Volume = value;
                    }
            
                    OnPropertyChanged();
                }
            }
        }
        
        
        // В классе MainWindow
        private SongItem _currentSong;
        public SongItem CurrentSong
        {
            get => _currentSong;
            set
            {
                _currentSong = value;
                OnPropertyChanged(nameof(CurrentSong));
                OnPropertyChanged(nameof(CurrentSongTitle)); // Добавьте эти строки
                OnPropertyChanged(nameof(CurrentArtist));    // для обновления UI
                if (value != null) 
                {
                    StartPlayback(value);
                }
            }
        }
        
        
        private void StartPlayback(SongItem song)
        {
            try 
            {
                _mediaPlayer.Open(new Uri(song.FilePath));
                _mediaPlayer.Play();
                IsPlaying = true;
        
                // Ждем инициализации длительности
                Dispatcher.BeginInvoke(new Action(() => 
                {
                    TotalDuration = _mediaPlayer.NaturalDuration.HasTimeSpan 
                        ? _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds 
                        : 0;
                    OnPropertyChanged(nameof(TotalDuration));
                    UpdatePlaybackStates(song);
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка воспроизведения: {ex.Message}");
            }
        }
        
        

        public double PlaybackProgress
        {
            get => _playbackProgress;
            set
            {
                if (Math.Abs(_playbackProgress - value) > 0.01)
                {
                    _playbackProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentTime
        {
            get => _mediaPlayer.Position.ToString(@"mm\:ss");
        }

        public string TotalTime
        {
            get => _mediaPlayer.NaturalDuration.HasTimeSpan 
                ? _mediaPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss") 
                : "--:--";
        }
        
        

        public MainWindow()
        {
            InitializeComponent();
            InitializeParticleSystem();
            InitializePlayingIndicatorAnimation ();
            DataContext = this;
            // MinimizeButton.Click += (s, e) => WindowState = WindowState.Minimized;
            // MaximizeButton.Click += (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            // CloseButton.Click += (s, e) => Close();
            
            Console.WriteLine(dbPath);
            
            LoadPlaylist();
        
            Closing += (s, e) => SavePlaylist();

            //AddTrackCommand = new RelayCommand(AddTracks);
            //PlayPauseCommand = new RelayCommand(PlayPause);
            //NextCommand = new RelayCommand(NextTrack);
            //PreviousCommand = new RelayCommand(PreviousTrack);

            _mediaPlayer.MediaOpened += (s, e) => OnPropertyChanged(nameof(TotalTime));
            _mediaPlayer.MediaEnded += (s, e) => 
            {
                if (IsRepeatEnabled && CurrentSong != null)
                {
                    // Повторяем текущий трек
                    _mediaPlayer.Position = TimeSpan.Zero;
                    _mediaPlayer.Play();
                }
                else
                {
                    // Переходим к следующему треку
                    NextTrack();
                }
            };
            
            _mediaPlayer.MediaOpened += (s, e) => 
            {
                OnPropertyChanged(nameof(MuteIcon)); // Обновляем при загрузке трека
            };

            _progressTimer.Interval = TimeSpan.FromMilliseconds(500);
            _progressTimer.Tick += (s, e) => {
                UpdateProgress();
                OnPropertyChanged(nameof(CurrentTime));
            };
            _progressTimer.Start();
            
            _mediaPlayer.MediaOpened += (s, e) => {
                TotalDuration = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                OnPropertyChanged(nameof(TotalTime));
                OnPropertyChanged(nameof(CurrentTime));
            };
        }
        
        private void InitializeParticleSystem()
        {
            // Проверяем, что Canvas существует
            if (ParticlesCanvas == null)
            {
                Debug.WriteLine("ParticlesCanvas не найден");
                return;
            }
    
            // Выводим размеры Canvas для отладки
            Debug.WriteLine($"ParticlesCanvas: Width={ParticlesCanvas.ActualWidth}, Height={ParticlesCanvas.ActualHeight}");
    
            // Если размеры нулевые, подписываемся на событие загрузки
            if (ParticlesCanvas.ActualWidth <= 0 || ParticlesCanvas.ActualHeight <= 0)
            {
                ParticlesCanvas.Loaded += (s, e) =>
                {
                    Debug.WriteLine($"ParticlesCanvas загружен: Width={ParticlesCanvas.ActualWidth}, Height={ParticlesCanvas.ActualHeight}");
                };
            }
    
            _particleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // Увеличиваем интервал
            };
    
            _particleTimer.Tick += (s, e) =>
            {
                if (IsPlaying)
                {
                    try
                    {
                        // Создаем новые частицы только если играет музыка
                        CreateParticle();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка в системе частиц: {ex.Message}");
                    }
                }
            };
    
            _particleTimer.Start();
            Debug.WriteLine("Система частиц инициализирована");
        }

        private void InitializePlayingIndicatorAnimation()
        {
            if (Bar1 == null || Bar2 == null || Bar3 == null) return;
    
            // Анимация для первой полоски
            var animation1 = new DoubleAnimation
            {
                From = 3,
                To = 12,
                Duration = TimeSpan.FromSeconds(0.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
    
            // Анимация для второй полоски
            var animation2 = new DoubleAnimation
            {
                From = 12,
                To = 4,
                Duration = TimeSpan.FromSeconds(0.65),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
    
            // Анимация для третьей полоски
            var animation3 = new DoubleAnimation
            {
                From = 6,
                To = 10,
                Duration = TimeSpan.FromSeconds(0.55),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
    
            // Запускаем анимации
            Bar1.BeginAnimation(Rectangle.HeightProperty, animation1);
            Bar2.BeginAnimation(Rectangle.HeightProperty, animation2);
            Bar3.BeginAnimation(Rectangle.HeightProperty, animation3);
        }

        private void CreateParticle()
{
    if (_particles.Count >= MaxParticles || ParticlesCanvas == null) return;

    // Создаем частицу с градиентной заливкой
    var particle = new Ellipse
    {
        Width = _random.Next(3, 7),  // Уменьшаем размер
        Height = _random.Next(3, 7),
        RenderTransformOrigin = new Point(0.5, 0.5)
    };

    // Создаем градиентную заливку для более мягкого вида
    var gradientBrush = new RadialGradientBrush();
    var spotifyGreen = Color.FromRgb(29, 185, 84); // Spotify Green
    gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, spotifyGreen.R, spotifyGreen.G, spotifyGreen.B), 0));
    gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, spotifyGreen.R, spotifyGreen.G, spotifyGreen.B), 1));
    particle.Fill = gradientBrush;
    
    // Добавляем начальную прозрачность
    particle.Opacity = _random.NextDouble() * 0.3 + 0.5; // 0.5-0.8

    // Позиционирование с более случайным начальным положением
    double left = _random.Next(50, Math.Max(51, (int)ParticlesCanvas.ActualWidth - 50));
    double top = ParticlesCanvas.ActualHeight + _random.Next(0, 20);
    Canvas.SetLeft(particle, left);
    Canvas.SetTop(particle, top);

    // Добавляем трансформацию для эффектов
    var transformGroup = new TransformGroup();
    
    // Добавляем вращение
    var rotateTransform = new RotateTransform(_random.Next(0, 360));
    transformGroup.Children.Add(rotateTransform);
    
    // Добавляем масштабирование
    var scaleTransform = new ScaleTransform(1, 1);
    transformGroup.Children.Add(scaleTransform);
    
    particle.RenderTransform = transformGroup;

    // Добавляем частицу
    ParticlesCanvas.Children.Add(particle);
    _particles.Add(particle);

    // Создаем анимацию движения вверх с разной скоростью
    var verticalAnim = new DoubleAnimation
    {
        From = top,
        To = -20,
        Duration = TimeSpan.FromSeconds(_random.Next(10, 20) + _random.NextDouble()), // Более разнообразное время
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };

    // Создаем более плавное горизонтальное движение
    var horizontalAnim = new DoubleAnimation
    {
        From = left,
        To = left + _random.Next(-60, 60), // Меньшая амплитуда
        Duration = TimeSpan.FromSeconds(_random.Next(3, 8) + _random.NextDouble()),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
        EasingFunction = new SineEase() // Синусоидальное движение для плавности
    };

    // Анимация прозрачности для плавного исчезновения
    var opacityAnim = new DoubleAnimation
    {
        From = particle.Opacity,
        To = 0,
        Duration = TimeSpan.FromSeconds(_random.Next(8, 15) + _random.NextDouble()),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
    };

    // Анимация вращения для более интересного эффекта
    var rotateAnim = new DoubleAnimation
    {
        From = 0,
        To = _random.Next(-360, 360), // Случайное вращение
        Duration = TimeSpan.FromSeconds(_random.Next(10, 20)),
        EasingFunction = new SineEase()
    };
    
    // Анимация масштабирования
    var scaleAnim = new DoubleAnimation
    {
        From = 1,
        To = _random.NextDouble() * 0.5 + 0.5, // 0.5-1.0
        Duration = TimeSpan.FromSeconds(_random.Next(5, 10)),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever
    };

    // Удаляем частицу после завершения анимации
    verticalAnim.Completed += (s, e) => RemoveParticle(particle);
    
    // Запускаем анимации
    particle.BeginAnimation(Canvas.TopProperty, verticalAnim);
    particle.BeginAnimation(Canvas.LeftProperty, horizontalAnim);
    particle.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
}
        
        private void RemoveParticle(Ellipse particle)
        {
            Dispatcher.Invoke(() => 
            {
                ParticlesCanvas.Children.Remove(particle);
                _particles.Remove(particle);
            });
        }

private void UpdateParticles()
{
    // Обновляем цвет частиц в зависимости от текущего трека
    if (CurrentSong != null && _particles.Count > 0)
    {
        try
        {
            // Можно добавить логику изменения цвета частиц
            // в зависимости от текущего трека или громкости
            foreach (var particle in _particles)
            {
                // Пример: меняем прозрачность в зависимости от громкости
                particle.Opacity = 0.3 + (Volume * 0.7);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка при обновлении частиц: {ex.Message}");
        }
    }
}
        
private async void LoadPlaylist()
{
    try
    {
        var savedPlaylist = await _playlistDb.LoadPlaylistAsync();
        Playlist.Clear();
        _existingFiles.Clear();
        
        foreach (var song in savedPlaylist)
        {
            if (!_existingFiles.Contains(song.FilePath))
            {
                Playlist.Add(song);
                _existingFiles.Add(song.FilePath);
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка загрузки: {ex.Message}");
    }
}

        private void SavePlaylist()
        {
            try
            {
                _playlistDb.SavePlaylist(Playlist.ToList());
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                MessageBox.Show("Ошибка сохранения: трек уже добавлен.", 
                    "Ошибка базы данных", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка: {ex.Message}", 
                    "Ошибка", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        private void UpdateSongIndexes()
        {
            for (int i = 0; i < Playlist.Count; i++)
            {
                Playlist[i].Index = i + 1;
            }
        }

        private void AddTracks()
{
    var openFileDialog = new OpenFileDialog
    {
        Multiselect = true,
        Filter = "Audio Files|*.mp3;*.wav;*.wma;*.aac"
    };

    if (openFileDialog.ShowDialog() == true)
    {
        foreach (var fileName in openFileDialog.FileNames)
        {
            try
            {
                // Проверка на дубликат
                if (_existingFiles.Contains(fileName))
                {
                    MessageBox.Show($"Трек уже в плейлисте:\n{fileName}", 
                                  "Дубликат", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Warning);
                    continue;
                }

                var tagFile = TagLib.File.Create(fileName);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                
                // Парсим метаданные с проверкой на whitespace
                string title = !string.IsNullOrWhiteSpace(tagFile.Tag.Title) 
                    ? tagFile.Tag.Title.Trim() 
                    : null;
                    
                string artist = !string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) 
                    ? tagFile.Tag.FirstPerformer.Trim() 
                    : null;

                // Если оба поля заполнены - используем метаданные
                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(artist))
                {
                    CreateSongItem(title, artist, fileName, tagFile);
                    _existingFiles.Add(fileName);
                    continue;
                }

                // Парсим из имени файла
                var parsedData = ParseFileName(fileNameWithoutExt);
                title = parsedData.title ?? title;
                artist = parsedData.artist ?? artist;

                // Создаем объект трека с защитой от null
                CreateSongItem(
                    title ?? fileNameWithoutExt,
                    artist ?? "Неизвестный исполнитель",
                    fileName,
                    tagFile
                );
                
                _existingFiles.Add(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обработки файла {fileName}: {ex.Message}",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }
    }
    
    UpdateSongIndexes();
    SavePlaylist();
}
        
        private void CreateSongItem(string title, string artist, string fileName, TagLib.File tagFile)
        {
            var song = new SongItem
            {
                Title = title,
                Artist = artist,
                Duration = tagFile.Properties.Duration.ToString(@"mm\:ss"),
                FilePath = fileName
            };

            // Диагностический вывод
            Debug.WriteLine($"Added track: [Artist: {song.Artist}] [Title: {song.Title}]" +
                            $"[From tags: {tagFile.Tag.Title}|{tagFile.Tag.FirstPerformer}]" +
                            $"[File: {fileName}]");

            Playlist.Add(song);
        }

private (string artist, string title) ParseFileName(string fileName)
{
    // Убираем все виды кавычек и лишние пробелы
    fileName = fileName.Replace("\"", "").Replace("'", "").Trim();
    
    // Пробуем разные разделители
    string[] separators = { " - ", "-", " – ", " — " };
    foreach (var separator in separators)
    {
        int separatorIndex = fileName.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            string artistPart = fileName.Substring(0, separatorIndex).Trim();
            string titlePart = fileName.Substring(separatorIndex + separator.Length).Trim();

            // Проверяем что обе части не пустые
            if (!string.IsNullOrWhiteSpace(artistPart) && !string.IsNullOrWhiteSpace(titlePart))
            {
                return (artistPart, titlePart);
            }
        }
    }

    // Если разделитель не найден или части пустые
    return (null, null);
}

        
        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                    OnPropertyChanged(nameof(PlayPauseIcon)); // Обновляем иконку
                }
            }
        }
        
        public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

        private void PlayPause()
        {
            if (_mediaPlayer.Source == null) return;

            if (IsPlaying)
            {
                _mediaPlayer.Pause();
            }
            else
            {
                if (_mediaPlayer.Position.TotalSeconds >= _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds)
                {
                    _mediaPlayer.Position = TimeSpan.Zero;
                }
                _mediaPlayer.Play();
            }
            IsPlaying = !IsPlaying;
        }
        

        private void TogglePlayPause()
        {
            if (_mediaPlayer.IsMuted || _mediaPlayer.Volume == 0)
            {
                _mediaPlayer.Play();
            }
            else
            {
                _mediaPlayer.Pause();
            }
        }

        private void PlaySelectedSong()
        {
            if (PlaylistView.SelectedItem is SongItem selectedSong && selectedSong.FilePath != null)
            {
                // Обновляем CurrentSong без немедленного начала воспроизведения
                CurrentSong = selectedSong;
            }
        }

        private void NextTrack()
        {
            if (Playlist.Count == 0 || CurrentSong == null) return;

            if (IsShuffleEnabled)
            {
                PlayNextShuffled();
            }
            else
            {
                PlayNextNormal();
            }
    
            UpdatePlaybackStates(CurrentSong);
        }
        
        private void PlayNextNormal()
        {
            int currentIndex = Playlist.IndexOf(CurrentSong);
            int newIndex = currentIndex + 1;

            if (newIndex >= Playlist.Count)
            {
                if (IsRepeatEnabled) newIndex = 0;
                else return;
            }

            CurrentSong = Playlist[newIndex];
            PlaylistView.SelectedIndex = newIndex;
        }

        private void PlayNextShuffled()
        {
            // Удаляем текущий трек из непроигранных
            _shuffledPlaylist.Remove(CurrentSong);
            _playedInShuffle.Add(CurrentSong);

            // Если все треки проиграны - перемешиваем заново
            if (_shuffledPlaylist.Count == 0)
            {
                _shuffledPlaylist = _playedInShuffle.OrderBy(x => _random.Next()).ToList();
                _playedInShuffle.Clear();
            }

            // Выбираем следующий трек
            CurrentSong = _shuffledPlaylist.First();
            PlaylistView.SelectedIndex = Playlist.IndexOf(CurrentSong);
        }

        

        private void PreviousTrack()
        {
            if (Playlist.Count == 0) return;
    
            var newIndex = Playlist.IndexOf(CurrentSong) - 1;
            if (newIndex < 0) newIndex = Playlist.Count - 1;
    
            CurrentSong = Playlist[newIndex];
            PlaylistView.SelectedIndex = newIndex;
        }

        private void UpdatePlaybackStates(SongItem currentSong)
        {
            foreach (var song in Playlist)
            {
                song.IsPlaying = song == currentSong;
            }
    
            // Обновляем информацию о текущем треке
            OnPropertyChanged(nameof(CurrentSongTitle));
            OnPropertyChanged(nameof(CurrentArtist));
        }

        private void UpdateProgress()
        {
            if (!_isDragging && _mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = _mediaPlayer.Position.TotalSeconds;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}