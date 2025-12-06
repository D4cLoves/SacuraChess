using System;
using System.Collections.Generic;
using System.Linq;

namespace Lc_0_Chess.Models
{
    public static class Chess960Generator
    {
        private static readonly Random _random = new Random();

        public static PieceType[] GeneratePosition()
        {
            var position = new PieceType[8];
            var availableSquares = Enumerable.Range(0, 8).ToList();

            // 1. Размещаем слонов на полях разного цвета
            int firstBishopSquare = _random.Next(0, 4) * 2; // Четные позиции (белые поля)
            position[firstBishopSquare] = PieceType.Bishop;
            availableSquares.Remove(firstBishopSquare);

            int secondBishopSquare = _random.Next(0, 4) * 2 + 1; // Нечетные позиции (черные поля)
            position[secondBishopSquare] = PieceType.Bishop;
            availableSquares.Remove(secondBishopSquare);

            // 2. Размещаем коней на любых свободных полях
            int firstKnightSquare = availableSquares[_random.Next(availableSquares.Count)];
            position[firstKnightSquare] = PieceType.Knight;
            availableSquares.Remove(firstKnightSquare);

            int secondKnightSquare = availableSquares[_random.Next(availableSquares.Count)];
            position[secondKnightSquare] = PieceType.Knight;
            availableSquares.Remove(secondKnightSquare);

            // 3. Размещаем ферзя на любом свободном поле
            int queenSquare = availableSquares[_random.Next(availableSquares.Count)];
            position[queenSquare] = PieceType.Queen;
            availableSquares.Remove(queenSquare);

            // 4. Размещаем ладьи и короля
            // Король должен быть между ладьями для возможности рокировки
            int rookSquare1 = availableSquares[0];
            int kingSquare = availableSquares[1];
            int rookSquare2 = availableSquares[2];

            position[rookSquare1] = PieceType.Rook;
            position[kingSquare] = PieceType.King;
            position[rookSquare2] = PieceType.Rook;

            return position;
        }
    }
}