// Target file: Lc_0_Chess/Views/MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes; // Для Ellipse
using System.Windows.Media.Animation;
using Lc_0_Chess.Models;
using Lc_0_Chess.Services;
using System.IO;
using Path = System.IO.Path;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Effects;
using System.Threading;
using System.Threading.Tasks;
//using Lc0_Chess.Models; // This using directive should make Lc0Engine available
// using Chess.Models; // Old using for Stockfish project

namespace Lc_0_Chess.Views
{
    public class EvaluationBarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                return width / 2;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Главное окно WPF-приложения.
    /// Отвечает за пользовательский интерфейс, визуализацию шахматной доски,
    /// коммуникацию с моделью (<see cref="Lc_0_Chess.Models.ChessBoard"/>) и движком
    /// (<see cref="Lc_0_Chess.Models.Lc0Engine"/>), а также за таймер, анимации и историю ходов.
    /// В файле сгруппированы логические блоки методов (инициализация, рендеринг, обработка ходов,
    /// работа с историей, анализ позиции), что облегчает навигацию в IDE.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Свойство зависимости для значения оценки
        public static readonly DependencyProperty EvaluationValueProperty =
            DependencyProperty.Register(
                "EvaluationValue",
                typeof(double),
                typeof(MainWindow),
                new PropertyMetadata(0.0, OnEvaluationValueChanged));

        public double EvaluationValue
        {
            get { return (double)GetValue(EvaluationValueProperty); }
            set { SetValue(EvaluationValueProperty, value); }
        }

