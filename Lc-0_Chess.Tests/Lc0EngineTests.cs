using System;
using System.Threading.Tasks;
using Xunit;
using Lc_0_Chess.Models;

namespace Lc_0_Chess.Tests
{
    public class Lc0EngineTests : IDisposable
    {
        private readonly Lc0Engine _engine;
        private const string TestEnginePath = "Lc-0/lc0.exe";
        private const string TestWeightsPath = "Lc-0/791556.pb.gz";

        public Lc0EngineTests()
        {
            _engine = new Lc0Engine(TestEnginePath, TestWeightsPath);
        }

        [Fact]
        public async Task InitializeAsync_ShouldCompleteSuccessfully()
        {
            // Act & Assert
            await _engine.InitializeAsync();
            // If no exception is thrown, the test passes
        }

        [Fact]
        public async Task GetBestMoveAsync_FromInitialPosition_ShouldReturnValidMove()
        {
            // Arrange
            await _engine.InitializeAsync();
            string initialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

            // Act
            var result = await _engine.GetBestMoveAsync(initialFen, 1000);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Value.BestMove);
            Assert.NotEqual("(none)", result.Value.BestMove);
            Assert.True(IsValidUciMove(result.Value.BestMove));
        }

        [Fact]
        public async Task GetBestMoveAsync_FromMiddleGame_ShouldReturnValidMove()
        {
            // Arrange
            await _engine.InitializeAsync();
            string middleGameFen = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 0 1";

            // Act
            var result = await _engine.GetBestMoveAsync(middleGameFen, 1000);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Value.BestMove);
            Assert.NotEqual("(none)", result.Value.BestMove);
            Assert.True(IsValidUciMove(result.Value.BestMove));
        }

        [Fact]
        public async Task GetBestMoveAsync_FromEndGame_ShouldReturnValidMove()
        {
            // Arrange
            await _engine.InitializeAsync();
            string endGameFen = "4k3/8/8/8/8/8/4P3/4K3 w - - 0 1";

            // Act
            var result = await _engine.GetBestMoveAsync(endGameFen, 1000);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Value.BestMove);
            Assert.NotEqual("(none)", result.Value.BestMove);
            Assert.True(IsValidUciMove(result.Value.BestMove));
        }

        [Fact]
        public async Task GetBestMoveAsync_WithInvalidFen_ShouldHandleError()
        {
            // Arrange
            await _engine.InitializeAsync();
            string invalidFen = "invalid/fen/string";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _engine.GetBestMoveAsync(invalidFen, 1000));
        }

        [Fact]
        public async Task GetBestMoveAsync_WithTimeoutZero_ShouldReturnQuickly()
        {
            // Arrange
            await _engine.InitializeAsync();
            string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

            // Act
            var result = await _engine.GetBestMoveAsync(fen, 1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Value.BestMove);
        }

        [Fact]
        public async Task GetBestMoveAsync_WithMateInOne_ShouldFindMate()
        {
            // Arrange
            await _engine.InitializeAsync();
            // Position where white can mate in one with Qh7#
            string mateInOneFen = "r1bqk1nr/pppp1ppp/2n5/2b5/2B5/5Q2/PPPP1PPP/RNB1K2R w KQkq - 0 1";

            // Act
            var result = await _engine.GetBestMoveAsync(mateInOneFen, 2000);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("f3h7", result.Value.BestMove.ToLower()); // Qh7#
        }

        private bool IsValidUciMove(string move)
        {
            if (string.IsNullOrEmpty(move) || move.Length < 4 || move.Length > 5)
                return false;

            // Check if the move format is correct (e.g., "e2e4" or "e7e8q")
            char fromFile = move[0];
            char fromRank = move[1];
            char toFile = move[2];
            char toRank = move[3];

            bool isValidFile(char c) => c >= 'a' && c <= 'h';
            bool isValidRank(char c) => c >= '1' && c <= '8';

            if (!isValidFile(fromFile) || !isValidFile(toFile) ||
                !isValidRank(fromRank) || !isValidRank(toRank))
                return false;

            // If promotion move
            if (move.Length == 5)
            {
                char promotion = char.ToLower(move[4]);
                if (promotion != 'q' && promotion != 'r' && promotion != 'b' && promotion != 'n')
                    return false;
            }

            return true;
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}