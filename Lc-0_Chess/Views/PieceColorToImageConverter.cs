using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Lc_0_Chess.Models;

namespace Lc_0_Chess.Views
{
    public class PieceColorToImageConverter : IValueConverter
    {
        public PieceType PieceType { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PieceColor color)
            {
                string colorPrefix = color == PieceColor.White ? "l" : "d";
                string typeSuffix = PieceType switch
                {
                    PieceType.Queen => "q",
                    PieceType.Rook => "r",
                    PieceType.Bishop => "b",
                    PieceType.Knight => "n",
                    _ => throw new ArgumentException($"Неподдерживаемый тип фигуры: {PieceType}")
                };

                string imagePath = $"/Images/Chess_{typeSuffix}{colorPrefix}t60.png";
                return new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}