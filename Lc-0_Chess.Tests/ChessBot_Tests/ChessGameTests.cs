using System;
using Xunit;
using Lc_0_Chess.Models;
using System.Collections.Generic;
using System.Linq;

namespace Lc_0_Chess.Tests.ChessBot_Tests
{
    public class ChessGameTests
    {
        [Fact]
        public void InitialBoard_ShouldHaveCorrectPiecePositions()
        {
            // Arrange
            var board = new ChessBoard();

            // Assert
            // Check white pieces
            Assert.Equal(PieceType.Rook, board.GetPiece(new Position(7, 0))?.Type);
            Assert.Equal(PieceType.Knight, board.GetPiece(new Position(7, 1))?.Type);
            Assert.Equal(PieceType.Bishop, board.GetPiece(new Position(7, 2))?.Type);
            Assert.Equal(PieceType.Queen, board.GetPiece(new Position(7, 3))?.Type);
            Assert.Equal(PieceType.King, board.GetPiece(new Position(7, 4))?.Type);
            Assert.Equal(PieceType.Bishop, board.GetPiece(new Position(7, 5))?.Type);
            Assert.Equal(PieceType.Knight, board.GetPiece(new Position(7, 6))?.Type);
            Assert.Equal(PieceType.Rook, board.GetPiece(new Position(7, 7))?.Type);

            // Check black pieces
            Assert.Equal(PieceType.Rook, board.GetPiece(new Position(0, 0))?.Type);
            Assert.Equal(PieceType.Knight, board.GetPiece(new Position(0, 1))?.Type);
            Assert.Equal(PieceType.Bishop, board.GetPiece(new Position(0, 2))?.Type);
            Assert.Equal(PieceType.Queen, board.GetPiece(new Position(0, 3))?.Type);
            Assert.Equal(PieceType.King, board.GetPiece(new Position(0, 4))?.Type);
            Assert.Equal(PieceType.Bishop, board.GetPiece(new Position(0, 5))?.Type);
            Assert.Equal(PieceType.Knight, board.GetPiece(new Position(0, 6))?.Type);
            Assert.Equal(PieceType.Rook, board.GetPiece(new Position(0, 7))?.Type);

            // Check pawns
            for (int col = 0; col < 8; col++)
            {
                Assert.Equal(PieceType.Pawn, board.GetPiece(new Position(1, col))?.Type);
                Assert.Equal(PieceColor.Black, board.GetPiece(new Position(1, col))?.Color);
                Assert.Equal(PieceType.Pawn, board.GetPiece(new Position(6, col))?.Type);
                Assert.Equal(PieceColor.White, board.GetPiece(new Position(6, col))?.Color);
            }
        }

        [Fact]
        public void PawnMovement_FirstMove_ShouldAllowTwoSquares()
        {
            // Arrange
            var board = new ChessBoard();
            var from = new Position(6, 0); // White pawn starting position

            // Act
            var possibleMoves = board.GetPossibleMoves(from);

            // Assert
            Assert.Contains(new Position(5, 0), possibleMoves); // One square forward
            Assert.Contains(new Position(4, 0), possibleMoves); // Two squares forward
            Assert.Equal(2, possibleMoves.Count); // Only these two moves should be possible
        }

        [Fact]
        public void PawnMovement_AfterFirstMove_ShouldOnlyAllowOneSquare()
        {
            // Arrange
            var board = new ChessBoard();

            // Make first move
            board.MovePiece(new Position(6, 0), new Position(4, 0), null); // White pawn two squares
            board.MovePiece(new Position(1, 1), new Position(3, 1), null); // Black pawn

            // Act
            var possibleMoves = board.GetPossibleMoves(new Position(4, 0));

            // Assert
            Assert.Single(possibleMoves);
            Assert.Contains(new Position(3, 0), possibleMoves);
        }

        [Fact]
        public void PawnCapture_ShouldBeAllowed_WhenEnemyPieceIsDiagonal()
        {
            // Arrange
            var board = new ChessBoard();

            // Move white pawn forward
            board.MovePiece(new Position(6, 3), new Position(4, 3), null);
            // Move black pawn diagonally adjacent
            board.MovePiece(new Position(1, 2), new Position(3, 2), null);

            // Act & Assert
            var possibleMoves = board.GetPossibleMoves(new Position(4, 3));
            Assert.Contains(new Position(3, 2), possibleMoves);
        }

        [Fact]
        public void EnPassant_ShouldBeAllowed_WhenConditionsMet()
        {
            // Arrange
            var board = new ChessBoard();

            // Setup position for en passant
            board.MovePiece(new Position(6, 1), new Position(4, 1), null); // White pawn two squares
            board.MovePiece(new Position(1, 0), new Position(3, 0), null); // Black pawn
            board.MovePiece(new Position(4, 1), new Position(3, 1), null); // White pawn forward
            board.MovePiece(new Position(1, 2), new Position(3, 2), null); // Black pawn two squares

            // Act & Assert
            var possibleMoves = board.GetPossibleMoves(new Position(3, 1));
            Assert.Contains(new Position(2, 2), possibleMoves); // En passant capture should be possible
        }

        [Fact]
        public void Castle_KingSide_ShouldBeAllowed_WhenPathIsClear()
        {
            // Arrange
            var board = new ChessBoard();

            // Clear path for kingside castle
            board.MovePiece(new Position(6, 5), new Position(4, 5), null); // White pawn
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn
            board.MovePiece(new Position(7, 5), new Position(6, 4), null); // White bishop
            board.MovePiece(new Position(0, 6), new Position(2, 5), null); // Black knight
            board.MovePiece(new Position(7, 6), new Position(5, 5), null); // White knight

            // Act & Assert
            var kingMoves = board.GetPossibleMoves(new Position(7, 4));
            Assert.Contains(new Position(7, 6), kingMoves); // Castling move should be possible
        }

