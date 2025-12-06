using System;
using System.Windows.Threading;
using System.Windows.Media;

namespace Lc_0_Chess.Services
{
    /// <summary>
    /// Таймер шахматной партии (по аналогии с «часы»).
    /// Поддерживает два независимых счётчика времени (игрок / бот), режим паузы, переключение хода,
    /// а также всплывающие события при каждом тике и при истечении времени.
    /// Работает на WPF-DispatcherTimer, поэтому обновляет UI-поток без маршаллирования.
    /// </summary>
    public class ChessTimer
    {
        #region Поля состояния
        /// <summary>Внутренний таймер, тикает каждые 100 мс.</summary>
        private readonly DispatcherTimer _timer;
        /// <summary>Момент последнего тика: нужен, чтобы точно высчитывать прошедшее время даже при лагах UI.</summary>
        private DateTime _lastTickTime;
        /// <summary>Оставшееся время игрока.</summary>
        private TimeSpan _playerTimeLeft;
        /// <summary>Оставшееся время бота.</summary>
        private TimeSpan _botTimeLeft;
        /// <summary>Чья сейчас очередь делать ход.</summary>
        private bool _isPlayerTurn;
        private bool _isEnabled;
        private bool _isRunning;
        /// <summary>Цвет игрока (true – белые); нужен, чтобы корректно детектировать сторону, когда доска перевёрнута.</summary>
        private readonly bool _isPlayerWhite;
        /// <summary>Коллбек, вызывается при каждом изменении времени (для перерисовки UI).</summary>
        private readonly Action _onTimeUpdate;
        /// <summary>Событие «у игрока закончилось время».</summary>
        private readonly Action _onPlayerTimeOut;
        /// <summary>Событие «у движка (бота) закончилось время».</summary>
        private readonly Action _onBotTimeOut;
        /// <summary>Сервис воспроизведения звуков; используется для звукового предупреждения, когда остаётся &lt;10 сек.</summary>
        private readonly AudioService _audioService;
        #endregion

        public TimeSpan PlayerTimeLeft => _playerTimeLeft;
        public TimeSpan BotTimeLeft => _botTimeLeft;
        public bool IsEnabled => _isEnabled;
        public bool IsPlayerTurn => _isPlayerTurn;
        public bool IsRunning => _isRunning;

        public ChessTimer(
            int initialTimeSeconds,
            Action onTimeUpdate,
            Action onPlayerTimeOut,
            Action onBotTimeOut,
            AudioService audioService,
            bool isPlayerWhite)
        {
            _playerTimeLeft = TimeSpan.FromSeconds(initialTimeSeconds);
            _botTimeLeft = TimeSpan.FromSeconds(initialTimeSeconds);
            _onTimeUpdate = onTimeUpdate;
            _onPlayerTimeOut = onPlayerTimeOut;
            _onBotTimeOut = onBotTimeOut;
            _audioService = audioService;
            _isPlayerWhite = isPlayerWhite;
            _isPlayerTurn = true;
            _isRunning = false;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isEnabled || !_isRunning) return;

            var now = DateTime.Now;
            var elapsed = now - _lastTickTime;
            _lastTickTime = now;

            bool isWhiteTurn = _isPlayerTurn;
            bool shouldUpdatePlayerTime = (isWhiteTurn && _isPlayerWhite) || (!isWhiteTurn && !_isPlayerWhite);

            if (shouldUpdatePlayerTime)
            {
                _playerTimeLeft = _playerTimeLeft.Subtract(elapsed);
                if (_playerTimeLeft <= TimeSpan.Zero)
                {
                    _playerTimeLeft = TimeSpan.Zero;
                    _isEnabled = false;
                    _isRunning = false;
                    _timer.Stop();
                    _onPlayerTimeOut?.Invoke();
                    return;
                }

                if (_playerTimeLeft.TotalSeconds <= 10 && elapsed.Milliseconds < 100)
                {
                    _audioService?.PlayMoveSound();
                }
            }
            else
            {
                _botTimeLeft = _botTimeLeft.Subtract(elapsed);
                if (_botTimeLeft <= TimeSpan.Zero)
                {
                    _botTimeLeft = TimeSpan.Zero;
                    _isEnabled = false;
                    _isRunning = false;
                    _timer.Stop();
                    _onBotTimeOut?.Invoke();
                    return;
                }

                if (_botTimeLeft.TotalSeconds <= 10 && elapsed.Milliseconds < 100)
                {
                    _audioService?.PlayMoveSound();
                }
            }

            _onTimeUpdate?.Invoke();
        }

        #region Public API (управление таймером)

        public void Start()
        {
            if (!_isEnabled) return;
            _lastTickTime = DateTime.Now;
            _isRunning = true;
            _timer.Start();
            _onTimeUpdate?.Invoke();
        }

        public void Stop()
        {
            _isRunning = false;
            _timer.Stop();
            _onTimeUpdate?.Invoke();
        }

        public void Reset(int timeSeconds)
        {
            _playerTimeLeft = TimeSpan.FromSeconds(timeSeconds);
            _botTimeLeft = TimeSpan.FromSeconds(timeSeconds);
            _isEnabled = true;
            _isRunning = false;
            _lastTickTime = DateTime.Now;
            _timer.Stop();
            _onTimeUpdate?.Invoke();
        }

        public void SwitchTurn()
        {
            Stop();
            _isPlayerTurn = !_isPlayerTurn;
            _lastTickTime = DateTime.Now;
            _onTimeUpdate?.Invoke();
        }

        public void Enable()
        {
            _isEnabled = true;
            _lastTickTime = DateTime.Now;
            _onTimeUpdate?.Invoke();
        }

        public void Disable()
        {
            _isEnabled = false;
            _isRunning = false;
            Stop();
        }

        #endregion // Public API

        #region Форматирование времени / выбор цвета

        public string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        public string FormatMilliseconds(TimeSpan time)
        {
            return $".{(time.Milliseconds / 10):D2}";
        }

        public Brush GetTimeColor(TimeSpan timeLeft)
        {
            if (timeLeft.TotalMinutes > 1)
                return Brushes.White;
            if (timeLeft.TotalSeconds > 30)
                return Brushes.Yellow;
            return Brushes.Red;
        }

        #endregion // Форматирование времени / выбор цвета
    }
}