using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using MyMp3Player.Commands;
using MyMp3Player.Data;


namespace MyMp3Player
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Slider progressSlider;
        private TextBlock currentTimeText;
        private TextBlock totalTimeText;
        
        
        private readonly MediaPlayer mediaPlayer;
        private int currentSongIndex = -1;
        private bool isPlaying = false;
        private bool isDraggingSlider = false;
        private readonly DispatcherTimer timer;
        private double volumeBeforeMute = 1.0;
        private Button? playButton;
        private Slider? volumeSlider;
        private readonly PlaylistDatabase _database;
        
        //для перетаскивания песенок
        private SongItem draggedItem;
        private int dragSourceIndex;
        private bool isDragging = false;
        
        //для перемешивания/повторения треков
        
        private PlaybackMode currentPlaybackMode = PlaybackMode.Normal;
        private Button? repeatButton;
        private Button? shuffleButton;
        private Random random = new Random();
        
        // Метод для инициализации кнопок режимов воспроизведения
        private void InitializePlaybackModeButtons()
        {
            // Добавляем обработчики событий для кнопок
            RepeatButton.Click += RepeatButton_Click;
            ShuffleButton.Click += ShuffleButton_Click;
    
            // Обновляем внешний вид кнопок
            UpdateRepeatButtonAppearance();
            UpdateShuffleButtonAppearance();
        }
        
        private void InitializeMediaPlayer()
        {
            if (mediaPlayer == null)
            {
                throw new InvalidOperationException("MediaPlayer не инициализирован!");
            }

            mediaPlayer.MediaOpened += (sender, e) => 
            {
                Dispatcher.Invoke(() =>
                {
                    // Добавляем проверку на NaturalDuration
                    if (mediaPlayer.NaturalDuration.HasTimeSpan)
                    {
                        ProgressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                        TotalTimeText.Text = mediaPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
                    }
                });
            };

            mediaPlayer.MediaEnded += (sender, e) => 
            {
                Dispatcher.Invoke(() => 
                {
                    // Безопасный вызов PlayNextSong
                    if (playlist.Any()) PlayNextSong();
                });
            };
        }
        
        private void UpdatePlaybackTimer()
        {
            timer.Tick += (sender, e) =>
            {
                if (mediaPlayer.NaturalDuration.HasTimeSpan && !isDraggingSlider)
                {
                    ProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
                    CurrentTimeText.Text = mediaPlayer.Position.ToString(@"mm\:ss");
                }
            };
        }
        
        
        

        // Обработчик нажатия на кнопку повтора
        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // Циклическое переключение между режимами Normal -> Repeat -> RepeatAll -> Normal
            switch (currentPlaybackMode)
            {
                case PlaybackMode.Normal:
                    currentPlaybackMode = PlaybackMode.Repeat;
                    break;
                case PlaybackMode.Repeat:
                    currentPlaybackMode = PlaybackMode.RepeatAll;
                    break;
                case PlaybackMode.RepeatAll:
                    currentPlaybackMode = PlaybackMode.Normal;
                    break;
                case PlaybackMode.Shuffle:
                    // Если был включен режим Shuffle, переключаемся на Repeat и выключаем Shuffle
                    currentPlaybackMode = PlaybackMode.Repeat;
                    break;
            }

            UpdateRepeatButtonAppearance();
            UpdateShuffleButtonAppearance();
        }

        // Обработчик нажатия на кнопку случайного воспроизведения
        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            // Переключение между режимами Shuffle и Normal
            if (currentPlaybackMode == PlaybackMode.Shuffle)
            {
                currentPlaybackMode = PlaybackMode.Normal;
            }
            else
            {
                currentPlaybackMode = PlaybackMode.Shuffle;
            }

            UpdateRepeatButtonAppearance();
            UpdateShuffleButtonAppearance();
        }

        // Обновление внешнего вида кнопки повтора
        private void UpdateRepeatButtonAppearance()
        {
            // Сбрасываем стиль кнопки
            RepeatButton.Background = new SolidColorBrush(Colors.Transparent);
            RepeatButton.Foreground = new SolidColorBrush(Colors.White);

            // Устанавливаем соответствующий текст или иконку в зависимости от режима
            switch (currentPlaybackMode)
            {
                case PlaybackMode.Repeat:
                    RepeatButton.Content = "🔂"; // Иконка повтора одного трека
                    RepeatButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    break;
                case PlaybackMode.RepeatAll:
                    RepeatButton.Content = "🔁"; // Иконка повтора всего плейлиста
                    RepeatButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    break;
                default:
                    RepeatButton.Content = "🔁"; // Иконка повтора (неактивная)
                    break;
            }
        }

        // Обновление внешнего вида кнопки случайного воспроизведения
        private void UpdateShuffleButtonAppearance()
        {
            // Сбрасываем стиль кнопки
            ShuffleButton.Background = new SolidColorBrush(Colors.Transparent);
            ShuffleButton.Foreground = new SolidColorBrush(Colors.White);

            // Если режим перемешивания активен, выделяем кнопку
            if (currentPlaybackMode == PlaybackMode.Shuffle)
            {
                ShuffleButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            }
        }
        
        // Перечисление для режимов воспроизведения
        public enum PlaybackMode
        {
            Normal,     // Обычное воспроизведение
            Repeat,     // Повтор текущего трека
            RepeatAll,  // Повтор всего плейлиста
            Shuffle     // Случайное воспроизведение
        }
        
        // Метод для инициализации функции перетаскивания
        private void InitializeDragDrop()
        {
            // Настраиваем ListView для поддержки перетаскивания
            PlaylistView.AllowDrop = true;
            
            // Подписываемся на события перетаскивания
            PlaylistView.PreviewMouseLeftButtonDown += PlaylistView_PreviewMouseLeftButtonDown;
            PlaylistView.PreviewMouseMove += PlaylistView_PreviewMouseMove;
            PlaylistView.Drop += PlaylistView_Drop;
            PlaylistView.DragEnter += PlaylistView_DragEnter;
            PlaylistView.DragOver += PlaylistView_DragOver;
        }
        
        private void PlaylistView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Сохраняем начальную точку для определения минимального расстояния перетаскивания
            startPoint = e.GetPosition(null);
            
            // Проверяем, является ли источник события кнопкой удаления
            if (IsDeleteButton(e.OriginalSource))
            {
                return; // Пропускаем обработку перетаскивания для кнопки удаления
            }
    
            // Проверяем, не нажата ли кнопка с тремя точками или другая кнопка
            var originalSource = e.OriginalSource as DependencyObject;
    
            // Проверяем, является ли источник события кнопкой или её потомком
            while (originalSource != null && !(originalSource is Button) && !(originalSource is ListViewItem))
            {
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }
    
            // Если источник - кнопка, не начинаем перетаскивание
            if (originalSource is Button)
            {
                return;
            }
            
            startPoint = e.GetPosition(null);
            // Получаем элемент, на котором произошло нажатие
            var item = GetItemFromPoint(e.GetPosition(PlaylistView));
            if (item != null)
            {
                // Сохраняем информацию о перетаскиваемом элементе
                draggedItem = item.Content as SongItem;
                dragSourceIndex = playlist.IndexOf(draggedItem);
            }
        }
        
        private bool IsDeleteButton(object source)
        {
            var dependencyObject = source as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is Button button && button.Name == "DeleteButton")
                {
                    return true;
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }
            return false;
        }
        
        // Вспомогательный метод для получения ListViewItem из координат мыши
        private ListViewItem GetItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(PlaylistView, point);
            if (result == null)
                return null;

            DependencyObject obj = result.VisualHit;
            while (obj != null && !(obj is ListViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            return obj as ListViewItem;
        }

        
        private Point startPoint;
        
        // Обработчик движения мыши (перетаскивание)
        private void PlaylistView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && draggedItem != null && !isDragging)
            {
                // Получаем текущую позицию мыши
                Point mousePos = e.GetPosition(null);
                Vector diff = startPoint - mousePos;

                // Начинаем перетаскивание только если мышь переместилась на достаточное расстояние
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Начинаем операцию перетаскивания
                    isDragging = true;
            
                    // Создаем данные для перетаскивания
                    DataObject dragData = new DataObject("SongItem", draggedItem);
            
                    // Запускаем операцию перетаскивания
                    DragDrop.DoDragDrop(PlaylistView, dragData, DragDropEffects.Move);
            
                    // Сбрасываем состояние перетаскивания
                    isDragging = false;
                    draggedItem = null;
                }
            }
        }

        // Обработчик события DragEnter
        private void PlaylistView_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("SongItem") || e.Source != sender)
            {
                e.Effects = DragDropEffects.None;
            }
        }

        // Обработчик события DragOver
        private void PlaylistView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("SongItem") || e.Source != sender)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // Показываем визуальный индикатор места вставки
            e.Effects = DragDropEffects.Move;
        }
        
        // Обработчик события Drop (завершение перетаскивания)
        private void PlaylistView_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("SongItem") || e.Source != sender)
            {
                return;
            }

            // Получаем элемент, над которым произошло событие Drop
            var targetItem = GetItemFromPoint(e.GetPosition(PlaylistView));
            if (targetItem != null)
            {
                // Получаем индекс целевого элемента
                var targetSong = targetItem.Content as SongItem;
                int targetIndex = playlist.IndexOf(targetSong);

                // Если индексы различаются, перемещаем элемент
                if (dragSourceIndex != targetIndex)
                {
                    // Получаем перетаскиваемый элемент
                    var draggedSong = e.Data.GetData("SongItem") as SongItem;
                    
                    // Удаляем элемент из исходной позиции
                    playlist.Remove(draggedSong);
                    
                    // Вставляем элемент в новую позицию
                    playlist.Insert(targetIndex, draggedSong);
                    
                    // Обновляем индексы всех элементов
                    UpdatePlaylistIndices();
                    
                    // Если перемещаемый трек был текущим воспроизводимым, обновляем индекс
                    if (currentSongIndex == dragSourceIndex)
                    {
                        currentSongIndex = targetIndex;
                    }
                    // Если перемещение произошло между текущим треком и началом списка
                    else if (dragSourceIndex < currentSongIndex && targetIndex >= currentSongIndex)
                    {
                        currentSongIndex--;
                    }
                    // Если перемещение произошло между текущим треком и концом списка
                    else if (dragSourceIndex > currentSongIndex && targetIndex <= currentSongIndex)
                    {
                        currentSongIndex++;
                    }
                    
                    // Сохраняем обновленный плейлист
                    SavePlaylistToDatabase();
                }
            }
        }
        
        public ObservableCollection<SongItem> playlist { get; } = new ObservableCollection<SongItem>();
        
        
        public ObservableCollection<SongItem> PlaylistItems { get; } = new ObservableCollection<SongItem>();
        private int currentPlayingIndex = -1;

        public event PropertyChangedEventHandler? PropertyChanged;

        
        public MainWindow()
        {
            InitializeComponent();
            
            mediaPlayer = new MediaPlayer();
            // Инициализация базы данных и медиаплеера
            _database = new PlaylistDatabase();
            playlist = new ObservableCollection<SongItem>();
            PlaylistView.ItemsSource = playlist;
            
            // Инициализация таймера
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            timer.Tick += Timer_Tick;
            
            
            InitializeMediaPlayer(); 
            UpdatePlaybackTimer();
            
            progressSlider = (Slider)FindName("ProgressSlider");
            currentTimeText = (TextBlock)FindName("CurrentTimeText");
            totalTimeText = (TextBlock)FindName("TotalTimeText");
            
            // Инициализация коллекции перед загрузкой данных
            PlaylistView.ItemsSource = PlaylistItems; // Привязка к коллекции
            
            
            
            LoadPlaylistFromDatabase();
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.Volume = 1.0;
            
            

            // Инициализация элементов управления
            InitializeControls();
            
            // Загрузка плейлиста после инициализации всех компонентов
            InitializePlaylist();
            
            // Настраиваем обработчик закрытия окна
            Closing += MainWindow_Closing;
            InitializeDragDrop();
        }
        

        // Метод для загрузки плейлиста из базы данных
        private void LoadPlaylistFromDatabase()
        {
            try
            {
                var loadedPlaylist = _database.LoadPlaylist();
                
                playlist.Clear();
                foreach (var song in loadedPlaylist)
                {
                    song.DeleteRequested += Song_DeleteRequested;
                    playlist.Add(song);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке плейлиста: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        
        
        private string GetSongDuration(string filePath)
        {
            try
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    TimeSpan duration = file.Properties.Duration;
                    return duration.Hours > 0 
                        ? $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}" 
                        : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
                }
            }
            catch (Exception)
            {
                return "00:00";
            }
        }
        
        // Инициализация плейлиста
        private void InitializePlaylist()
        {
            // Загрузка плейлиста из базы данных
            LoadPlaylistFromDatabase();
        }
        

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SongItem songItem)
            {
                songItem.IsContextMenuOpen = true;
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SongItem songItem)
            {
                songItem.IsContextMenuOpen = false;
                Song_DeleteRequested(songItem, EventArgs.Empty);
            }
        }
        
        // Обработчик события удаления трека
        private void Song_DeleteRequested(object sender, EventArgs e)
        {
            if (sender is SongItem songItem)
            {
                int indexToRemove = playlist.IndexOf(songItem);
        
                if (indexToRemove == -1) return;

                // Корректировка текущего индекса
                if (indexToRemove == currentSongIndex)
                {
                    StopSong();
                    currentSongIndex = -1;
                }
                else if (currentSongIndex > indexToRemove)
                {
                    currentSongIndex--;
                }

                playlist.RemoveAt(indexToRemove);
                UpdatePlaylistIndices();
                SavePlaylistToDatabase();

                // Автоматическое воспроизведение
                if (playlist.Count > 0)
                {
                    int newIndex = currentSongIndex == -1 ? 0 
                        : Math.Clamp(currentSongIndex, 0, playlist.Count - 1);
                    PlaySongAtIndex(newIndex);
                }
            }
        }

        
        // Метод для обновления индексов треков в плейлисте
        private void UpdatePlaylistIndices()
        {
            for (int i = 0; i < playlist.Count; i++)
            {
                playlist[i].Index = i + 1;
            }
        }

        
        
        private void InitializeControls()
        {
            volumeSlider = FindName("VolumeSlider") as Slider;
            if (volumeSlider != null)
            {
                volumeSlider.Minimum = 0;
                volumeSlider.Maximum = 100;
                volumeSlider.Value = mediaPlayer.Volume * 100;
                volumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            }

            playButton = FindName("PlayButton") as Button;
            if (playButton == null)
            {
                foreach (var element in LogicalTreeHelper.GetChildren(this))
                {
                    if (element is Button button && button.Style != null && 
                        button.Style.ToString().Contains("MainPlayerButton"))
                    {
                        playButton = button;
                        break;
                    }
                }
            }

            var volumeButton = FindName("VolumeButton") as Button;
            if (volumeButton != null)
            {
                volumeButton.Click += VolumeButton_Click;
                UpdateVolumeButtonIcon();
            }
        }
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        

        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Stop();
                mediaPlayer.Close();
            }

            try
            {
                await _database.SavePlaylistAsync(playlist);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении плейлиста: {ex.Message}");
            }
        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Minimum = 0;
                ProgressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                ProgressSlider.Value = 0;
                
                TotalTimeText.Text = FormatTimeSpan(mediaPlayer.NaturalDuration.TimeSpan);
                CurrentTimeText.Text = "00:00";
                
                if (currentSongIndex >= 0 && currentSongIndex < playlist.Count)
                {
                    CurrentSongTitle.Text = playlist[currentSongIndex].Title;
                    playlist[currentSongIndex].Duration = FormatTimeSpan(mediaPlayer.NaturalDuration.TimeSpan);
                    PlaylistView.Items.Refresh();
                }
            }
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            // Воспроизводим следующий трек с учетом текущего режима воспроизведения
            PlayNextSong();
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!isDraggingSlider && mediaPlayer.Source != null && 
                    mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    ProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
                    CurrentTimeText.Text = FormatTimeSpan(mediaPlayer.Position);
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки и остановка таймера
                Debug.WriteLine($"Ошибка в таймере: {ex}");
                timer.Stop();
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MP3 Files (*.mp3)|*.mp3|All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    AddSongToPlaylist(filename);
                }
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] files = Directory.GetFiles(dialog.SelectedPath, "*.mp3", SearchOption.AllDirectories);
                
                foreach (string file in files)
                {
                    AddSongToPlaylist(file);
                }
            }
        }

        // Метод для добавления нового трека в плейлист
        private void AddSongToPlaylist(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                using (var file = TagLib.File.Create(filePath))
                {
                    var songItem = new SongItem
                    {
                        Index = playlist.Count + 1,
                        Title = string.IsNullOrEmpty(file.Tag.Title) 
                            ? Path.GetFileNameWithoutExtension(filePath) 
                            : file.Tag.Title,
                        Artist = string.IsNullOrEmpty(file.Tag.FirstPerformer) 
                            ? "Неизвестный исполнитель" 
                            : file.Tag.FirstPerformer,
                        FilePath = filePath,
                        Duration = file.Properties.Duration.ToString(@"mm\:ss")
                    };

                    songItem.DeleteRequested += Song_DeleteRequested;
                    playlist.Add(songItem);
                    SavePlaylistToDatabase();
            
                    // Автоматическое воспроизведение первого добавленного трека
                    if (playlist.Count == 1) PlaySongAtIndex(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления трека: {ex.Message}");
            }
        }
        
        // Метод для сохранения плейлиста в базу данных
        private void SavePlaylistToDatabase()
        {
            try
            {
                _database.SavePlaylist(playlist.ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении плейлиста: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSong(int index)
        {
            // Полная проверка индекса
            if (index < 0 || index >= playlist.Count) return;

            try
            {
                mediaPlayer.Stop();
                var uri = new Uri(playlist[index].FilePath);
                mediaPlayer.Open(uri);
                currentSongIndex = index;
        
                // Проверка на null перед установкой свойств
                if (CurrentSongTitle != null) 
                    CurrentSongTitle.Text = playlist[index].Title;
            
                if (CurrentArtist != null) 
                    CurrentArtist.Text = string.IsNullOrEmpty(playlist[index].Artist) 
                        ? "Неизвестный исполнитель" 
                        : playlist[index].Artist;
        
                ProgressSlider.Value = 0;
                CurrentTimeText.Text = "00:00";
        
                UpdatePlayButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки трека: {ex.Message}");
                PlayNextSong(); // Пропускаем битый трек
            }
        }

        private void PlaySong()
        {
            if (currentSongIndex >= 0 && currentSongIndex < playlist.Count)
            {
                mediaPlayer.Play();
                isPlaying = true;
                timer.Start();
                UpdatePlayButtonState();
            }
        }

        private void PauseSong()
        {
            if (isPlaying)
            {
                mediaPlayer.Pause();
                isPlaying = false;
                timer.Stop();
                UpdatePlayButtonState();
            }
        }

        private void StopSong()
        {
            mediaPlayer.Stop();
            isPlaying = false;
            timer.Stop();
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
            UpdatePlayButtonState();
        }

        private void UpdatePlayButtonState()
        {
            Dispatcher.Invoke(() =>
            {
                if (PlayButton != null)
                {
                    PlayButton.Content = isPlaying ? "⏸" : "▶";
                }
            });
        }

        private void PlayNextSong()
        {
            try
            {
                if (playlist.Count == 0)
                {
                    StopSong();
                    return;
                }

                int nextIndex = currentSongIndex;

                switch (currentPlaybackMode)
                {
                    case PlaybackMode.Normal:
                    case PlaybackMode.RepeatAll:
                        nextIndex = GetNextValidIndex(currentSongIndex + 1);
                        break;
            
                    case PlaybackMode.Repeat:
                        // Остаемся на текущем треке
                        break;
            
                    case PlaybackMode.Shuffle:
                        nextIndex = GetRandomIndex(excludeCurrent: true);
                        break;
                }

                PlaySongAtIndex(nextIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка переключения трека: {ex.Message}");
            }
        }
        
        private int GetNextValidIndex(int proposedIndex)
        {
            if (playlist.Count == 0) return -1;
            return (proposedIndex % playlist.Count + playlist.Count) % playlist.Count;
        }


        private void PlayPreviousSong()
        {
            if (playlist.Count == 0) return;

            int prevIndex;

            switch (currentPlaybackMode)
            {
                case PlaybackMode.Normal:
                case PlaybackMode.RepeatAll:
                    // Обычное последовательное воспроизведение в обратном порядке
                    prevIndex = (currentSongIndex - 1 + playlist.Count) % playlist.Count;
                    break;

                case PlaybackMode.Repeat:
                    // Повтор текущего трека
                    prevIndex = currentSongIndex;
                    break;

                case PlaybackMode.Shuffle:
                    // Случайное воспроизведение
                    if (playlist.Count == 1)
                    {
                        prevIndex = 0;
                    }
                    else
                    {
                        // Выбираем случайный индекс, отличный от текущего
                        do
                        {
                            prevIndex = random.Next(playlist.Count);
                        } while (prevIndex == currentSongIndex && playlist.Count > 1);
                    }
                    break;

                default:
                    prevIndex = (currentSongIndex - 1 + playlist.Count) % playlist.Count;
                    break;
            }

            PlaySongAtIndex(prevIndex);
            HighlightCurrentSong();
        }
        
        private void PlaySongAtIndex(int index)
        {
            try
            {
                if (playlist == null || playlist.Count == 0)
                {
                    StopSong();
                    currentSongIndex = -1;
                    return;
                }

                // Нормализация индекса
                index = Math.Clamp(index, 0, playlist.Count - 1);

                currentSongIndex = index;
                LoadSong(currentSongIndex);
                PlaySong();

                Dispatcher.Invoke(() =>
                {
                    PlaylistView.SelectedIndex = currentSongIndex;
                    PlaylistView.ScrollIntoView(PlaylistView.Items[currentSongIndex]);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка воспроизведения: {ex.Message}");
                PlayNextSong();
            }
        }
        
        // Вспомогательный метод для выделения текущего трека в списке
        private void HighlightCurrentSong()
        {
            // Сначала сбрасываем выделение для всех элементов
            foreach (var item in playlist)
            {
                item.IsPlaying = false;
            }

            // Затем выделяем текущий трек
            if (currentSongIndex >= 0 && currentSongIndex < playlist.Count)
            {
                playlist[currentSongIndex].IsPlaying = true;
            }

            // Принудительно обновляем UI для каждого элемента
            // Это гарантирует, что изменения свойства IsPlaying будут отражены в интерфейсе
            foreach (var item in playlist)
            {
                // Вызываем PropertyChanged вручную для каждого элемента
                if (item is INotifyPropertyChanged notifyItem)
                {
                    var method = notifyItem.GetType().GetMethod("OnPropertyChanged", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
            
                    if (method != null)
                    {
                        method.Invoke(item, new object[] { nameof(SongItem.IsPlaying) });
                    }
                }
            }

            // Обновляем отображение списка
            PlaylistView.Items.Refresh();

            // Прокручиваем список к текущему треку
            if (PlaylistView.Items.Count > 0 && currentSongIndex >= 0 && currentSongIndex < PlaylistView.Items.Count)
            {
                PlaylistView.ScrollIntoView(PlaylistView.Items[currentSongIndex]);
            }
        }
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (playlist.Count == 0) return;
        
                if (currentSongIndex == -1) // Если трек не выбран
                {
                    PlaySongAtIndex(0); // Начинаем с первого трека
                }
                else
                {
                    if (isPlaying)
                    {
                        PauseSong();
                    }
                    else
                    {
                        PlaySong();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка управления воспроизведением: {ex.Message}");
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            PauseSong();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopSong();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0) return;

            int newIndex = currentSongIndex;

            if (currentPlaybackMode == PlaybackMode.Shuffle)
            {
                newIndex = GetRandomIndex(excludeCurrent: true);
            }
            else
            {
                newIndex = (currentSongIndex + 1) % playlist.Count;
            }

            // Добавляем проверку
            if (newIndex < 0 || newIndex >= playlist.Count)
            {
                newIndex = 0;
            }

            PlaySongAtIndex(newIndex);
    
            Dispatcher.Invoke(() =>
            {
                PlaylistView.UnselectAll();
                if (newIndex >= 0 && newIndex < playlist.Count)
                {
                    PlaylistView.SelectedIndex = newIndex;
                }
            });
        }
        private int GetRandomIndex(bool excludeCurrent = false)
        {
            if (playlist.Count == 0) return -1;
            if (playlist.Count == 1) return excludeCurrent ? -1 : 0;

            int index;
            int attempts = 0;
            do
            {
                index = random.Next(playlist.Count);
                attempts++;
            } 
            while (excludeCurrent && index == currentSongIndex && attempts < 100);

            return index >= 0 && index < playlist.Count ? index : 0;
        }
        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (currentSongIndex > 0)
            {
                PlaySongAtIndex(currentSongIndex - 1);
            }
            else
            {
                PlaySongAtIndex(playlist.Count - 1);
            }
    
            // Явно обновляем выделение
            PlaylistView.UnselectAll();
            PlaylistView.SelectedIndex = currentSongIndex;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Обработчики событий UI
        private void PlaylistView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Добавляем проверку на допустимость индекса
            if (PlaylistView.SelectedIndex >= 0 && PlaylistView.SelectedIndex < playlist.Count)
            {
                LoadSong(PlaylistView.SelectedIndex);
                if (isPlaying)
                {
                    PlaySong();
                }
            }
            else
            {
                PlaylistView.SelectedIndex = -1; // Сбрасываем невалидный выбор
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDraggingSlider && mediaPlayer.Source != null)
            {
                CurrentTimeText.Text = FormatTimeSpan(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = true;
            timer.Stop();
        }

        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
        }
        
       

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = e.NewValue / 100.0;
                UpdateVolumeButtonIcon();
            }
        }

        private void UpdateVolumeButtonIcon()
        {
            if (FindName("VolumeButton") is Button volumeButton)
            {
                volumeButton.Content = mediaPlayer.Volume switch
                {
                    0 => "🔇",
                    < 0.3 => "🔈",
                    < 0.7 => "🔉",
                    _ => "🔊"
                };
            }
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.Volume > 0)
            {
                volumeBeforeMute = mediaPlayer.Volume;
                mediaPlayer.Volume = 0;
                if (volumeSlider != null)
                {
                    volumeSlider.Value = 0;
                }
            }
            else
            {
                mediaPlayer.Volume = volumeBeforeMute;
                if (volumeSlider != null)
                {
                    volumeSlider.Value = volumeBeforeMute * 100;
                }
            }
            
            UpdateVolumeButtonIcon();
        }
    }
}