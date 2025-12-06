using System;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lc_0_Chess.Services
{
    public class AudioService
    {
        private readonly MediaPlayer _movePlayer;
        private readonly string _soundsPath;
        private bool _isInitialized;
        private const string MoveSoundFile = "MoveSongs.mp3";
        private TaskCompletionSource<bool> _initializationTcs;

        public AudioService()
        {
            try
            {
                _initializationTcs = new TaskCompletionSource<bool>();
                _soundsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds");
                Debug.WriteLine($"[AudioService] Путь к звуковым файлам: {_soundsPath}");

                var soundFile = Path.Combine(_soundsPath, MoveSoundFile);
                Debug.WriteLine($"[AudioService] Проверяем файл звука: {soundFile}");

                if (!File.Exists(soundFile))
                {
                    Debug.WriteLine($"[AudioService] ОШИБКА: Файл не найден: {soundFile}");
                    throw new FileNotFoundException($"Звуковой файл не найден: {soundFile}");
                }

                var fileInfo = new FileInfo(soundFile);
                Debug.WriteLine($"[AudioService] Размер файла: {fileInfo.Length} байт");

                _movePlayer = new MediaPlayer();
                _movePlayer.MediaFailed += (s, e) =>
                {
                    Debug.WriteLine($"[AudioService] ОШИБКА медиа: {e.ErrorException?.Message}");
                    Debug.WriteLine($"[AudioService] Stack Trace: {e.ErrorException?.StackTrace}");
                    _isInitialized = false;
                    _initializationTcs.TrySetResult(false);
                };
                _movePlayer.MediaOpened += (s, e) =>
                {
                    Debug.WriteLine("[AudioService] Медиафайл успешно открыт");
                    _isInitialized = true;
                    _initializationTcs.TrySetResult(true);
                };

                Debug.WriteLine("[AudioService] Открываем звуковой файл...");
                _movePlayer.Open(new Uri(soundFile, UriKind.Absolute));
                _movePlayer.Volume = 1.0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioService] ОШИБКА инициализации: {ex.Message}");
                Debug.WriteLine($"[AudioService] Stack Trace: {ex.StackTrace}");
                _isInitialized = false;
                _initializationTcs?.TrySetResult(false);
                throw;
            }
        }

        public async Task<bool> WaitForInitializationAsync(int timeoutMs = 5000)
        {
            if (_initializationTcs == null) return false;

            try
            {
                var timeoutTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(_initializationTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Debug.WriteLine("[AudioService] Таймаут инициализации");
                    return false;
                }

                return await _initializationTcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioService] Ошибка при ожидании инициализации: {ex.Message}");
                return false;
            }
        }

        public void PlayMoveSound()
        {
            if (!_isInitialized)
            {
                Debug.WriteLine("[AudioService] ОШИБКА: Попытка воспроизвести звук до инициализации");
                return;
            }

            try
            {
                Debug.WriteLine("[AudioService] Начинаем воспроизведение звука");
                _movePlayer.Position = TimeSpan.Zero;
                _movePlayer.Play();
                Debug.WriteLine("[AudioService] Звук запущен");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioService] ОШИБКА воспроизведения: {ex.Message}");
                Debug.WriteLine($"[AudioService] Stack Trace: {ex.StackTrace}");
            }
        }
    }
}