        [Fact]
        public void Castle_QueenSide_ShouldBeAllowed_WhenPathIsClear()
        {
            // Arrange
            var board = new ChessBoard();

            // Clear path for queenside castle
            board.MovePiece(new Position(6, 3), new Position(4, 3), null); // White pawn
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn
            board.MovePiece(new Position(7, 2), new Position(6, 3), null); // White bishop
            board.MovePiece(new Position(0, 6), new Position(2, 5), null); // Black knight
            board.MovePiece(new Position(7, 1), new Position(5, 2), null); // White knight
            board.MovePiece(new Position(0, 5), new Position(2, 7), null); // Black bishop
            board.MovePiece(new Position(7, 3), new Position(6, 4), null); // White queen

            // Act & Assert
            var kingMoves = board.GetPossibleMoves(new Position(7, 4));
            Assert.Contains(new Position(7, 2), kingMoves); // Castling move should be possible
        }

        [Fact]
        public void Castle_ShouldNotBeAllowed_AfterKingMoves()
        {
            // Arrange
            var board = new ChessBoard();

            // Setup position
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // White pawn
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn
            board.MovePiece(new Position(7, 4), new Position(6, 4), null); // White king moves
            board.MovePiece(new Position(0, 6), new Position(2, 5), null); // Black knight
            board.MovePiece(new Position(6, 4), new Position(7, 4), null); // White king back

            // Act & Assert
            var kingMoves = board.GetPossibleMoves(new Position(7, 4));
            Assert.DoesNotContain(new Position(7, 6), kingMoves); // Kingside castle should not be possible
            Assert.DoesNotContain(new Position(7, 2), kingMoves); // Queenside castle should not be possible
        }

        [Fact]
        public void Check_ShouldPreventMovesNotBlockingCheck()
        {
            // Arrange
            var board = new ChessBoard();

            // Setup position
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // White pawn
            board.MovePiece(new Position(1, 4), new Position(3, 4), null); // Black pawn
            board.MovePiece(new Position(6, 5), new Position(4, 5), null); // White pawn
            board.MovePiece(new Position(0, 3), new Position(4, 7), null); // Black queen to attack

            // Act & Assert
            Assert.True(board.IsKingInCheck(PieceColor.White));
        }

        [Fact]
        public void Checkmate_ShouldBeDetected()
        {
            // Arrange
            var board = new ChessBoard();
            // Setup a fool's mate position
            board.MovePiece(new Position(6, 5), new Position(5, 5), null); // White f3
            board.MovePiece(new Position(1, 4), new Position(2, 4), null); // Black e6
            board.MovePiece(new Position(6, 6), new Position(4, 6), null); // White g4
            board.MovePiece(new Position(0, 3), new Position(4, 7), null); // Black Qh4#

            // Act & Assert
            Assert.True(board.IsCheckmate(PieceColor.White));
        }

        [Fact]
        public void Stalemate_ShouldBeDetected()
        {
            // Arrange
            var board = new ChessBoard();

            // Setup stalemate position
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // White pawn
            board.MovePiece(new Position(1, 0), new Position(2, 0), null); // Black pawn
            board.MovePiece(new Position(7, 3), new Position(3, 7), null); // White queen
            board.MovePiece(new Position(2, 0), new Position(3, 0), null); // Black pawn
            board.MovePiece(new Position(3, 7), new Position(1, 7), null); // White queen
            board.MovePiece(new Position(0, 4), new Position(1, 5), null); // Black king

            // Act & Assert
            Assert.True(board.IsStalemate(PieceColor.Black));
        }

        [Fact]
        public void PawnPromotion_ShouldBeAllowed_WhenReachingEndRank()
        {
            // Arrange
            var board = new ChessBoard();

            // Setup position for promotion
            board.MovePiece(new Position(6, 0), new Position(4, 0), null); // White pawn forward
            board.MovePiece(new Position(1, 1), new Position(3, 1), null); // Black pawn
            board.MovePiece(new Position(4, 0), new Position(3, 0), null); // White pawn forward
            board.MovePiece(new Position(3, 1), new Position(4, 1), null); // Black pawn
            board.MovePiece(new Position(3, 0), new Position(2, 0), null); // White pawn forward
            board.MovePiece(new Position(4, 1), new Position(5, 1), null); // Black pawn
            board.MovePiece(new Position(2, 0), new Position(1, 0), null); // White pawn forward

            // Act & Assert
            var possibleMoves = board.GetPossibleMoves(new Position(1, 0));
            Assert.Contains(new Position(0, 0), possibleMoves);
        }

        [Fact]
        public void FEN_ShouldGenerateCorrectString_ForInitialPosition()
        {
            // Arrange
            var board = new ChessBoard();

            // Act
            string fen = board.GenerateFEN();

            // Assert
            Assert.Equal("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", fen);
        }

        [Fact]
        public void FEN_ShouldGenerateCorrectString_AfterMoves()
        {
            // Arrange
            var board = new ChessBoard();
            board.MovePiece(new Position(6, 4), new Position(4, 4), null); // e4

            // Act
            string fen = board.GenerateFEN();

            // Assert
            Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", fen);
        }
    }
}