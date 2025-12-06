using System;

namespace Lc_0_Chess.Models.MoveValidators // Пространство имен Lc_0_Chess
{
    public class QueenMoveValidator : IMoveValidator
    {
        public bool IsValidMove(Position from, Position to, IBoard board)
        {
            var piece = board.GetPiece(from);
            if (piece == null || piece.Type != PieceType.Queen) return false;
            if (from == to) return false;

            // Движение как ладья ИЛИ как слон
            bool isRookLikeMove = from.Row == to.Row || from.Col == to.Col;
            bool isBishopLikeMove = Math.Abs(to.Row - from.Row) == Math.Abs(to.Col - from.Col);

            if (!isRookLikeMove && !isBishopLikeMove) return false;

            if (!board.IsPathClear(from, to)) return false;

            var targetPiece = board.GetPiece(to);
            return targetPiece == null || targetPiece.Color != piece.Color;
        }
    }
}