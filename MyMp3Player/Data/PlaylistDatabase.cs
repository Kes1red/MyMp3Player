using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace MyMp3Player.Data
{
    public class PlaylistDatabase
    {
        private readonly string _dbPath;
        private const string DbFileName = "playlist.db";

        public PlaylistDatabase()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Songs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        FilePath TEXT NOT NULL UNIQUE,
                        Duration TEXT,
                        PlaylistIndex INTEGER NOT NULL
                    )";
                command.ExecuteNonQuery();
            }
        }
        
        

        // Синхронный метод для сохранения плейлиста (ObservableCollection)
        public void SavePlaylist(IEnumerable<SongItem> playlist)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                var transaction = connection.BeginTransaction();
            
                try
                {
                    // Очистка старого плейлиста
                    var clearCommand = connection.CreateCommand();
                    clearCommand.CommandText = "DELETE FROM Songs";
                    clearCommand.ExecuteNonQuery();

                    // Вставка новых записей
                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = @"INSERT INTO Songs 
                    (Title, FilePath, Duration, PlaylistIndex) 
                    VALUES ($title, $path, $duration, $index)";

                    int index = 1;
                    foreach (var song in playlist)
                    {
                        insertCommand.Parameters.Clear();
                        insertCommand.Parameters.AddWithValue("$title", song.Title);
                        insertCommand.Parameters.AddWithValue("$path", song.FilePath);
                        insertCommand.Parameters.AddWithValue("$duration", song.Duration);
                        insertCommand.Parameters.AddWithValue("$index", index++);
                        insertCommand.ExecuteNonQuery();
                    }
                
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // Перегрузка метода для работы с List<SongItem>
        public void SavePlaylist(List<SongItem> playlist)
        {
            // Преобразуем List в ObservableCollection и вызываем основной метод
            var observablePlaylist = new ObservableCollection<SongItem>(playlist);
            SavePlaylist(observablePlaylist);
        }

        // Асинхронный метод для сохранения плейлиста (ObservableCollection)
        public async Task SavePlaylistAsync(ObservableCollection<SongItem> playlist)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();

                // Начинаем транзакцию для обеспечения целостности данных
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Очищаем существующие записи
                        using (var clearCommand = connection.CreateCommand())
                        {
                            clearCommand.CommandText = "DELETE FROM Songs";
                            await clearCommand.ExecuteNonQueryAsync();
                        }

                        // Добавляем новые записи
                        for (int i = 0; i < playlist.Count; i++)
                        {
                            using (var insertCommand = connection.CreateCommand())
                            {
                                insertCommand.CommandText = @"
                                    INSERT INTO Songs (Title, FilePath, Duration, PlaylistIndex)
                                    VALUES ($title, $filePath, $duration, $index)";

                                insertCommand.Parameters.AddWithValue("$title", playlist[i].Title);
                                insertCommand.Parameters.AddWithValue("$filePath", playlist[i].FilePath);
                                insertCommand.Parameters.AddWithValue("$duration", playlist[i].Duration);
                                insertCommand.Parameters.AddWithValue("$index", i + 1);

                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }

                        // Подтверждаем транзакцию
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        // В случае ошибки откатываем все изменения
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // Перегрузка асинхронного метода для работы с List<SongItem>
        public async Task SavePlaylistAsync(List<SongItem> playlist)
        {
            // Преобразуем List в ObservableCollection и вызываем основной метод
            var observablePlaylist = new ObservableCollection<SongItem>(playlist);
            await SavePlaylistAsync(observablePlaylist);
        }

        // Синхронный метод для загрузки плейлиста
        public ObservableCollection<SongItem> LoadPlaylist()
        {
            var result = new ObservableCollection<SongItem>();

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT Title, FilePath, Duration, PlaylistIndex FROM Songs ORDER BY PlaylistIndex";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var song = new SongItem
                        {
                            Title = reader.GetString(0),
                            FilePath = reader.GetString(1),
                            Duration = reader.GetString(2),
                            Index = reader.GetInt32(3)
                        };

                        result.Add(song);
                    }
                }
            }

            return result;
        }

        // Асинхронный метод для загрузки плейлиста
        public async Task<ObservableCollection<SongItem>> LoadPlaylistAsync()
        {
            var playlist = new ObservableCollection<SongItem>();
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Title, FilePath, Duration, PlaylistIndex FROM Songs ORDER BY PlaylistIndex";
            
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        playlist.Add(new SongItem
                        {
                            Title = reader.GetString(0),
                            FilePath = reader.GetString(1),
                            Duration = reader.GetString(2),
                            Index = reader.GetInt32(3)
                        });
                    }
                }
            }
            return playlist;
        }

        // Метод для проверки существования файла базы данных
        public bool DatabaseExists()
        {
            return File.Exists(_dbPath);
        }

        // Метод для удаления базы данных (может пригодиться для сброса настроек)
        public void DeleteDatabase()
        {
            if (DatabaseExists())
            {
                File.Delete(_dbPath);
            }
        }
    }
}