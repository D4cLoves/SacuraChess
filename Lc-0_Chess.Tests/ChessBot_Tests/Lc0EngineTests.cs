using System;
using System.Threading.Tasks;
using Xunit;
using Lc_0_Chess.Models;

namespace Lc_0_Chess.Tests.ChessBot_Tests
{
    public class Lc0EngineTests
    {
        private const string Lc0ExecutablePath = "Lc-0/lc0.exe";
        private const string Lc0WeightsPath = "Lc-0/791556.pb.gz";
        private Lc0Engine _engine;

        public Lc0EngineTests()
        {
            _engine = new Lc0Engine(Lc0ExecutablePath, Lc0WeightsPath);
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }

        [Fact]
        public async Task GetBestMove_FromInitialPosition_ShouldReturnValidMove()
        {
            // Arrange
            string initialPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            int moveTimeMs = 1000;

            // Act
            var result = await _engine.GetBestMoveAsync(initialPosition, moveTimeMs);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasValue);
            Assert.NotNull(result.Value.BestMove);
            Assert.True(IsValidMove(result.Value.BestMove));
            Assert.True(result.Value.ScoreCp is >= -1000 and <= 1000);
        }

        [Fact]
        public async Task GetBestMove_FromMiddleGame_ShouldReturnValidMove()
        {
            // Arrange
            string middleGamePosition = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3";
            int moveTimeMs = 1000;

            // Act
            var result = await _engine.GetBestMoveAsync(middleGamePosition, moveTimeMs);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasValue);
            Assert.NotNull(result.Value.BestMove);
            Assert.True(IsValidMove(result.Value.BestMove));
            Assert.True(result.Value.ScoreCp is >= -1000 and <= 1000);
        }

        [Fact]
        public async Task GetBestMove_FromEndGame_ShouldReturnValidMove()
        {
            // Arrange
            string endGamePosition = "4k3/8/8/8/8/8/4P3/4K3 w - - 0 1";
            int moveTimeMs = 1000;

            // Act
            var result = await _engine.GetBestMoveAsync(endGamePosition, moveTimeMs);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasValue);
            Assert.NotNull(result.Value.BestMove);
            Assert.True(IsValidMove(result.Value.BestMove));
            Assert.True(result.Value.ScoreCp is >= -1000 and <= 1000);
        }

        [Fact]
        public async Task GetBestMove_WithInvalidFEN_ShouldReturnNull()
        {
            // Arrange
            string invalidFen = "invalid/fen/string";
            int moveTimeMs = 1000;

            // Act
            var result = await _engine.GetBestMoveAsync(invalidFen, moveTimeMs);

            // Assert
            Assert.False(result.HasValue);
        }

        [Fact]
        public async Task GetBestMove_WithZeroTime_ShouldReturnQuickMove()
        {
            // Arrange
            string initialPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            int moveTimeMs = 0;

            // Act
            var result = await _engine.GetBestMoveAsync(initialPosition, moveTimeMs);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasValue);
            Assert.NotNull(result.Value.BestMove);
            Assert.True(IsValidMove(result.Value.BestMove));
        }

        private bool IsValidMove(string move)
        {
            if (string.IsNullOrEmpty(move) || move.Length != 4)
                return false;

            // Check if the move format is correct (e.g., "e2e4")
            char fromFile = move[0];
            char fromRank = move[1];
            char toFile = move[2];
            char toRank = move[3];

            return IsValidFile(fromFile) && IsValidRank(fromRank) &&
                   IsValidFile(toFile) && IsValidRank(toRank);
        }

        private bool IsValidFile(char file)
        {
            return file >= 'a' && file <= 'h';
        }

        private bool IsValidRank(char rank)
        {
            return rank >= '1' && rank <= '8';
        }
    }
}