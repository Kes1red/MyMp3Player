using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Microsoft.Win32;
using MyMp3Player.Commands;
using MyMp3Player.Data;
using Path = System.IO.Path;


namespace MyMp3Player
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly PlaylistDatabase _playlistDb = new PlaylistDatabase();
        
        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private readonly DispatcherTimer _progressTimer = new DispatcherTimer();
        
        
        //для системы частиц
        private Random _random = new Random();
        private DispatcherTimer _particleTimer;
        private List<Ellipse> _particles = new List<Ellipse>();
        private const int MaxParticles = 100;
        
        
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
                    UpdateMuteState();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MuteIcon));
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
        
        public ICommand ToggleMuteCommand => new RelayCommand(ToggleMute);
        public ICommand AddTrackCommand => new RelayCommand(AddTracks);
        public ICommand PlayPauseCommand => new RelayCommand(PlayPause);
        public ICommand NextCommand => new RelayCommand(NextTrack, () => CanNavigate());
        public ICommand PreviousCommand => new RelayCommand(PreviousTrack, () => CanNavigate());
        
        
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
        
        
        
        
        private bool CanNavigate() => Playlist.Count > 0;

        public string MuteIcon => IsMuted ? "🔇" : 
            Volume switch {
                >= 0.7 => "🔊",
                >= 0.3 => "🔉",
                _ => "🔈"
            };
        
        public double Volume {
            get => _volume;
            set {
                if (Math.Abs(_volume - value) > 0.01) {
                    _volume = value;
                    _mediaPlayer.Volume = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MuteIcon)); // Добавляем уведомление об изменении иконки
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
            DataContext = this;
            
            LoadPlaylist();
        
            Closing += (s, e) => SavePlaylist();

            //AddTrackCommand = new RelayCommand(AddTracks);
            //PlayPauseCommand = new RelayCommand(PlayPause);
            //NextCommand = new RelayCommand(NextTrack);
            //PreviousCommand = new RelayCommand(PreviousTrack);

            _mediaPlayer.MediaOpened += (s, e) => OnPropertyChanged(nameof(TotalTime));
            _mediaPlayer.MediaEnded += (s, e) => NextTrack();

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
        _particleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        
        _particleTimer.Tick += (s, e) =>
        {
            if (IsPlaying)
            {
                // Создаем новые частицы только если играет музыка
                CreateParticle();
                
                // Обновляем положение существующих частиц
                UpdateParticles();
            }
        };
        
        _particleTimer.Start();
    }

    private void CreateParticle()
    {
        if (_particles.Count >= MaxParticles) return;
        
        // Создаем новую частицу
        var particle = new Ellipse
        {
            Style = (Style)FindResource("MusicParticle"),
            Width = _random.Next(3, 7),
            Height = _random.Next(3, 7)
        };
        
        // Устанавливаем начальное положение частицы
        double left = _random.Next(0, (int)ParticlesCanvas.ActualWidth);
        Canvas.SetLeft(particle, left);
        Canvas.SetTop(particle, ParticlesCanvas.ActualHeight);
        
        // Добавляем частицу на Canvas и в список
        ParticlesCanvas.Children.Add(particle);
        _particles.Add(particle);
        
        // Создаем анимацию движения
        var animation = new DoubleAnimation
        {
            From = ParticlesCanvas.ActualHeight,
            To = -20,
            Duration = TimeSpan.FromSeconds(_random.Next(5, 15)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        // Добавляем случайное колебание по горизонтали
        var horizontalAnimation = new DoubleAnimation
        {
            From = left,
            To = left + _random.Next(-50, 50),
            Duration = TimeSpan.FromSeconds(_random.Next(3, 8)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        
        // Добавляем пульсацию размера
        var pulseAnimation = new DoubleAnimation
        {
            From = particle.Width,
            To = particle.Width * 1.5,
            Duration = TimeSpan.FromSeconds(0.5),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        
        // Запускаем анимации
        particle.BeginAnimation(Canvas.TopProperty, animation);
        particle.BeginAnimation(Canvas.LeftProperty, horizontalAnimation);
        particle.BeginAnimation(WidthProperty, pulseAnimation);
        particle.BeginAnimation(HeightProperty, pulseAnimation);
        
        // Удаляем частицу после завершения анимации
        animation.Completed += (s, e) =>
        {
            if (ParticlesCanvas.Children.Contains(particle))
            {
                ParticlesCanvas.Children.Remove(particle);
                _particles.Remove(particle);
            }
        };
    }

    private void UpdateParticles()
    {
        // Обновляем цвет частиц в зависимости от текущего трека
        if (CurrentSong != null)
        {
            foreach (var particle in _particles)
            {
                // Можно добавить логику изменения цвета частиц
                // в зависимости от текущего трека или громкости
            }
        }
    }
        
        private async void LoadPlaylist()
        {
            var savedPlaylist = await _playlistDb.LoadPlaylistAsync();
            foreach (var song in savedPlaylist)
            {
                Playlist.Add(song);
            }
            UpdateSongIndexes();
        }

        private void SavePlaylist()
        {
            _playlistDb.SavePlaylist(Playlist.ToList());
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
            var startIndex = Playlist.Count;
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio Files|*.mp3;*.wav;*.wma;*.aac"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    var tagFile = TagLib.File.Create(fileName);
                    var song = new SongItem
                    {
                        Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(fileName),
                        Artist = tagFile.Tag.FirstPerformer ?? "Unknown Artist",
                        Duration = tagFile.Properties.Duration.ToString(@"mm\:ss"),
                        FilePath = fileName
                    };

                    Playlist.Add(song);
                }
            }
            // Обновляем индексы после добавления
            for (int i = 0; i < Playlist.Count; i++)
            {
                Playlist[i].Index = i + 1; // Индексация с 1
            }
            UpdateSongIndexes();
            SavePlaylist(); // Автосохранение после добавления
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
            if (Playlist.Count == 0) return;
    
            var newIndex = Playlist.IndexOf(CurrentSong) + 1;
            if (newIndex >= Playlist.Count) newIndex = 0;
    
            CurrentSong = Playlist[newIndex];
            PlaylistView.SelectedIndex = newIndex;
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