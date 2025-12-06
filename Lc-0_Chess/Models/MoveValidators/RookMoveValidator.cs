using System;

namespace Lc_0_Chess.Models.MoveValidators // Пространство имен Lc_0_Chess
{
    public class RookMoveValidator : IMoveValidator
    {
        public bool IsValidMove(Position from, Position to, IBoard board)
        {
            var piece = board.GetPiece(from);
            if (piece == null || piece.Type != PieceType.Rook) return false;
            if (from.Row != to.Row && from.Col != to.Col) return false; // Должен двигаться по прямой
            if (from == to) return false; // Нельзя стоять на месте

            if (!board.IsPathClear(from, to)) return false;

            var targetPiece = board.GetPiece(to);
            return targetPiece == null || targetPiece.Color != piece.Color; // Может пойти на пустую или взять чужую
        }
    }
}