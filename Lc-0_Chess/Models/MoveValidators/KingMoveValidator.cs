using System;

namespace Lc_0_Chess.Models.MoveValidators // Пространство имен Lc_0_Chess
{
    public class KingMoveValidator : IMoveValidator
    {
        public bool IsValidMove(Position from, Position to, IBoard board)
        {
            var piece = board.GetPiece(from);
            if (piece == null || piece.Type != PieceType.King) return false;

            int rowDiffAbs = Math.Abs(to.Row - from.Row);
            int colDiffAbs = Math.Abs(to.Col - from.Col);

            // Обычный ход короля на 1 клетку
            if (rowDiffAbs <= 1 && colDiffAbs <= 1 && (rowDiffAbs != 0 || colDiffAbs != 0))
            {
                var targetPiece = board.GetPiece(to);
                return targetPiece == null || targetPiece.Color != piece.Color;
            }

            // Проверка рокировки
            if (!piece.HasMoved && rowDiffAbs == 0 && colDiffAbs == 2)
            {
                // Проверяем, что король на начальной позиции
                int expectedRow = piece.Color == PieceColor.White ? 7 : 0;
                int expectedCol = 4;
                if (from.Row != expectedRow || from.Col != expectedCol)
                {
                    return false;
                }

                // Проверяем, что конечная позиция правильная
                if (to.Row != expectedRow || (to.Col != 2 && to.Col != 6))
                {
                    return false;
                }

                // Проверяем наличие ладьи
                int rookCol = to.Col == 6 ? 7 : 0;
                var rookPos = new Position(expectedRow, rookCol);
                var rook = board.GetPiece(rookPos);
                if (rook == null || rook.Type != PieceType.Rook || rook.HasMoved)
                {
                    return false;
                }

                // Проверяем, что путь чист
                return board.IsPathClear(from, rookPos);
            }

            return false;
        }
    }
}