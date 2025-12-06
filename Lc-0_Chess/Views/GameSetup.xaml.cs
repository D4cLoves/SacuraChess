using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Lc_0_Chess.Views
{
    /// <summary>
    /// Логика взаимодействия для GameSetup.xaml
    /// </summary>
    public partial class GameSetup : Window
    {
        private int selectedDifficulty = 0;
        private Random random = new Random();
        private string _gameMode;

        // Приватные поля для RadioButton'ов
        private RadioButton _rbNoLimit;
        private RadioButton _rbBullet30Sec;
        private RadioButton _rbBullet1Min;
        private RadioButton _rbBullet3Min;
        private RadioButton _rbBlitz5Min;
        private RadioButton _rbBlitz10Min;
        private RadioButton _rbRapid15Min;
        private RadioButton _rbRapid30Min;
        private RadioButton _rbClassical60Min;
        private RadioButton _rbWhiteSide;
        private RadioButton _rbBlackSide;
        private RadioButton _rbRandomSide;

        // Словарь для преобразования выбранного времени в минуты
        private readonly Dictionary<string, int> timeControlMinutes = new()
        {
            { "Без ограничения", 0 },
            { "30 сек", 0 },  // 30 секунд преобразуем в 0.5 минуты
            { "1 мин", 1 },
            { "3 мин", 3 },
            { "5 мин", 5 },
            { "10 мин", 10 },
            { "15 мин", 15 },
            { "30 мин", 30 },
            { "60 мин", 60 }
        };

        public GameSetup(string gameMode)
        {
            InitializeComponent();
            _gameMode = gameMode;

            // Инициализация RadioButton'ов после InitializeComponent
            _rbNoLimit = (RadioButton)FindName("NoLimitRadio");
            _rbBullet30Sec = (RadioButton)FindName("Bullet30SecRadio");
            _rbBullet1Min = (RadioButton)FindName("Bullet1MinRadio");
            _rbBullet3Min = (RadioButton)FindName("Bullet3MinRadio");
            _rbBlitz5Min = (RadioButton)FindName("Blitz5MinRadio");
            _rbBlitz10Min = (RadioButton)FindName("Blitz10MinRadio");
            _rbRapid15Min = (RadioButton)FindName("Rapid15MinRadio");
            _rbRapid30Min = (RadioButton)FindName("Rapid30MinRadio");
            _rbClassical60Min = (RadioButton)FindName("Classical60MinRadio");
            _rbWhiteSide = (RadioButton)FindName("WhiteSideRadio");
            _rbBlackSide = (RadioButton)FindName("BlackSideRadio");
            _rbRandomSide = (RadioButton)FindName("RandomSideRadio");

            // Добавляем обработчики событий для обновления превью
            _rbWhiteSide.Checked += UpdateBoardPreview;
            _rbBlackSide.Checked += UpdateBoardPreview;
            _rbRandomSide.Checked += UpdateBoardPreview;

            // Инициализируем начальное состояние доски
            Loaded += (s, e) =>
            {
                UpdateBoardPreview(null, null);
            };
        }

        private int GetThinkingTime()
        {
            return selectedDifficulty switch
                {
                    0 => 1000, // Начинающий - 1 секунда
                    1 => 3000, // Продвинутый - 3 секунды
                    2 => 5000, // Эксперт - 5 секунд
                    _ => 1000  // По умолчанию - 1 секунда
            };
        }

        private void StartGame()
        {
            // Получаем выбранное время из RadioButton
            double timeControlMinutes = 0; // По умолчанию без ограничения

            if (_rbNoLimit.IsChecked == true) timeControlMinutes = 0;
            else if (_rbBullet30Sec.IsChecked == true) timeControlMinutes = 0.5; // 30 секунд = 0.5 минуты
            else if (_rbBullet1Min.IsChecked == true) timeControlMinutes = 1;
            else if (_rbBullet3Min.IsChecked == true) timeControlMinutes = 3;
            else if (_rbBlitz5Min.IsChecked == true) timeControlMinutes = 5;
            else if (_rbBlitz10Min.IsChecked == true) timeControlMinutes = 10;
            else if (_rbRapid15Min.IsChecked == true) timeControlMinutes = 15;
            else if (_rbRapid30Min.IsChecked == true) timeControlMinutes = 30;
            else if (_rbClassical60Min.IsChecked == true) timeControlMinutes = 60;

            // Определяем режим игры на основе выбранного RadioButton
            string gameMode = Chess960Radio.IsChecked == true ? "Chess960" : "Classic";

            // Создаем главное окно с выбранными параметрами
            var mainWindow = new MainWindow(
                gameMode: gameMode,
                thinkingTimeMs: GetThinkingTime(),
                isPlayerWhite: _rbWhiteSide.IsChecked == true,
                timeControlSeconds: (int)(timeControlMinutes * 60) // Convert to seconds
            );

            mainWindow.Show();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenu menuWindow = new MainMenu();
            menuWindow.Show();
            this.Close();
        }

        private void DifficultyRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                string tag = radioButton.Tag?.ToString();
                selectedDifficulty = tag switch
                {
                    "🌱" => 0, // Начинающий
                    "⚔" => 1,  // Продвинутый
                    "👑" => 2,  // Эксперт
                    _ => 0      // По умолчанию - начинающий
                };
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartGame();
        }

        private void UpdateBoardPreview(object sender, RoutedEventArgs e)
        {
            if (BoardPreview == null) return;

            bool isWhiteSide = _rbWhiteSide.IsChecked == true;
            bool isRandom = _rbRandomSide.IsChecked == true;

            // Очищаем текущее превью
            BoardPreview.Children.Clear();
            BoardPreview.RowDefinitions.Clear();
            BoardPreview.ColumnDefinitions.Clear();

            // Добавляем определения строк и столбцов
            for (int i = 0; i < 8; i++)
            {
                BoardPreview.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BoardPreview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            if (isRandom)
            {
                // Создаем размытую доску с вопросительным знаком для случайного выбора
                var overlay = new Grid();

                // Создаем и добавляем клетки размытой доски
                InitializeBoard(true, true);

                // Добавляем знак вопроса
                var questionMark = new TextBlock
                {
                    Text = "?",
                    FontSize = 200,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB7C5")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#FF4D67"),
                        ShadowDepth = 0,
                        BlurRadius = 30,
                        Opacity = 0.8
                    }
                };

                Grid.SetRowSpan(questionMark, 8);
                Grid.SetColumnSpan(questionMark, 8);
                BoardPreview.Children.Add(questionMark);
            }
            else
            {
                // Создаем обычную доску
                InitializeBoard(isWhiteSide, false);
            }
        }

        private void InitializeBoard(bool isWhiteSide, bool isBlurred)
        {
            // Определяем цвета для доски
            var lightSquareColor = (Color)ColorConverter.ConvertFromString("#2B2B2F");  // Минималистичный светлый
            var darkSquareColor = (Color)ColorConverter.ConvertFromString("#232325");   // Минималистичный тёмный

            // Если доска размытая, делаем цвета более тусклыми
            if (isBlurred)
            {
                lightSquareColor = Color.FromRgb(
                    (byte)(lightSquareColor.R * 0.7),
                    (byte)(lightSquareColor.G * 0.7),
                    (byte)(lightSquareColor.B * 0.7));
                darkSquareColor = Color.FromRgb(
                    (byte)(darkSquareColor.R * 0.7),
                    (byte)(darkSquareColor.G * 0.7),
                    (byte)(darkSquareColor.B * 0.7));
            }

            // Создаем клетки доски
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    int actualRow = isWhiteSide ? row : 7 - row;
                    int actualCol = isWhiteSide ? col : 7 - col;

                    var border = new Border
                    {
                        Background = new SolidColorBrush((actualRow + actualCol) % 2 == 0 ? lightSquareColor : darkSquareColor)
                    };

                    if (isBlurred)
                    {
                        border.Effect = new BlurEffect { Radius = 3 };
                    }

                    // Добавляем фигуры
                    if (row == 0 || row == 1 || row == 6 || row == 7)
                    {
                        var textBlock = new TextBlock
                        {
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 42,
                            FontWeight = FontWeights.Normal,
                            FontFamily = new FontFamily("Segoe UI Symbol")
                        };

                        bool isBlackPiece;
                        if (isWhiteSide)
                        {
                            isBlackPiece = (row == 0 || row == 1);
                        }
                        else
                        {
                            isBlackPiece = (row == 6 || row == 7);
                        }

                        // Расставляем фигуры
                        if (row == 1 || row == 6)
                        {
                            textBlock.Text = "♟";
                        }
                        else if (row == 0 || row == 7)
                        {
                            switch (col)
                            {
                                case 0:
                                case 7:
                                    textBlock.Text = "♜";
                                    break;
                                case 1:
                                case 6:
                                    textBlock.Text = "♞";
                                    break;
                                case 2:
                                case 5:
                                    textBlock.Text = "♝";
                                    break;
                                case 3:
                                    textBlock.Text = "♛";
                                    break;
                                case 4:
                                    textBlock.Text = "♚";
                                    break;
                            }
                        }

                        // Устанавливаем цвет фигур с улучшенным контрастом
                        if (isBlackPiece)
                        {
                            // Черные фигуры
                            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                            // Добавляем легкое свечение для черных фигур на темных клетках
                            if ((actualRow + actualCol) % 2 != 0)
                            {
                                textBlock.Effect = new DropShadowEffect
                                {
                                    Color = Colors.White,
                                    Direction = 320,
                                    ShadowDepth = 0,
                                    BlurRadius = 3,
                                    Opacity = 0.3
                                };
                            }
                        }
                        else
                        {
                            // Белые фигуры с контуром для лучшей видимости
                            textBlock.Effect = new DropShadowEffect
                            {
                                Color = Colors.Black,
                                Direction = 320,
                                ShadowDepth = 1,
                                BlurRadius = 3,
                                Opacity = 0.7
                            };
                            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        }

                        if (isBlurred)
                        {
                            textBlock.Effect = new BlurEffect { Radius = 3 };
                        }

                        border.Child = textBlock;
                    }

                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    BoardPreview.Children.Add(border);
                }
            }
        }
    }
}
