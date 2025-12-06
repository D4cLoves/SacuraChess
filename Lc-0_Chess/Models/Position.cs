using System;

namespace Lc_0_Chess.Models // Adjusted namespace
{
    public readonly struct Position : IEquatable<Position>
    {
        public int Row { get; }
        public int Col { get; }

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        // Assuming ChessBoard.Size will be accessible or defined elsewhere (e.g., a global const or config)
        // For now, let's hardcode it or assume it's available via a static property if ChessBoard is in the same namespace.
        // If ChessBoard is created later, this might need adjustment.
        public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8; // Assuming 8x8 board

        public static Position FromAlgebraic(string algebraic)
        {
            if (string.IsNullOrEmpty(algebraic) || algebraic.Length != 2)
                throw new ArgumentException("Invalid algebraic notation", nameof(algebraic));

            int col = char.ToLower(algebraic[0]) - 'a';
            // Assuming 8x8 board and standard FEN-like rank numbering (1-8 from bottom to top)
            // where row 0 in a 2D array corresponds to rank 8.
            int row = 8 - (algebraic[1] - '0');

            return new Position(row, col);
        }

        public string ToAlgebraic()
        {
            if (!IsValid)
                throw new InvalidOperationException("Cannot convert invalid position to algebraic notation");

            char file = (char)('a' + Col);
            char rank = (char)('0' + (8 - Row)); // Assuming 8x8 board
            return $"{file}{rank}";
        }

        public override bool Equals(object obj) => obj is Position position && Equals(position);

        public bool Equals(Position other) => Row == other.Row && Col == other.Col;

        public override int GetHashCode() => HashCode.Combine(Row, Col);

        public static bool operator ==(Position left, Position right) => left.Equals(right);

        public static bool operator !=(Position left, Position right) => !left.Equals(right);

        public override string ToString() => $"({Row}, {Col})";

        public void Deconstruct(out int row, out int col)
        {
            row = Row;
            col = Col;
        }
    }
}