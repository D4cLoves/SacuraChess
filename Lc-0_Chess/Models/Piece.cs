using System;

namespace Lc_0_Chess.Models // Adjusted namespace
{
    public enum PieceColor
    {
        White,
        Black
    }

    public enum PieceType
    {
        Pawn,
        Rook,
        Knight,
        Bishop,
        Queen,
        King
    }

    public class Piece
    {
        public PieceColor Color { get; }
        public PieceType Type { get; }
        public string ImageName { get; }
        public bool HasMoved { get; private set; }

        public Piece(PieceColor color, PieceType type)
        {
            Color = color;
            Type = type;
            HasMoved = false;

            string colorPrefix = color == PieceColor.White ? "l" : "d";

            string typeSuffix = Type switch
            {
                PieceType.Pawn => "p",
                PieceType.Rook => "r",
                PieceType.Knight => "n",
                PieceType.Bishop => "b",
                PieceType.Queen => "q",
                PieceType.King => "k",
                _ => throw new InvalidOperationException($"Неподдерживаемый тип фигуры: {type}")
            };

            ImageName = $"Chess_{typeSuffix}{colorPrefix}t60";
        }

        public void MarkAsMoved()
        {
            HasMoved = true;
        }

        public override string ToString() => $"{Color} {Type}";
    }
}