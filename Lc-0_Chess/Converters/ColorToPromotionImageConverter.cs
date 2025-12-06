using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Lc_0_Chess.Models;

namespace Lc_0_Chess.Converters
{
    public class ColorToPromotionImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PieceColor color && parameter is string pieceType)
            {
                string colorPrefix = color == PieceColor.White ? "l" : "d";
                string typeSuffix = pieceType.ToLower() switch
                {
                    "queen" => "q",
                    "rook" => "r",
                    "bishop" => "b",
                    "knight" => "n",
                    _ => throw new ArgumentException($"Неподдерживаемый тип фигуры: {pieceType}")
                };

                string imagePath = $"pack://application:,,,/Images/Chess_{typeSuffix}{colorPrefix}t60.png";
                return new BitmapImage(new Uri(imagePath));
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}