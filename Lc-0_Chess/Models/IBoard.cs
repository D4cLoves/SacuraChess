namespace Lc_0_Chess.Models // Adjusted namespace
{
    public interface IBoard
    {
        Piece GetPiece(Position pos);
        bool IsPathClear(Position from, Position to);
        Position? EnPassantTarget { get; }
        bool IsWhiteTurn { get; }
    }
}