using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
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

                // Создаем таблицу без столбца Artist (если не существует)
                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS Songs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                FilePath TEXT NOT NULL UNIQUE,
                Duration TEXT,
                PlaylistIndex INTEGER NOT NULL
            )";
                createTableCommand.ExecuteNonQuery();

                // Проверяем наличие столбца Artist
                var checkColumnCommand = connection.CreateCommand();
                checkColumnCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Songs') WHERE name = 'Artist';";
                int columnExists = Convert.ToInt32(checkColumnCommand.ExecuteScalar());

                if (columnExists == 0)
                {
                    // Добавляем столбец Artist, если он отсутствует
                    var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Songs ADD COLUMN Artist TEXT NOT NULL DEFAULT '';";
                    alterCommand.ExecuteNonQuery();
                }
            }
        }
        
        

        // Синхронный метод для сохранения плейлиста (ObservableCollection)
        public void SavePlaylist(IEnumerable<SongItem> playlist)
    {
        var items = playlist.ToList(); // Материализуем коллекцию
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Оптимизированный запрос для удаления
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = @"
                        DELETE FROM Songs 
                        WHERE FilePath NOT IN (SELECT value FROM json_each($paths))";
                    
                    deleteCommand.Parameters.AddWithValue("$paths", JsonSerializer.Serialize(items.Select(s => s.FilePath)));
                    deleteCommand.ExecuteNonQuery();

                    // Пакетный UPSERT
                    var upsertCommand = connection.CreateCommand();
                    upsertCommand.CommandText = @"
                        INSERT OR REPLACE INTO Songs 
                        (Id, Title, Artist, FilePath, Duration, PlaylistIndex) 
                        VALUES (
                            COALESCE(
                                (SELECT Id FROM Songs WHERE FilePath = $path), 
                                NULL
                            ),
                            $title,
                            $artist,
                            $path,
                            $duration,
                            $index)";

                    // Добавляем параметры
                    var parameters = new[]
                    {
                        ("$title", SqliteType.Text),
                        ("$artist", SqliteType.Text),
                        ("$path", SqliteType.Text),
                        ("$duration", SqliteType.Text),
                        ("$index", SqliteType.Integer)
                    };

                    foreach (var (name, type) in parameters)
                    {
                        upsertCommand.Parameters.Add(name, type);
                    }

                    // Выполняем пакетную вставку
                    foreach (var song in items)
                    {
                        upsertCommand.Parameters["$title"].Value = song.Title ?? "";
                        upsertCommand.Parameters["$artist"].Value = song.Artist ?? "";
                        upsertCommand.Parameters["$path"].Value = song.FilePath;
                        upsertCommand.Parameters["$duration"].Value = song.Duration;
                        upsertCommand.Parameters["$index"].Value = items.IndexOf(song);
                        
                        upsertCommand.ExecuteNonQuery();
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
                command.CommandText = "SELECT Title, Artist, FilePath, Duration, PlaylistIndex FROM Songs ORDER BY PlaylistIndex";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var song = new SongItem
                        {
                            Title = reader.GetString(0),
                            Artist = reader.GetString(1),
                            FilePath = reader.GetString(2),
                            Duration = reader.GetString(3),
                            PlaylistIndex = reader.GetInt32(4)
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
                command.CommandText = @"
            SELECT Title, Artist, FilePath, Duration, PlaylistIndex 
            FROM Songs 
            ORDER BY PlaylistIndex";
        
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        playlist.Add(new SongItem
                        {
                            Title = reader.GetString(0),
                            Artist = reader.GetString(1),
                            FilePath = reader.GetString(2),
                            Duration = reader.GetString(3),
                            PlaylistIndex = reader.GetInt32(4)
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