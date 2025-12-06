using System;

namespace Lc_0_Chess.Models
{
    public class Move
    {
        public Position From { get; }
        public Position To { get; }
        public Piece MovedPiece { get; }
        public Piece? CapturedPiece { get; }
        public PieceType? PromotionType { get; }
        public bool IsEnPassant { get; }
        public bool IsCastling { get; }
        public Position? CastlingRookFrom { get; }
        public Position? CastlingRookTo { get; }
        public bool WasFirstMove { get; }

        public Move(Position from, Position to, Piece movedPiece, Piece? capturedPiece,
                   PieceType? promotionType, bool isEnPassant, bool isCastling,
                   Position? castlingRookFrom, Position? castlingRookTo, bool wasFirstMove)
        {
            From = from;
            To = to;
            MovedPiece = movedPiece;
            CapturedPiece = capturedPiece;
            PromotionType = promotionType;
            IsEnPassant = isEnPassant;
            IsCastling = isCastling;
            CastlingRookFrom = castlingRookFrom;
            CastlingRookTo = castlingRookTo;
            WasFirstMove = wasFirstMove;
        }

        public override string ToString()
        {
            string moveStr = $"{From.ToAlgebraic()}-{To.ToAlgebraic()}";
            if (PromotionType.HasValue)
                moveStr += $"={PromotionType.Value}";
            return moveStr;
        }
    }
}