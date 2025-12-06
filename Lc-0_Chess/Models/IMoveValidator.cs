namespace Lc_0_Chess.Models // Adjusted namespace
{
    public interface IMoveValidator
    {
        bool IsValidMove(Position from, Position to, IBoard board);
    }
}