using System;

namespace Lc_0_Chess.Models.MoveValidators // Пространство имен Lc_0_Chess
{
    public class BishopMoveValidator : IMoveValidator
    {
        public bool IsValidMove(Position from, Position to, IBoard board)
        {
            var piece = board.GetPiece(from);
            if (piece == null || piece.Type != PieceType.Bishop) return false;

            if (Math.Abs(to.Row - from.Row) != Math.Abs(to.Col - from.Col)) return false; // Должен двигаться по диагонали
            if (from == to) return false;

            if (!board.IsPathClear(from, to)) return false;

            var targetPiece = board.GetPiece(to);
            return targetPiece == null || targetPiece.Color != piece.Color;
        }
    }
}