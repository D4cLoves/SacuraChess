using System;

namespace Lc_0_Chess.Models.MoveValidators // Пространство имен Lc_0_Chess
{
    public class KnightMoveValidator : IMoveValidator
    {
        public bool IsValidMove(Position from, Position to, IBoard board)
        {
            var piece = board.GetPiece(from);
            if (piece == null || piece.Type != PieceType.Knight) return false;

            int rowDiffAbs = Math.Abs(to.Row - from.Row);
            int colDiffAbs = Math.Abs(to.Col - from.Col);

            if (!((rowDiffAbs == 2 && colDiffAbs == 1) || (rowDiffAbs == 1 && colDiffAbs == 2))) return false;
            if (from == to) return false;

            var targetPiece = board.GetPiece(to);
            return targetPiece == null || targetPiece.Color != piece.Color;
        }
    }
}