        private static void OnEvaluationValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow mainWindow)
            {
                mainWindow.UpdateEvaluationBar((double)e.NewValue);
            }
        }

        private void UpdateEvaluationBar(double value)
        {
            if (EvaluationBar == null) return; // если еще не создан

            // Clamp value between -50 and 50
            value = Math.Max(-50, Math.Min(50, value));

            double barWidth = EvaluationBar.ActualWidth;
            if (barWidth <= 0)
            {
                barWidth = EvaluationBar.RenderSize.Width;
            }
            if (barWidth <= 0)
            {
                // попробуем заново после отрисовки
                _ = EvaluationBar.Dispatcher.InvokeAsync(() => UpdateEvaluationBar(value), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            // Percentages
            double blackPercent = 50 - value; // черные слева
            double blackTargetWidth = barWidth * blackPercent / 100.0;
            double whiteTargetWidth = barWidth - blackTargetWidth;

            double currentBlack = BlackRect.Width;
            double currentWhite = WhiteRect.Width;
            if (double.IsNaN(currentBlack) || currentBlack == 0)
            {
                currentBlack = barWidth / 2;
            }
            if (double.IsNaN(currentWhite) || currentWhite == 0)
            {
                currentWhite = barWidth - currentBlack;
            }
            BlackRect.Width = currentBlack;
            WhiteRect.Width = currentWhite;

            var duration = TimeSpan.FromMilliseconds(300);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var blackAnim = new DoubleAnimation
            {
                From = currentBlack,
                To = blackTargetWidth,
                Duration = duration,
                EasingFunction = ease
            };
            var whiteAnim = new DoubleAnimation
            {
                From = currentWhite,
                To = whiteTargetWidth,
                Duration = duration,
                EasingFunction = ease
            };

            BlackRect.BeginAnimation(FrameworkElement.WidthProperty, blackAnim, HandoffBehavior.SnapshotAndReplace);
            WhiteRect.BeginAnimation(FrameworkElement.WidthProperty, whiteAnim, HandoffBehavior.SnapshotAndReplace);

            Log($"[Bar] value={value}  blackWidth={blackTargetWidth:F1}px whiteWidth={whiteTargetWidth:F1}px");

            // Меняем цвет текста на контрастный: если центр находится на белой стороне (value > 0) — текст чёрный, иначе белый
            if (EvaluationText != null)
            {
                EvaluationText.Foreground = value > 0 ? Brushes.Black : Brushes.White;
                EvaluationText.Effect = new DropShadowEffect
                {
                    BlurRadius = 0,
                    ShadowDepth = 0,
                    Color = value > 0 ? Colors.White : Colors.Black,
                    Opacity = 1
                };
            }
        }

        private double _lastEvaluationBarValue = 0; // хранит последнее показанное значение для сглаживания

        // Открытый метод для установки оценки, value передается в сантипешках (centipawns), например +150 = +1.5
        public void SetEvaluationFromCentipawns(int centipawns)
        {
            // 1) Преобразуем сырую оценку движка (centipawns) в диапазон ‑50…+50 с использованием сглаженной логистической функции.
            //    Таким образом даже очень большие значения cp не будут сразу приводить к насыщению шкалы.
            double target;

            // Обрабатываем «матовые» оценки движка, которые обычно приходят как ±10000 и выше
            if (Math.Abs(centipawns) >= 10000)
            {
                target = centipawns > 0 ? 50 : -50;
            }
            else
            {
                // Логистическая аппроксимация: 400 cp ≈ 1 пешка ⇒ ~29 на шкале, 800 cp ⇒ ~46, 1200 cp ⇒ ~49, 1500+ ⇒ ~50
                target = 50 * Math.Tanh(centipawns / 400.0);
            }

            // 2) Знак оставляем «для белых» — полоса всегда показывает преимущество белых/чёрных корректно.

            // 3) Лёгкое экспоненциальное сглаживание, чтобы всплески не дёргали ползунок слишком резко
            const double smoothingFactor = 0.35; // 0 = совсем без обновления, 1 = без сглаживания
            double smoothed = _lastEvaluationBarValue * (1 - smoothingFactor) + target * smoothingFactor;
            _lastEvaluationBarValue = smoothed;

            // 4) Передаём сглаженное значение в бар
            SetEvaluation(smoothed);

            // Обновляем текстовую метку, показывая оценку в пешках (cp/100)
            if (EvaluationText != null)
            {
                // Показываем числовое значение с точки зрения игрока: «+» = наш перевес.
                double pawns = centipawns / 100.0 * (_isPlayerWhite ? 1 : -1);
                EvaluationText.Text = pawns.ToString("+0.0;-0.0;0.0");
            }
        }

        public void SetEvaluation(double value)
        {
            EvaluationValue = value;
        }

        private class MovePair
        {
            public int MoveNumber { get; set; }
            public string WhiteMove { get; set; }
            public string BlackMove { get; set; }
            public int WhiteMoveIndex { get; set; }
            public int BlackMoveIndex { get; set; }
        }

        #region Модель, движок и конфигурация игры
        /// <summary>Экземпляр <see cref="ChessBoard"/>, хранит состояние партии.</summary>
        private readonly ChessBoard _chessBoard;
        /// <summary>Основной движок LC0, делает ходы за соперника-бота.</summary>
        private Lc0Engine _lc0Engine;
        /// <summary>Второй экземпляр движка — используется параллельно для анализа позиции (бар оценки, «план»).</summary>
        private Lc0Engine _analysisEngine; // отдельный движок для аналитики
        /// <summary>Время обдумывания одного хода ботом (мс).</summary>
        private readonly int _thinkingTimeMs;
        /// <summary>Сервис звуковых эффектов (перемещение фигур, предупреждения таймера и т.д.).</summary>
        private AudioService _audioService;
        /// <summary>Выбранный режим (Classic / Chess960). Используется при инициализации доски.</summary>
        private readonly string _gameMode;
        /// <summary>Флаг, показывающий, играет ли пользователь белыми.</summary>
        private readonly bool _isPlayerWhite;
        #endregion

        private bool _isPlayerTurn;
        private bool _isDisposed = false;
        private bool _engineInitialized = false;

        private readonly ChessTimer _chessTimer;
        private readonly int _timeControlMinutes;
        private TimeSpan _playerTimeLeft;
        private TimeSpan _botTimeLeft;
        private bool _isTimerEnabled;

        private TextBlock _playerTimeLabel;
        private TextBlock _botTimeLabel;

        private const string Lc0ExecutablePath = "Lc-0/lc0.exe";
        private const string Lc0WeightsPath = "Lc-0/791556.pb.gz";

        private readonly List<string> _numericLabels = new() { "8", "7", "6", "5", "4", "3", "2", "1" };
        private readonly List<string> _alphabetLabels = new() { "a", "b", "c", "d", "e", "f", "g", "h" };
        private readonly List<string> _reversedAlphabetLabels = new() { "H", "G", "F", "E", "D", "C", "B", "A" };
        private readonly List<string> _reversedNumericLabels = new() { "1", "2", "3", "4", "5", "6", "7", "8" };

        private Position? _selectedPosition; // Позиция выбранной фигуры
        private Border? _selectedCellVisual;   // Визуальное представление выбранной клетки (сделано nullable)

        private Border[,] _cellVisuals = new Border[ChessBoard.Size, ChessBoard.Size];
        private Image[,] _pieceImages = new Image[ChessBoard.Size, ChessBoard.Size];
        private Ellipse[,] _possibleMoveDots = new Ellipse[ChessBoard.Size, ChessBoard.Size];
        private readonly Dictionary<string, BitmapImage> _imageCache = new();
        // Список для хранения клеток, подсвеченных как возможное взятие, чтобы их потом очистить
        private readonly List<Border> _highlightedCaptureCells = new List<Border>();

        // Ссылки на клетки предыдущего хода, чтобы можно было снять подсветку при следующем
        private Border? _lastMoveFromCell = null;
        private Border? _lastMoveToCell = null;

        /// <summary>
        /// Подсвечивает клетки, с которых фигура ушла и на которые пришла в последнем совершённом ходе.
        /// Предыдущая подсветка снимается автоматически.
        /// </summary>
        private void HighlightLastMove(Position from, Position to)
        {
            // This method is now empty to disable last move highlighting.
            // The logic was removed as requested.
        }

        private void ClearLastMoveHighlight()
        {
            // This method is now empty.
            // It was related to the disabled last move highlighting.
        }

        private PieceType? _pendingPromotionType = null; // Для выбора фигуры при превращении
        private Action<PieceType?>? _promotionCallback; // Сделано nullable и используется

        private ObservableCollection<MovePair> _moveHistoryCollection = new();
        private int _currentHistoryIndex = -1; // -1 = начальная позиция
        // Добавляем флаг, чтобы подавить обновление истории при навигации по ней
        private bool _suppressMoveHistoryUpdate = false; // true, когда мы временно не хотим перестраивать список ходов
        // Отслеживает количество строк в истории ходов, чтобы анимировать появление
        private int _lastMoveHistoryRowCount = 0;
        private MovePair? _pendingRowToAnimate = null;

        private void InitializeTimerControls()
        {
            _playerTimeLabel = (TextBlock)FindName("PlayerTimeLabel");
            _botTimeLabel = (TextBlock)FindName("BotTimeLabel");

            if (_playerTimeLabel == null || _botTimeLabel == null)
            {
                throw new InvalidOperationException("Не удалось найти элементы управления таймером");
            }
        }

        private void UpdateTimerDisplays()
        {
            if (_chessTimer == null) return;

            try
            {
                // Обновляем основное время
                uiPlayerTimerMain.Text = _chessTimer.FormatTimeSpan(_chessTimer.PlayerTimeLeft);
                uiBotTimerMain.Text = _chessTimer.FormatTimeSpan(_chessTimer.BotTimeLeft);

                // Обновляем миллисекунды
                uiPlayerTimerMs.Text = _chessTimer.FormatMilliseconds(_chessTimer.PlayerTimeLeft);
                uiBotTimerMs.Text = _chessTimer.FormatMilliseconds(_chessTimer.BotTimeLeft);

                // Обновляем цвета
                uiPlayerTimerMain.Foreground = _chessTimer.GetTimeColor(_chessTimer.PlayerTimeLeft);
                uiBotTimerMain.Foreground = _chessTimer.GetTimeColor(_chessTimer.BotTimeLeft);

                // Подсветка активного таймера
                if (_chessTimer.IsEnabled)
                {
                    if (_chessTimer.IsPlayerTurn)
                    {
                        uiPlayerTimerMain.Foreground = (SolidColorBrush)FindResource("SakuraAccent");
                    }
                    else
                    {
                        uiBotTimerMain.Foreground = (SolidColorBrush)FindResource("SakuraAccent");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainWindow] Ошибка при обновлении таймеров: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            Console.WriteLine($"[MainWindow] {message}");
            Debug.WriteLine($"[MainWindow] {message}");
        }

        public MainWindow(string gameMode = "Classic", int thinkingTimeMs = 1000, bool isPlayerWhite = true, int timeControlSeconds = 0)
        {
            try
            {
                InitializeComponent();

                // Полоса оценки всегда рисуется «белые-справа / чёрные-слева».
                // Никакого перекидывания прямоугольников больше не нужно.
                //Orientation setup removed – kept call commented intentionally.
                //SetupEvaluationBarOrientation(isPlayerWhite);

                _chessBoard = new ChessBoard(gameMode == "Chess960");
                _gameMode = gameMode;
                _thinkingTimeMs = thinkingTimeMs;
                _isPlayerWhite = isPlayerWhite;
                _isPlayerTurn = isPlayerWhite;

                // Инициализируем звук
                InitializeAudioServiceAsync();

                // Инициализируем таймер
                if (timeControlSeconds > 0)
                {
                    _chessTimer = new ChessTimer(
                        timeControlSeconds,
                        UpdateTimerDisplays,
                        OnPlayerTimeOut,
                        OnBotTimeOut,
                        _audioService,
                        isPlayerWhite
                    );
                    _chessTimer.Enable();
                    if (_isPlayerWhite)
                    {
                        _chessTimer.Start();
                    }
                }

                Loaded += MainWindow_Loaded_Async;
                ContentRendered += MainWindow_ContentRendered_Async;
                uiInnerBoardGrid.SizeChanged += uiInnerBoardGrid_SizeChanged;

                RenderOrUpdateBoardVisuals();
            }
            catch (Exception ex)
            {
                Log($"КРИТИЧЕСКАЯ ОШИБКА в конструкторе: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"Ошибка при инициализации окна: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        // Ориентация полосы по умолчанию уже правильная (чёрные слева, белые справа),
        // поэтому дополнительная настройка не требуется. Метод оставлен пустым, чтобы
        // не нарушать вызовы, но больше ничего не делает.
        private void SetupEvaluationBarOrientation(bool isPlayerWhite) { /* no-op */ }

        private async void InitializeAudioServiceAsync()
        {
            try
            {
                Log("Начинаем инициализацию звукового сервиса...");
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var soundPath = Path.Combine(baseDir, "Sounds", "MoveSongs.mp3");
                Log($"Путь к звуковому файлу: {soundPath}");
                Log($"Файл существует: {File.Exists(soundPath)}");

                if (File.Exists(soundPath))
                {
                    var fileInfo = new FileInfo(soundPath);
                    Log($"Размер файла: {fileInfo.Length} байт");
                }

                var audioService = new AudioService();
                var initResult = await audioService.WaitForInitializationAsync();

                if (!initResult)
                {
                    Log("ОШИБКА: Не удалось инициализировать звуковой сервис");
                    MessageBox.Show(
                        $"Ошибка инициализации звука.\n\nПроверьте:\n1. Файл MoveSongs.mp3 существует\n2. Файл не поврежден\n3. Звук в Windows включен",
                        "Ошибка звука",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    _audioService = null;
                }
                else
                {
                    Log("Звуковой сервис успешно инициализирован");
                    _audioService = audioService;
                }
            }
            catch (Exception ex)
            {
                Log($"КРИТИЧЕСКАЯ ОШИБКА инициализации звука: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show(
                    $"Ошибка инициализации звука:\n{ex.Message}",
                    "Ошибка звука",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _audioService = null;
            }
        }

        private async void MainWindow_Loaded_Async(object sender, RoutedEventArgs e)
        {
            try
            {
                // Инициализируем движок
                await InitializeEngineAsync();

                // Получаем первую оценку и лучший ход до начала партии
                await RefreshEvaluationAsync();

                // Если игрок играет за черных, делаем первый ход за белых
                if (!_isPlayerWhite && _engineInitialized)
                {
                    Log("Игрок играет за черных, запрашиваем первый ход у движка");
                    if (_chessTimer != null)
                    {
                        _chessTimer.Start();
                    }
                    await MakeBotMoveAsync();
                }

                // Обновляем состояние кнопок
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                Log($"Ошибка при инициализации: {ex.Message}");
                MessageBox.Show("Произошла ошибка при инициализации игры.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MainWindow_ContentRendered_Async(object sender, EventArgs e)
        {
            // Обновляем отображение времени (размеры элементов к этому моменту уже известны)
            try
            {
                UpdateTimerDisplays();

                // Движок запускается в MainWindow_Loaded_Async. Здесь повторно ничего не инициализируем,
                // чтобы не создавать дубликаты процессов и не получать таймаутов.

                // Если к этому моменту движок уже готов – можно обновить индикатор хода и оценку.
                if (_engineInitialized)
                {
                    UpdateTurnIndicator();
                    await RefreshEvaluationAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении UI: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task InitializeEngineAsync()
        {
            try
            {
                Console.WriteLine("[MainWindow] Начало инициализации движка...");
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string enginePath = Path.Combine(baseDir, "Lc-0", "lc0.exe");
                string weightsPath = Path.Combine(baseDir, "Lc-0", "791556.pb.gz");

                Console.WriteLine($"[MainWindow] Путь к движку: {enginePath}");
                Console.WriteLine($"[MainWindow] Путь к весам: {weightsPath}");

                if (!File.Exists(enginePath))
                {
                    throw new FileNotFoundException($"Не найден исполняемый файл движка: {enginePath}");
                }

                if (!File.Exists(weightsPath))
                {
                    throw new FileNotFoundException($"Не найден файл весов: {weightsPath}");
                }

                _lc0Engine = new Lc0Engine(enginePath, weightsPath);
                await _lc0Engine.InitializeAsync();
                _engineInitialized = true;
                Console.WriteLine("[MainWindow] Движок успешно инициализирован");

                // Инициализируем второй движок для аналитики
                _analysisEngine = new Lc0Engine(enginePath, weightsPath);
                await _analysisEngine.InitializeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainWindow] ОШИБКА при инициализации движка: {ex.Message}");
                Console.WriteLine($"[MainWindow] Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"Ошибка при инициализации движка: {ex.Message}\n\nПроверьте:\n1. Файлы движка в папке Lc-0\n2. Антивирус не блокирует lc0.exe",
                    "Ошибка инициализации", MessageBoxButton.OK, MessageBoxImage.Error);
                _engineInitialized = false;
                throw; // Пробрасываем ошибку дальше
            }
        }

        private void uiInnerBoardGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderOrUpdateBoardVisuals();
        }

        public List<string> NumericLabels => _numericLabels; // Для биндинга в XAML
        public List<string> AlphabetLabels => _alphabetLabels; // Для биндинга в XAML

        private void RenderOrUpdateBoardVisuals()
        {
            if (uiInnerBoardGrid == null) return;

            // Очищаем старые элементы
            uiInnerBoardGrid.Children.Clear();
            uiInnerBoardGrid.RowDefinitions.Clear();
            uiInnerBoardGrid.ColumnDefinitions.Clear();

            // Добавляем определения строк и столбцов
            for (int i = 0; i < ChessBoard.Size; i++)
            {
                uiInnerBoardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                uiInnerBoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Инициализируем массивы
            _cellVisuals = new Border[ChessBoard.Size, ChessBoard.Size];
            _pieceImages = new Image[ChessBoard.Size, ChessBoard.Size];
            _possibleMoveDots = new Ellipse[ChessBoard.Size, ChessBoard.Size];

            // Создаем ячейки
            for (int row = 0; row < ChessBoard.Size; row++)
            {
                for (int col = 0; col < ChessBoard.Size; col++)
                {
                    CreateCellVisuals(new Position(row, col));
                }
            }

            // Обновляем содержимое всех ячеек
            for (int row = 0; row < ChessBoard.Size; row++)
            {
                for (int col = 0; col < ChessBoard.Size; col++)
                {
                    UpdateCellContent(new Position(row, col));
                }
            }

            // После перерисовки доски обновляем подсветку шаха/мата
            RefreshKingCheckHighlight();
            UpdateMoveHistory();
        }

        private void CreateCellVisuals(Position pos)
        {
            var cell = new Border
            {
                Background = GetCellBackground(pos),
                Child = new Grid(),
                Tag = pos
            };

            // Добавляем обработчик клика
            cell.MouseDown += Cell_MouseDown;

            // Создаем точку для возможного хода
            var possibleMoveDot = new Ellipse
            {
                Style = (Style)FindResource("PossibleMoveDotStyle"),
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            ((Grid)cell.Child).Children.Add(possibleMoveDot);
            _possibleMoveDots[pos.Row, pos.Col] = possibleMoveDot;

            // Создаем изображение фигуры
            var pieceImage = new Image
            {
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            ((Grid)cell.Child).Children.Add(pieceImage);
            _pieceImages[pos.Row, pos.Col] = pieceImage;

            // Добавляем координаты
            if (pos.Col == (_isPlayerWhite ? 7 : 0)) // Цифры справа для белых, слева для черных
            {
                var numberLabel = new TextBlock
                {
                    Text = _numericLabels[pos.Row],
                    FontSize = 16,
                    // Используем светлую сакура-кисть, чтобы цифры были хорошо видны на обоих цветах клеток
                    Foreground = (Brush)FindResource("SakuraLight"),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 2, 0)
                };
                ((Grid)cell.Child).Children.Add(numberLabel);
            }

            if (pos.Row == (_isPlayerWhite ? 7 : 0)) // Буквы внизу
            {
                var letterLabel = new TextBlock
                {
                    Text = _alphabetLabels[pos.Col],
                    FontSize = 16,
                    // Используем светлую сакура-кисть, чтобы цифры были хорошо видны на обоих цветах клеток
                    Foreground = (Brush)FindResource("SakuraLight"),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(2, 0, 0, 2)
                };
                ((Grid)cell.Child).Children.Add(letterLabel);
            }

            // Сохраняем ячейку
            _cellVisuals[pos.Row, pos.Col] = cell;

            // Добавляем ячейку на доску
            Grid.SetRow(cell, _isPlayerWhite ? pos.Row : 7 - pos.Row);
            Grid.SetColumn(cell, _isPlayerWhite ? pos.Col : 7 - pos.Col);
            uiInnerBoardGrid.Children.Add(cell);
        }

        private void UpdateCellContent(Position pos)
        {
            if (_pieceImages == null || _pieceImages[pos.Row, pos.Col] == null)
            {
                Console.WriteLine($"UI ОШИБКА: UpdateCellContent - _pieceImages не инициализирован для позиции {pos}");
                return;
            }

            Piece? piece = _chessBoard.GetPiece(pos);
            Image imageControl = _pieceImages[pos.Row, pos.Col];

            if (piece != null)
            {
                string imagePath = $"pack://application:,,,/Images/{piece.ImageName}.png";
                try
                {
                    if (!_imageCache.TryGetValue(piece.ImageName, out BitmapImage? bitmap))
                    {
                        bitmap = new BitmapImage(new Uri(imagePath));
                        _imageCache[piece.ImageName] = bitmap;
                    }
                    imageControl.Source = bitmap;
                    imageControl.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UI ОШИБКА: Не удалось загрузить изображение {piece.ImageName}: {ex.Message}");
                    imageControl.Source = null;
                    imageControl.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                imageControl.Source = null;
                imageControl.Visibility = Visibility.Collapsed;
            }
        }

        private Brush GetCellBackground(Position pos)
        {
            bool isLightSquare = (pos.Row + pos.Col) % 2 == 0;
            return (Brush)FindResource(isLightSquare ? "DarkBoardLightCellBrush" : "DarkBoardDarkCellBrush");
        }

        private async void Cell_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_engineInitialized || !_isPlayerTurn)
            {
                Log($"Ход не возможен. Движок инициализирован: {_engineInitialized}, Ход игрока: {_isPlayerTurn}");
                return;
            }

            // Запускаем таймер игрока при его первом клике, если он еще не запущен
            if (_chessTimer?.IsEnabled == true && !_chessTimer.IsRunning)
            {
                _chessTimer.Start();
            }

            var cell = sender as Border;
            if (cell == null) return;

            // Получаем позицию на доске
            int row = _isPlayerWhite ? Grid.GetRow(cell) : 7 - Grid.GetRow(cell);
            int col = _isPlayerWhite ? Grid.GetColumn(cell) : 7 - Grid.GetColumn(cell);
            var clickedPos = new Position(row, col);

            // Получаем фигуру на кликнутой позиции
            var piece = _chessBoard.GetPiece(clickedPos);

            if (_selectedPosition.HasValue)
            {
                // Если выбрана та же клетка, снимаем выделение
                if (_selectedPosition.Value == clickedPos)
                {
                    ClearSelectionAndHighlights();
                    return;
                }

                // Проверяем, является ли это допустимым ходом
                var possibleMoves = _chessBoard.GetPossibleMoves(_selectedPosition.Value);
                if (possibleMoves.Contains(clickedPos))
                {
                    await HandlePlayerMoveAsync(_selectedPosition.Value, clickedPos);
                }
                else if (piece != null && piece.Color == (_isPlayerWhite ? PieceColor.White : PieceColor.Black))
                {
                    // Если кликнули по своей фигуре, выбираем её
                    SelectPieceAndShowMoves(clickedPos, cell);
                }
                else
                {
                    ClearSelectionAndHighlights();
                }
            }
            else if (piece != null && piece.Color == (_isPlayerWhite ? PieceColor.White : PieceColor.Black))
            {
                // Выбираем фигуру и показываем возможные ходы
                SelectPieceAndShowMoves(clickedPos, cell);
            }
        }

        private void SelectPieceAndShowMoves(Position pos, Border cellVisual)
        {
            ClearSelectionAndHighlights();

            // Проверяем, можно ли выбрать эту фигуру
            var piece = _chessBoard.GetPiece(pos);
            if (piece == null || piece.Color != (_isPlayerWhite ? PieceColor.White : PieceColor.Black))
            {
                return;
            }

            // Подсвечиваем выбранную ячейку
            _selectedPosition = pos;
            _selectedCellVisual = cellVisual;
            _selectedCellVisual.Background = (Brush)FindResource("SelectedCellHighlight");
            _selectedCellVisual.Effect = new DropShadowEffect
            {
                Color = ((SolidColorBrush)FindResource("SakuraPink")).Color,
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.3
            };

            // Получаем и отображаем возможные ходы
            var possibleMoves = _chessBoard.GetPossibleMoves(pos);
            if (possibleMoves == null) return;

            foreach (var movePos in possibleMoves)
            {
                var targetCell = _cellVisuals[movePos.Row, movePos.Col];
                if (targetCell != null)
                {
                    var targetPiece = _chessBoard.GetPiece(movePos);
                    if (targetPiece != null)
                    {
                        // Подсветка возможного взятия
                        targetCell.BorderBrush = (Brush)FindResource("CaptureHighlight");
                        targetCell.BorderThickness = new Thickness(3);
                        targetCell.Effect = new DropShadowEffect
                        {
                            Color = ((SolidColorBrush)FindResource("SakuraAccent")).Color,
                            BlurRadius = 10,
                            ShadowDepth = 0,
                            Opacity = 0.3
                        };
                        _highlightedCaptureCells.Add(targetCell);
                    }
                    else if (_possibleMoveDots != null && _possibleMoveDots[movePos.Row, movePos.Col] != null)
                    {
                        // Показываем точку для хода на пустую клетку
                        _possibleMoveDots[movePos.Row, movePos.Col].Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void ClearSelectionAndHighlights()
        {
            // Сброс подсветки выбранной ячейки
            if (_selectedCellVisual != null && _selectedPosition.HasValue)
            {
                _selectedCellVisual.Background = GetCellBackground(_selectedPosition.Value);
                // Убираем эффект подсветки (например, тень) с ранее выбранной клетки
                _selectedCellVisual.Effect = null;
                _selectedCellVisual = null;
            }
            _selectedPosition = null;

            // Очищаем подсветку возможных ходов (точки)
            if (_possibleMoveDots != null)
            {
                foreach (var dot in _possibleMoveDots)
                {
                    if (dot != null) dot.Visibility = Visibility.Collapsed;
                }
            }

            // Очищаем подсветку взятий (рамки)
            foreach (var cell in _highlightedCaptureCells)
            {
                if (cell != null)
                {
                    cell.BorderBrush = null;
                    cell.BorderThickness = new Thickness(0);
                }
            }
            _highlightedCaptureCells.Clear();
        }

        private async Task HandlePlayerMoveAsync(Position fromPos, Position toPos)
        {
            var piece = _chessBoard.GetPiece(fromPos);
            if (piece == null) return;

            // Если это ход пешки на последнюю горизонталь, ждем выбор фигуры для превращения
            PieceType? promotionType = null;
            if (piece.Type == PieceType.Pawn && (toPos.Row == 0 || toPos.Row == 7))
            {
                promotionType = await WaitForPromotionChoiceAsync(piece.Color);
                if (promotionType == null)
                {
                    // Игрок отменил выбор, отменяем ход
                    ClearSelectionAndHighlights();
                    return;
                }
            }
            
            // Сначала убираем подсветку, потом анимируем
            ClearSelectionAndHighlights();
            await AnimatePieceMoveAsync(fromPos, toPos);
            await MakePlayerMoveAsync(fromPos, toPos, promotionType);
        }

        private void UpdateButtonStates()
        {
            // Кнопка доступна, только если можно отменить/повторить ПАРУ полуходов (ход игрока + бота)
            if (historyUndoButton != null)
                historyUndoButton.IsEnabled = _chessBoard.GetFullMoveHistory().Count >= 2;

            if (historyRedoButton != null)
                historyRedoButton.IsEnabled = _chessBoard.CanRedoMove();
            if (historyStartButton != null)
                historyStartButton.IsEnabled = _chessBoard.CanUndoMove();
            if (historyEndButton != null)
                historyEndButton.IsEnabled = _chessBoard.CanRedoMove();
        }

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Нужно отменить два полухода, чтобы снова была очередь игрока
                int requiredUndo = 2;
                if (_chessBoard.GetFullMoveHistory().Count < requiredUndo) return;

                var changedPositions = new List<Position>();

                for (int i = 0; i < requiredUndo; i++)
                {
                    if (!_chessBoard.CanUndoMove()) break;

                    // Получаем последний ход перед отменой, чтобы знать откуда-куда вернуть фигуру
                    var fullHistoryBeforeUndo = _chessBoard.GetFullMoveHistory();
                    if (fullHistoryBeforeUndo.Count == 0) break;

                    var lastMove = fullHistoryBeforeUndo.Last();

                    // Запускаем обратную анимацию: из lastMove.To обратно в lastMove.From
                    await AnimatePieceMoveAsync(lastMove.To, lastMove.From);

                    // Теперь применяем саму отмену хода
                    var moveChanged = _chessBoard.UndoMove();
                    changedPositions.AddRange(moveChanged);

                    // Мгновенно обновляем клетки после каждого полухода, чтобы исходные изображения не оставались скрытыми
                    foreach (var p in moveChanged)
                    {
                        UpdateCellContent(p);
                    }
                }

                // На случай, если что-то осталось, выполняем финальный проход
                foreach (var pos in changedPositions.Distinct())
                {
                    UpdateCellContent(pos);
                }

                _audioService?.PlayMoveSound();

                // Очищаем выделение и подсветку
                ClearSelectionAndHighlights();

                // Проверяем шах для текущей позиции
                var kingColor = _chessBoard.IsWhiteTurn ? PieceColor.White : PieceColor.Black;
                if (_chessBoard.IsKingInCheck(kingColor))
                {
                    HighlightKingInCheck(kingColor);
                }

                // Анимируем удаление последней строки из истории ходов перед её фактическим обновлением
                if (_moveHistoryCollection.Count > 0)
                {
                    var lastItem = _moveHistoryCollection.Last();
                    var container = uiMoveHistoryList.ItemContainerGenerator.ContainerFromItem(lastItem) as FrameworkElement;
                    if (container != null)
                    {
                        await AnimateMoveHistoryRowRemovalAsync(container);
                    }
                }

                // Обновляем состояние игры и интерфейс
                _isPlayerTurn = _chessBoard.IsWhiteTurn == _isPlayerWhite;
                UpdateTurnIndicator();
                UpdateButtonStates();
                UpdateMoveHistory();
                RefreshKingCheckHighlight();

                await RefreshEvaluationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отмене хода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Нужно повторить два полухода подряд
                int requiredRedo = 2;
                if (!_chessBoard.CanRedoMove()) return; // быстрый выход, если совсем нечего повторять

                var changedPositions = new List<Position>();
                for (int i = 0; i < requiredRedo; i++)
                {
                    if (!_chessBoard.CanRedoMove()) break;

                    var nextMove = _chessBoard.PeekRedoMove();
                    if (nextMove == null)
                    {
                        break;
                    }

                    // Анимация хода вперёд: из nextMove.From в nextMove.To
                    await AnimatePieceMoveAsync(nextMove.From, nextMove.To);

                    var moveChanged = _chessBoard.RedoMove();
                    changedPositions.AddRange(moveChanged);

                    foreach (var p in moveChanged)
                    {
                        UpdateCellContent(p);
                    }
                }

                foreach (var pos in changedPositions.Distinct())
                {
                    UpdateCellContent(pos);
                }

                _audioService?.PlayMoveSound();

                // Очищаем выделение и подсветку
                ClearSelectionAndHighlights();

                // Проверяем шах для текущей позиции
                var kingColor = _chessBoard.IsWhiteTurn ? PieceColor.White : PieceColor.Black;
                if (_chessBoard.IsKingInCheck(kingColor))
                {
                    HighlightKingInCheck(kingColor);
                }

                // Обновляем состояние игры
                _isPlayerTurn = _chessBoard.IsWhiteTurn == _isPlayerWhite;
                UpdateTurnIndicator();
                UpdateButtonStates();

                // Обновляем историю ходов
                UpdateMoveHistory();

                // Обновляем подсветку шаха
                RefreshKingCheckHighlight();

                await RefreshEvaluationAsync();
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Ошибка при повторе хода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await UndoAllMovesWithAnimationAsync();
        }

        private async void EndButton_Click(object sender, RoutedEventArgs e)
        {
            await RedoAllMovesWithAnimationAsync();
        }

        private async Task MakePlayerMoveAsync(Position fromPos, Position toPos, PieceType? promotionType)
        {
            try
            {
                var changedPositions = _chessBoard.MovePiece(fromPos, toPos, promotionType);
                _isPlayerTurn = false;
                // Переключаем таймер: после хода игрока начинает идти время бота
                if (_chessTimer?.IsEnabled == true)
                {
                    _chessTimer.SwitchTurn();
                    _chessTimer.Start();
                }
                PlayMoveSound();
                RenderOrUpdateBoardVisuals();
                // Обновляем подсветку шаха/мата после хода бота
                RefreshKingCheckHighlight();
                UpdateMoveHistory();
                UpdateTurnIndicator();
                UpdateButtonStates();

                await RefreshEvaluationAsync();

                // После хода игрока очередь бота. Проверяем, есть ли у бота законные ходы.
                var botColor = _isPlayerWhite ? PieceColor.Black : PieceColor.White;
                if (_chessBoard.IsCheckmate(botColor) || _chessBoard.IsStalemate(botColor))
                {
                    EndGame();
                }
                else
                {
                    await MakeBotMoveAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при ходе игрока: {ex.Message}");
                RenderOrUpdateBoardVisuals(); // Возвращаем доску в корректное состояние
            }
        }

        private Task<PieceType?> WaitForPromotionChoiceAsync(PieceColor color)
        {
            var tcs = new TaskCompletionSource<PieceType?>();
            ShowPromotionDialog(color, choice => tcs.SetResult(choice));
            return tcs.Task;
        }

        private void ShowPromotionDialog(PieceColor color, Action<PieceType?> callback)
        {
            _promotionCallback = callback;
            PromotionGrid.DataContext = color; // Используем PromotionGrid из XAML
            PromotionGrid.Visibility = Visibility.Visible;
        }

        private void PromotionChoice_Click(object sender, RoutedEventArgs e)
        {
            PieceType? chosenType = null;
            if (sender is Button button)
            {
                chosenType = button.Name switch
                {
                    "PromotionChoiceQueen" => PieceType.Queen,
                    "PromotionChoiceRook" => PieceType.Rook,
                    "PromotionChoiceBishop" => PieceType.Bishop,
                    "PromotionChoiceKnight" => PieceType.Knight,
                    _ => null
                };
            }
            PromotionGrid.Visibility = Visibility.Collapsed; // Используем PromotionGrid из XAML
            _promotionCallback?.Invoke(chosenType);
            _promotionCallback = null; // Сбрасываем после использования
        }

        private async Task MakeBotMoveAsync()
        {
            if (_isDisposed) return;
            UpdateTurnIndicator();
            Log("Бот думает...");

            var bestMove = await _lc0Engine.GetBestMoveAsync(_chessBoard.GenerateFEN(), _thinkingTimeMs);

            if (bestMove.HasValue)
            {
                var (from, to, promotion) = ParseUciMove(bestMove.Value.BestMove);
                if (from.HasValue && to.HasValue)
                {
                    await AnimatePieceMoveAsync(from.Value, to.Value);
                    var changedPositions = _chessBoard.MovePiece(from.Value, to.Value, promotion);
                    PlayMoveSound();
                    RenderOrUpdateBoardVisuals();
                    // Обновляем подсветку шаха/мата после хода бота
                    RefreshKingCheckHighlight();
                    UpdateMoveHistory();
                }
            }
            else
            {
                Log("Бот не смог найти ход.");
            }

            _isPlayerTurn = true;
            UpdateTurnIndicator();
            UpdateButtonStates();
            if (_chessTimer?.IsEnabled == true)
            {
                _chessTimer.SwitchTurn();
                _chessTimer.Start();
            }
            await RefreshEvaluationAsync();

            // После хода игрока очередь бота. Проверяем, есть ли у бота законные ходы.
            var botColor = _isPlayerWhite ? PieceColor.Black : PieceColor.White;
            if (_chessBoard.IsCheckmate(botColor) || _chessBoard.IsStalemate(botColor))
            {
                EndGame();
            }
        }

        private async Task AnimatePieceMoveAsync(Position from, Position to)
        {
            var fromCell = _cellVisuals[from.Row, from.Col];
            var toCell = _cellVisuals[to.Row, to.Col];
            var pieceImage = _pieceImages[from.Row, from.Col];

            if (fromCell == null || toCell == null || pieceImage == null || pieceImage.Source == null) return;

            // Получаем координаты относительно uiInnerBoardGrid
            var startPoint = fromCell.TransformToAncestor(uiInnerBoardGrid).Transform(new Point(0, 0));
            var endPoint = toCell.TransformToAncestor(uiInnerBoardGrid).Transform(new Point(0, 0));

            // Создаем копию изображения для анимации
            var animatedImage = new Image
            {
                Source = pieceImage.Source,
                Width = pieceImage.ActualWidth,
                Height = pieceImage.ActualHeight
            };

            // Добавляем на Canvas
            AnimationCanvas.Children.Add(animatedImage);
            Canvas.SetLeft(animatedImage, startPoint.X);
            Canvas.SetTop(animatedImage, startPoint.Y);

            // Прячем оригинальное изображение
            pieceImage.Visibility = Visibility.Collapsed;

            var transform = new TranslateTransform();
            animatedImage.RenderTransform = transform;

            var animationX = new DoubleAnimation(0, endPoint.X - startPoint.X, new Duration(TimeSpan.FromMilliseconds(250)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var animationY = new DoubleAnimation(0, endPoint.Y - startPoint.Y, new Duration(TimeSpan.FromMilliseconds(250)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var tcs = new TaskCompletionSource<bool>();
            animationY.Completed += (s, e) => tcs.SetResult(true);

            transform.BeginAnimation(TranslateTransform.XProperty, animationX);
            transform.BeginAnimation(TranslateTransform.YProperty, animationY);

            await tcs.Task;

            // Убираем анимированное изображение с Canvas
            AnimationCanvas.Children.Remove(animatedImage);
            // Видимость оригинального изображения восстановится при перерисовке доски
        }

        private (Position? from, Position? to, PieceType? promotion) ParseUciMove(string uciMove)
        {
            try
            {
                if (string.IsNullOrEmpty(uciMove) || uciMove.Length < 4)
                    return (null, null, null);

                // Парсим начальную позицию
                var fromCol = uciMove[0] - 'a';
                var fromRow = 8 - (uciMove[1] - '0');
                var from = new Position(fromRow, fromCol);

                // Парсим конечную позицию
                var toCol = uciMove[2] - 'a';
                var toRow = 8 - (uciMove[3] - '0');
                var to = new Position(toRow, toCol);

                // Проверяем наличие превращения пешки
                PieceType? promotion = null;
                if (uciMove.Length > 4)
                {
                    promotion = uciMove[4] switch
                    {
                        'q' => PieceType.Queen,
                        'r' => PieceType.Rook,
                        'b' => PieceType.Bishop,
                        'n' => PieceType.Knight,
                        _ => null
                    };
                }

                return (from, to, promotion);
            }
            catch (Exception ex)
            {
                Log($"Ошибка при разборе UCI хода {uciMove}: {ex.Message}");
                return (null, null, null);
            }
        }

        private void HighlightKingInCheck(PieceColor kingColor)
        {
            var kingPosition = _chessBoard.FindKing(kingColor);
            if (kingPosition != null)
            {
                var kingCellVisual = _cellVisuals[kingPosition.Value.Row, kingPosition.Value.Col];
                if (kingCellVisual != null)
                {
                    // Определяем, мат ли это или просто шах
                    bool isMate = _chessBoard.IsCheckmate(kingColor);

                    // Используем единый полупрозрачный цвет заливки клетки
                    string brushKey = isMate ? "KingMateCellBrush" : "KingCheckCellBrush";

                    kingCellVisual.Background = (Brush)FindResource(brushKey);
                    kingCellVisual.BorderBrush = null;
                    kingCellVisual.BorderThickness = new Thickness(0);

                    // Ставим метку, чтобы потом было легко очистить
                    kingCellVisual.Tag = "KingHighlight";
                }
            }
        }

        private void ClearKingHighlight()
        {
            if (_cellVisuals == null) return;
            for (int r = 0; r < ChessBoard.Size; r++)
            {
                for (int c = 0; c < ChessBoard.Size; c++)
                {
                    var cell = _cellVisuals[r, c];
                    if (cell != null && (cell.Tag as string) == "KingHighlight")
                    {
                        // Восстанавливаем оригинальный фон клетки
                        cell.Background = GetCellBackground(new Position(r, c));
                        cell.BorderBrush = null;
                        cell.BorderThickness = new Thickness(0);
                        cell.Tag = null;
                    }
                }
            }
        }

        private void UpdateTurnIndicator()
        {
            bool isWhiteTurn = _chessBoard.IsWhiteTurn;
            bool isPlayerTurn = (isWhiteTurn && _isPlayerWhite) || (!isWhiteTurn && !_isPlayerWhite);

            // Обновляем статусы и стили
            uiPlayerStatus.Text = isPlayerTurn ? "Ваш ход" : "Ожидание хода";
            uiBotStatus.Text = !isPlayerTurn ? "Думаю..." : "Ожидание хода";

            // Обновляем стили карточек
            uiPlayerCard.Style = isPlayerTurn ?
                (Style)FindResource("ActivePlayerCardStyle") :
                (Style)FindResource("PlayerCardStyle");

            uiBotCard.Style = !isPlayerTurn ?
                (Style)FindResource("ActivePlayerCardStyle") :
                (Style)FindResource("PlayerCardStyle");

            // Обновляем цвета таймеров
            if (_timeControlMinutes > 0)
            {
                var playerTimeColor = GetTimeColor(_playerTimeLeft);
                var botTimeColor = GetTimeColor(_botTimeLeft);

                if (isPlayerTurn)
                {
                    playerTimeColor = (SolidColorBrush)FindResource("SakuraAccent");
                }
                else
                {
                    botTimeColor = (SolidColorBrush)FindResource("SakuraAccent");
                }

                uiPlayerTimerMain.Foreground = playerTimeColor;
                uiBotTimerMain.Foreground = botTimeColor;
            }
        }

        private void EndGame()
        {
            if (_chessTimer != null)
            {
                _chessTimer.Disable();
            }
            _isPlayerTurn = false;
            UpdateButtonStates();
            Console.WriteLine("UI: Игра завершена.");
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_chessTimer != null)
            {
                _chessTimer.Disable();
            }
            base.OnClosed(e);
            _isDisposed = true;
            _lc0Engine?.Dispose();
            _analysisEngine?.Dispose();
            Console.WriteLine("UI: MainWindow закрыто, ресурсы Lc0Engine освобождены.");
        }

        private void FlipBoard()
        {
            // Очищаем все визуальные элементы
            uiInnerBoardGrid.Children.Clear();
            _cellVisuals = new Border[ChessBoard.Size, ChessBoard.Size];
            _pieceImages = new Image[ChessBoard.Size, ChessBoard.Size];
            _possibleMoveDots = new Ellipse[ChessBoard.Size, ChessBoard.Size];

            // Пересоздаем визуальные элементы доски
            RenderOrUpdateBoardVisuals();

            // Обновляем состояние кнопок
            UpdateButtonStates();

            // Обновляем индикаторы хода
            UpdateTurnIndicator();

            // Обновляем отображение времени
            if (_timeControlMinutes > 0)
            {
                UpdateTimerDisplays();
            }
        }

        private void OnPlayerTimeOut()
                {
            MessageBox.Show($"Время {(_isPlayerWhite ? "белых" : "черных")} истекло!", "Конец игры",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _audioService?.PlayMoveSound();
                    EndGame();
                }

        private void OnBotTimeOut()
                {
            MessageBox.Show($"Время {(_isPlayerWhite ? "черных" : "белых")} истекло!", "Конец игры",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _audioService?.PlayMoveSound();
                    EndGame();
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            return $"0:{timeSpan.Seconds:D2}";
        }

        private string FormatMilliseconds(TimeSpan timeSpan)
        {
            return $"{timeSpan.Milliseconds:D3}";
        }

        private Brush GetTimeColor(TimeSpan timeLeft)
        {
            if (timeLeft.TotalMinutes > 1)
                return (Brush)FindResource("SakuraLight");
            if (timeLeft.TotalSeconds > 30)
                return (Brush)FindResource("SakuraPink");
            return new SolidColorBrush(Colors.Crimson); // A more thematic red
        }

        private void PlayMoveSound()
        {
            _audioService?.PlayMoveSound();
        }

        private void UpdateMoveHistory()
        {
            // Если установлен флаг подавления, просто обновляем подсветку и выходим
            if (_suppressMoveHistoryUpdate)
            {
                UpdateMoveHistoryHighlight();
                return;
            }

            try
            {
                var moves = _chessBoard.GetMoveHistory();
                _moveHistoryCollection.Clear();

                // Добавляем только полные пары (ход белых + ответ чёрных)
                for (int i = 0; i + 1 < moves.Count; i += 2)
                {
                    var movePair = new MovePair
                    {
                        MoveNumber = (i / 2) + 1,
                        WhiteMove = moves[i],
                        WhiteMoveIndex = i,
                        BlackMove = moves[i + 1],
                        BlackMoveIndex = i + 1
                    };
                    _moveHistoryCollection.Add(movePair);
                }

                uiMoveHistoryList.ItemsSource = _moveHistoryCollection;

                // Синхронизируем текущий индекс с самым последним полуходом,
                // если не выполняется программная перемотка (suppress flag).
                _currentHistoryIndex = moves.Count - 1;

                // Анимация появления новой строки, когда добавилась ПОЛНАЯ пара ходов (white+black)
                int currentRowCount = _moveHistoryCollection.Count;
                if (currentRowCount > _lastMoveHistoryRowCount && currentRowCount > 0)
                {
                    _pendingRowToAnimate = _moveHistoryCollection.Last();
                    _ = AnimateRowWhenReadyAsync();
                }

                _lastMoveHistoryRowCount = currentRowCount; // сохраняем текущее число строк

                UpdateMoveHistoryHighlight();
            }
            catch (Exception ex)
            {
                Log($"Ошибка при обновлении истории ходов: {ex.Message}");
            }
        }

        private async void MoveHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int moveIndex)
            {
                await GoToMoveIndexAsync(moveIndex);
            }
        }

        private async Task GoToMoveIndexAsync(int index)
        {
            if (index == _currentHistoryIndex) return; // уже на нужном полуходе

            // Подавляем перестройку списка ходов на время воспроизведения
            _suppressMoveHistoryUpdate = true;
            try
            {
                int steps;
                var changedPositionsAggregate = new List<Position>();

                if (index < _currentHistoryIndex)
                {
                    // Двигаемся назад (undo)
                    steps = _currentHistoryIndex - index;
                    for (int i = 0; i < steps; i++)
                    {
                        if (!_chessBoard.CanUndoMove()) break;

                        var historyBeforeUndo = _chessBoard.GetFullMoveHistory();
                        if (historyBeforeUndo.Count == 0) break;

                        var lastMove = historyBeforeUndo.Last();

                        // Анимация в обратном направлении
                        await AnimatePieceMoveAsync(lastMove.To, lastMove.From);

                        var moved = _chessBoard.UndoMove();
                        changedPositionsAggregate.AddRange(moved);
                        foreach (var p in moved)
                        {
                            UpdateCellContent(p);
                        }
                    }
                }
                else // index > _currentHistoryIndex
                {
                    steps = index - _currentHistoryIndex;
                    for (int i = 0; i < steps; i++)
                    {
                        if (!_chessBoard.CanRedoMove()) break;

                        var nextMove = _chessBoard.PeekRedoMove();
                        if (nextMove == null) break;

                        await AnimatePieceMoveAsync(nextMove.From, nextMove.To);

                        var moved = _chessBoard.RedoMove();
                        changedPositionsAggregate.AddRange(moved);
                        foreach (var p in moved)
                        {
                            UpdateCellContent(p);
                        }
                    }
                }

                foreach (var p in changedPositionsAggregate.Distinct())
                {
                    UpdateCellContent(p);
                }

                _currentHistoryIndex = index;

                // Подсветка последнего хода
                var fullHistory = _chessBoard.GetFullMoveHistory();
                if (index < 0 || !fullHistory.Any())
                {
                    ClearLastMoveHighlight();
                }
                else if (index < fullHistory.Count)
                {
                    var lastMove = fullHistory[index];
                    HighlightLastMove(lastMove.From, lastMove.To);
                }

                ClearSelectionAndHighlights();

                // Проверяем шах после каждой позиции
                RefreshKingCheckHighlight();

                // Обновляем состояние ходов и UI
                _isPlayerTurn = _chessBoard.IsWhiteTurn == _isPlayerWhite;
                UpdateTurnIndicator();
                UpdateButtonStates();
                UpdateMoveHistoryHighlight();

                _audioService?.PlayMoveSound();

                await RefreshEvaluationAsync();
            }
            finally
            {
                _suppressMoveHistoryUpdate = false;
            }
        }

        private void UpdateMoveHistoryHighlight()
        {
            foreach (var item in _moveHistoryCollection)
            {
                var container = uiMoveHistoryList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    // В контейнере строки находятся два Button: первый (index 0) — ход белых,
                    // второй (index 1) — ход чёрных.  Ранее мы ошибочно смещали индексы на +1,
                    // поэтому подсветка переходила к соседней клетке. Исправляем.

                    var whiteButton = FindVisualChild<Button>(container, 0); // первый Button
                    var blackButton = FindVisualChild<Button>(container, 1); // второй Button

                    if (whiteButton != null)
                    {
                        whiteButton.Background = item.WhiteMoveIndex == _currentHistoryIndex
                            ? (SolidColorBrush)FindResource("SakuraAccent")
                            : (SolidColorBrush)FindResource("DarkPrimaryBackgroundBrush");
                    }

                    if (blackButton != null)
                    {
                        blackButton.Background = item.BlackMoveIndex == _currentHistoryIndex
                            ? (SolidColorBrush)FindResource("SakuraAccent")
                            : (SolidColorBrush)FindResource("DarkPrimaryBackgroundBrush");
                    }
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject obj, int childIndex = 0) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typedChild)
                {
                    if (childIndex == 0)
                        return typedChild;
                    childIndex--;
                }
                var result = FindVisualChild<T>(child, childIndex);
                if (result != null)
                    return result;
            }
            return null;
        }

        private readonly SemaphoreSlim _evalLock = new SemaphoreSlim(1,1);

        private async Task RefreshEvaluationAsync()
        {
            if (_analysisEngine == null || !_engineInitialized) return;

            await _evalLock.WaitAsync();
            try
            {
                var fenOriginal = _chessBoard.GenerateFEN();

                // Разделяем FEN, меняем токен хода на 'w' и 'b'
                var tokens = fenOriginal.Split(' ');
                if (tokens.Length < 2)
                {
                    Log("[Eval] Неверный FEN");
                    return;
                }

                string placement = tokens[0];
                string rest = string.Join(" ", tokens.Skip(2)); // всё после стороны хода

                string fenWhiteMove = $"{placement} w {rest}";
                string fenBlackMove = $"{placement} b {rest}";

                // Запрашиваем оценки ПОСЛЕДОВАТЕЛЬНО, иначе два одновременных "go" пересекают ответы

                var cpWhiteOpt = await _analysisEngine.EvaluateCpAsync(fenWhiteMove, 200);

                var cpBlackOpt = await _analysisEngine.EvaluateCpAsync(fenBlackMove, 200);

                if (!cpWhiteOpt.HasValue || !cpBlackOpt.HasValue)
                {
                    Log("[Eval] одна из оценок не получена");
                    return;
                }

                double cpWhitePerspective = cpWhiteOpt.Value; // уже со стороны белых
                double cpBlackPerspective = -cpBlackOpt.Value; // инвертируем, чтобы тоже было "для белых"

                double avgCp = (cpWhitePerspective + cpBlackPerspective) / 2.0;
                int whiteSignedCp = (int)Math.Round(avgCp);

                Log($"[Eval] two-side avg cp={avgCp:F1}; whiteSigned={whiteSignedCp}");

                // Проверяем актуальность перед обновлением UI
                // (актуальность гарантируется семафором)

                SetEvaluationFromCentipawns(whiteSignedCp);

                // Получаем расширенный анализ (Multipv 3, WDL, NPS)
                var analysis = await _analysisEngine.GetAnalysisAsync(fenOriginal, 300, 3);

                if (analysis != null)
                {
                    // Обновление UI (как раньше)
                    if (analysis.Lines.Any() && BestMoveText != null)
                    {
                        BestMoveText.Text = analysis.Lines[0].Pv.Split(' ')[0];
                    }

                    if (DepthText!=null) DepthText.Text = $"Глубина: {analysis.Depth}";
                    if (NpsText!=null)  NpsText.Text  = $"Скорость: {analysis.Nps/1000:0}k поз/с";
                    if (WdlText!=null)  WdlText.Text  = $"WDL: {analysis.Win:0}%/{analysis.Draw:0}%/{analysis.Loss:0}%";

                    if (ScoreText!=null)
                    {
                        ScoreText.Text = analysis.MatePly.HasValue
                            ? $"Мат в {Math.Abs(analysis.MatePly.Value)}"
                            : "?";
                    }

                    if (TimeText!=null)
                    {
                        TimeText.Text = $"Время: {analysis.TimeMs/1000:0.00}s";
                    }

                    if (PolicyText!=null)
                    {
                        PolicyText.Text = string.IsNullOrWhiteSpace(analysis.Policy)?"" : $"Policy: {analysis.Policy}";
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Eval] ошибка: {ex.Message}");
            }
            finally
            {
                _evalLock.Release();
            }
        }

        /// <summary>
        /// Обновляет подсветку шаха: снимает старую и, если один из королей под шахом, подсвечивает его.
        /// </summary>
        private void RefreshKingCheckHighlight()
        {
            ClearKingHighlight();

            // Сначала проверяем мат, затем шах
            if (_chessBoard.IsCheckmate(PieceColor.White) || _chessBoard.IsKingInCheck(PieceColor.White))
            {
                HighlightKingInCheck(PieceColor.White);
            }
            else if (_chessBoard.IsCheckmate(PieceColor.Black) || _chessBoard.IsKingInCheck(PieceColor.Black))
            {
                HighlightKingInCheck(PieceColor.Black);
            }
        }

        private async void RefreshPlanButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshEvaluationAsync();
        }

        /// <summary>
        /// Анимирует плавное исчезновение (fade + slide) строки истории ходов.
        /// </summary>
        private Task AnimateMoveHistoryRowRemovalAsync(FrameworkElement rowElement)
        {
            var tcs = new TaskCompletionSource<bool>();

            // Fade out
            var fadeAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeAnim, rowElement);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

            // Slide to the right slightly while fading
            var currentMargin = rowElement.Margin;
            var slideAnim = new ThicknessAnimation
            {
                From = currentMargin,
                To = new Thickness(currentMargin.Left + 40, currentMargin.Top, currentMargin.Right - 40, currentMargin.Bottom),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideAnim, rowElement);
            Storyboard.SetTargetProperty(slideAnim, new PropertyPath("Margin"));

            var sb = new Storyboard();
            sb.Children.Add(fadeAnim);
            sb.Children.Add(slideAnim);
            sb.Completed += (s, e) => tcs.TrySetResult(true);
            sb.Begin();

            return tcs.Task;
        }

        private void RunMoveHistoryRowAppearAnimation(FrameworkElement rowElement)
        {
            // начальные значения
            rowElement.Opacity = 0;
            var tt = new TranslateTransform { X = -40, Y = 0 };
            rowElement.RenderTransform = tt;

            var dur = TimeSpan.FromMilliseconds(300);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var fadeAnim = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };
            var slideAnim = new DoubleAnimation(-40, 0, dur) { EasingFunction = ease };

            rowElement.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            tt.BeginAnimation(TranslateTransform.XProperty, slideAnim);
        }

        private async Task AnimateRowWhenReadyAsync()
        {
            if (_pendingRowToAnimate == null) return;

            const int maxAttempts = 10;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var container = uiMoveHistoryList.ItemContainerGenerator.ContainerFromItem(_pendingRowToAnimate) as FrameworkElement;
                if (container != null)
                {
                    RunMoveHistoryRowAppearAnimation(container);
                    _pendingRowToAnimate = null;
                    return;
                }
                await Task.Delay(50);
            }
            // если не нашли контейнер за время ожидания — снимем флаг
            _pendingRowToAnimate = null;
        }

        private async Task UndoAllMovesWithAnimationAsync()
        {
            if (!_chessBoard.CanUndoMove()) return;

            ClearSelectionAndHighlights();

            var changedPositions = new List<Position>();

            // Пока есть что откатывать
            while (_chessBoard.CanUndoMove())
            {
                var fullHistory = _chessBoard.GetFullMoveHistory();
                if (fullHistory.Count == 0) break;

                var lastMove = fullHistory.Last();

                // Анимируем ход назад (из To в From)
                await AnimatePieceMoveAsync(lastMove.To, lastMove.From);

                // Откатываем в модели
                var moveChanged = _chessBoard.UndoMove();
                changedPositions.AddRange(moveChanged);

                // Мгновенно обновляем клетки, чтобы скрытые изображения появлялись корректно
                foreach (var pos in moveChanged)
                {
                    UpdateCellContent(pos);
                }
            }

            // Финальное обновление всех изменённых клеток
            foreach (var pos in changedPositions.Distinct())
            {
                UpdateCellContent(pos);
            }

            _audioService?.PlayMoveSound();

            // После полного отката обновляем состояние интерфейса
            UpdateAfterNavigation();
        }

        private async Task RedoAllMovesWithAnimationAsync()
        {
            if (!_chessBoard.CanRedoMove()) return;

            ClearSelectionAndHighlights();

            var changedPositions = new List<Position>();

            // Пока есть, что повторять
            while (_chessBoard.CanRedoMove())
            {
                var nextMove = _chessBoard.PeekRedoMove();
                if (nextMove == null) break;

                // Анимируем ход вперёд (из From в To)
                await AnimatePieceMoveAsync(nextMove.From, nextMove.To);

                var moveChanged = _chessBoard.RedoMove();
                changedPositions.AddRange(moveChanged);

                foreach (var pos in moveChanged)
                {
                    UpdateCellContent(pos);
                }
            }

            foreach (var pos in changedPositions.Distinct())
            {
                UpdateCellContent(pos);
            }

            _audioService?.PlayMoveSound();

            // После полного повтора обновляем состояние интерфейса
            UpdateAfterNavigation();
        }

        // Обновляет общий UI после перемотки начала/конца
        private async void UpdateAfterNavigation()
        {
            // Обновляем очередь хода относительно цвета игрока
            _isPlayerTurn = _chessBoard.IsWhiteTurn == _isPlayerWhite;

            // Сбрасываем сглаженное значение, чтобы шкала не "тащила" старую оценку
            _lastEvaluationBarValue = 0;

            UpdateButtonStates();
            UpdateTurnIndicator();

            // Подсветка королей под шахом
            RefreshKingCheckHighlight();

            // Обновляем историю и оценку
            UpdateMoveHistory();
            await RefreshEvaluationAsync();

            // Если после перемотки очередь бота — сразу делаем его ход
            if (!_isPlayerTurn && _engineInitialized)
            {
                await MakeBotMoveAsync();
            }
        }
    }
}