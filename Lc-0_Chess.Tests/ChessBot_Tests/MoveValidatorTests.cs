using System;
using Xunit;
using Lc_0_Chess.Models;
using Lc_0_Chess.Models.MoveValidators;

namespace Lc_0_Chess.Tests.ChessBot_Tests
{
    public class MoveValidatorTests
    {
        [Fact]
        public void PawnMoveValidator_BasicMoves_ShouldBeValid()
        {
            // Arrange
            var validator = new PawnMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // White pawn moves
            Assert.True(validator.IsValidMove(
                new Position(6, 0),
                new Position(5, 0),
                board));

            Assert.True(validator.IsValidMove(
                new Position(6, 0),
                new Position(4, 0),
                board));

            // Black pawn moves
            Assert.True(validator.IsValidMove(
                new Position(1, 0),
                new Position(2, 0),
                board));

            Assert.True(validator.IsValidMove(
                new Position(1, 0),
                new Position(3, 0),
                board));
        }

        [Fact]
        public void KnightMoveValidator_BasicMoves_ShouldBeValid()
        {
            // Arrange
            var validator = new KnightMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // White knight moves
            Assert.True(validator.IsValidMove(
                new Position(7, 1),
                new Position(5, 0),
                board));

            Assert.True(validator.IsValidMove(
                new Position(7, 1),
                new Position(5, 2),
                board));
        }

        [Fact]
        public void BishopMoveValidator_BasicMoves_ShouldBeValid()
        {
            // Arrange
            var validator = new BishopMoveValidator();
            var board = new ChessBoard();
            // Move pawns to clear path
            board.MovePiece(new Position(6, 3), new Position(4, 3), null); // White pawn
            board.MovePiece(new Position(1, 3), new Position(3, 3), null); // Black pawn
            board.MovePiece(new Position(6, 5), new Position(4, 5), null); // White pawn
            board.MovePiece(new Position(1, 5), new Position(3, 5), null); // Black pawn
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // White pawn to clear diagonal
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn

            // Act & Assert
            Assert.True(validator.IsValidMove(
                new Position(7, 2),
                new Position(5, 4),
                board));

            Assert.True(validator.IsValidMove(
                new Position(7, 5),
                new Position(5, 3),
                board));
        }

        [Fact]
        public void RookMoveValidator_BasicMoves_ShouldBeValid()
        {
            // Arrange
            var validator = new RookMoveValidator();
            var board = new ChessBoard();
            // Move pawn to clear path
            board.MovePiece(new Position(6, 0), new Position(4, 0), null); // White pawn
            board.MovePiece(new Position(1, 0), new Position(3, 0), null); // Black pawn

            // Act & Assert
            Assert.True(validator.IsValidMove(
                new Position(7, 0),
                new Position(5, 0),
                board));

            Assert.False(validator.IsValidMove(
                new Position(7, 0),
                new Position(5, 1),
                board));
        }

        [Fact]
        public void QueenMoveValidator_BasicMoves_ShouldBeValid()
        {
            // Arrange
            var validator = new QueenMoveValidator();
            var board = new ChessBoard();
            // Move pawns to clear paths
            board.MovePiece(new Position(6, 3), new Position(4, 3), null); // White pawn
            board.MovePiece(new Position(1, 3), new Position(3, 3), null); // Black pawn
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // White pawn
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn

            // Act & Assert
            // Diagonal move
            Assert.True(validator.IsValidMove(
                new Position(7, 3),
                new Position(5, 5),
                board));

            // Straight move
            Assert.True(validator.IsValidMove(
                new Position(7, 3),
                new Position(5, 3),
                board));
        }

        [Fact]
        public void KingMoveValidator_BasicMoves_ShouldBeValid()
        {
            // Arrange
            var validator = new KingMoveValidator();
            var board = new ChessBoard();
            // Move pawns to clear path
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // White pawn
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn
            board.MovePiece(new Position(6, 5), new Position(4, 5), null); // White pawn
            board.MovePiece(new Position(1, 5), new Position(3, 5), null); // Black pawn

            // Act & Assert
            // One square moves
            Assert.True(validator.IsValidMove(
                new Position(7, 4),
                new Position(6, 4),
                board));

            Assert.True(validator.IsValidMove(
                new Position(7, 4),
                new Position(6, 5),
                board));

            // Invalid two square move (not castling)
            Assert.False(validator.IsValidMove(
                new Position(7, 4),
                new Position(7, 6),
                board));
        }

        [Fact]
        public void PawnMoveValidator_InvalidMoves_ShouldBeFalse()
        {
            // Arrange
            var validator = new PawnMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // Backward move
            Assert.False(validator.IsValidMove(
                new Position(6, 0),
                new Position(7, 0),
                board));

            // Diagonal move without capture
            Assert.False(validator.IsValidMove(
                new Position(6, 0),
                new Position(5, 1),
                board));

            // Three squares forward
            Assert.False(validator.IsValidMove(
                new Position(6, 0),
                new Position(3, 0),
                board));
        }

        [Fact]
        public void KnightMoveValidator_InvalidMoves_ShouldBeFalse()
        {
            // Arrange
            var validator = new KnightMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // Straight move
            Assert.False(validator.IsValidMove(
                new Position(7, 1),
                new Position(6, 1),
                board));

            // Diagonal move
            Assert.False(validator.IsValidMove(
                new Position(7, 1),
                new Position(6, 2),
                board));
        }

        [Fact]
        public void BishopMoveValidator_InvalidMoves_ShouldBeFalse()
        {
            // Arrange
            var validator = new BishopMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // Straight move
            Assert.False(validator.IsValidMove(
                new Position(7, 2),
                new Position(6, 2),
                board));

            // L-shaped move
            Assert.False(validator.IsValidMove(
                new Position(7, 2),
                new Position(5, 1),
                board));
        }

        [Fact]
        public void RookMoveValidator_InvalidMoves_ShouldBeFalse()
        {
            // Arrange
            var validator = new RookMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // Diagonal move
            Assert.False(validator.IsValidMove(
                new Position(7, 0),
                new Position(6, 1),
                board));

            // L-shaped move
            Assert.False(validator.IsValidMove(
                new Position(7, 0),
                new Position(5, 1),
                board));
        }

        [Fact]
        public void QueenMoveValidator_InvalidMoves_ShouldBeFalse()
        {
            // Arrange
            var validator = new QueenMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // L-shaped move
            Assert.False(validator.IsValidMove(
                new Position(7, 3),
                new Position(5, 2),
                board));

            // Non-straight, non-diagonal move
            Assert.False(validator.IsValidMove(
                new Position(7, 3),
                new Position(5, 1),
                board));
        }

        [Fact]
        public void KingMoveValidator_InvalidMoves_ShouldBeFalse()
        {
            // Arrange
            var validator = new KingMoveValidator();
            var board = new ChessBoard();

            // Act & Assert
            // Two squares diagonally
            Assert.False(validator.IsValidMove(
                new Position(7, 4),
                new Position(5, 6),
                board));

            // Knight-like move
            Assert.False(validator.IsValidMove(
                new Position(7, 4),
                new Position(5, 5),
                board));
        }
    }
}