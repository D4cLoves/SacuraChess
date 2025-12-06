using System;

namespace Lc_0_Chess.Models.MoveValidators
{
    public class PawnMoveValidator : IMoveValidator
    {
        public bool IsValidMove(Position from, Position to, IBoard board)
        {
            var piece = board.GetPiece(from);
            if (piece == null || piece.Type != PieceType.Pawn) return false;

            // Учитываем, что в вашем Lc_0_Chess проекте:
            // Белые фигуры изначально на 6 и 7 рядах, черные на 0 и 1.
            // Ход белых пешек УМЕНЬШАЕТ ряд (например, с 6 на 5 или 4).
            // Ход черных пешек УВЕЛИЧИВАЕТ ряд (например, с 1 на 2 или 3).
            int direction = piece.Color == PieceColor.White ? -1 : 1;
            int rowDiff = to.Row - from.Row;
            int colDiffAbs = Math.Abs(to.Col - from.Col);

            // Обычный ход на одну клетку вперед
            if (colDiffAbs == 0 && rowDiff == direction && board.GetPiece(to) == null)
            {
                return true;
            }

            // Ход на две клетки вперед (только если пешка еще не ходила)
            if (colDiffAbs == 0 && !piece.HasMoved && rowDiff == 2 * direction && board.GetPiece(to) == null)
            {
                Position oneStepForward = new Position(from.Row + direction, from.Col);
                if (board.GetPiece(oneStepForward) == null) // Промежуточная клетка также должна быть пуста
                {
                    return true;
                }
            }

            // Взятие по диагонали
            if (colDiffAbs == 1 && rowDiff == direction)
            {
                var targetPiece = board.GetPiece(to);
                if (targetPiece != null && targetPiece.Color != piece.Color)
                {
                    return true; // Обычное взятие
                }
                // Взятие на проходе (en passant)
                if (targetPiece == null && board.EnPassantTarget.HasValue && to == board.EnPassantTarget.Value)
                {
                    // Для Lc_0_Chess, где белые на 6-м ряду, а черные на 1-м:
                    // Белая пешка бьет на проходе, если она на 3-м ряду (индекс 3), была на 4-м, стала на 2-й (индекс 2).
                    // Черная пешка бьет на проходе, если она на 4-м ряду (индекс 4), была на 3-м, стала на 5-й (индекс 5).
                    // from.Row для белой атакующей пешки должен быть 3 (для взятия пешки на 3-м ряду, которая прыгнула с 1 на 3)
                    // from.Row для черной атакующей пешки должен быть 4 (для взятия пешки на 4-м ряду, которая прыгнула с 6 на 4)
                    int expectedPawnRowForEnPassant = piece.Color == PieceColor.White ? 3 : 4;
                    if (from.Row == expectedPawnRowForEnPassant)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}