using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Added for StringBuilder
using Lc_0_Chess.Models.MoveValidators; // This will need to be created or namespace adjusted
// using Chess.Models.MoveValidators; // This will need to be created or namespace adjusted

namespace Lc_0_Chess.Models // Adjusted namespace
{
    /// <summary>
    /// Центральный класс логики игры.
    /// Хранит состояние шахматной доски, историю ходов, контролирует права на рокировку
    /// и инкапсулирует всю игровую механику:
    ///  • генерация возможных ходов (с учётом шахов, рокировок, взятия на проходе),
    ///  • выполнение/откат/повтор ходов,
    ///  • проверка шаха, мата, патовой позиции,
    ///  • преобразование позиции в FEN.
    /// Этот класс используется как «единственный источник истины» для всего UI и для движка LC0.
    /// </summary>
    public class ChessBoard : IBoard
    {
        #region Поля
        /// <summary>
        /// Матрица 8×8, где хранится текущая позиция. Индекс [0,0] соответствует клетке a8.
        /// Элемент равен <c>null</c>, если клетка пуста.
        /// </summary>
        private readonly Piece[,] _board;
        /// <summary>
        /// Флаг, показывающий цвет ходящего игрока. <c>true</c> – сейчас ход белых.
        /// </summary>
        public bool IsWhiteTurn { get; private set; }
        /// <summary>
        /// Координата, на которую возможно взятие «en-passant» после предыдущего хода.
        /// <c>null</c>, если это взятие недоступно.
        /// </summary>
        public Position? EnPassantTarget { get; private set; }
        /// <summary>
        /// Права на рокировку для каждой стороны: <c>(короткая, длинная)</c>.
        /// Изменяются при первом ходе короля или соответствующей ладьи.
        /// </summary>
        private readonly Dictionary<PieceColor, (bool KingSide, bool QueenSide)> _castlingRights;
        /// <summary>
        /// История FEN-строк после каждого хода – необходима для проверки троекратного повторения.
        /// </summary>
        private readonly List<string> _moveHistoryFEN;
        /// <summary>
        /// Детальная история совершённых ходов (<see cref="Move"/>), используется для Undo/Redo.
        /// </summary>
        private readonly List<Move> _moveHistory;
        /// <summary>
        /// Стек «вперёд» для функции Redo (повторить ход).
        /// Пополняется при Undo и очищается при новом ходе.
        /// </summary>
        private readonly Stack<Move> _redoStack;
        /// <summary>
        /// Размер доски. Значение фиксировано (8), выведено в константу для читабельности.
        /// </summary>
        public const int Size = 8; // Used by Position.cs if in same namespace and Position.cs uses ChessBoard.Size
        #endregion // Поля

        #region Инициализация

        public ChessBoard(bool isChess960 = false)
        {
            _board = new Piece[Size, Size];
            _moveHistoryFEN = new List<string>();
            _moveHistory = new List<Move>();
            _redoStack = new Stack<Move>();
            IsWhiteTurn = true;
            EnPassantTarget = null;

            _castlingRights = new Dictionary<PieceColor, (bool, bool)>
            {
                { PieceColor.White, (KingSide: true, QueenSide: true) },
                { PieceColor.Black, (KingSide: true, QueenSide: true) }
            };

            if (isChess960)
            {
                InitializeChess960Board();
            }
            else
            {
                InitializeClassicBoard();
            }
        }

        private void InitializeClassicBoard()
        {
            // Black pieces
            _board[0, 0] = new Piece(PieceColor.Black, PieceType.Rook);
            _board[0, 1] = new Piece(PieceColor.Black, PieceType.Knight);
            _board[0, 2] = new Piece(PieceColor.Black, PieceType.Bishop);
            _board[0, 3] = new Piece(PieceColor.Black, PieceType.Queen);
            _board[0, 4] = new Piece(PieceColor.Black, PieceType.King);
            _board[0, 5] = new Piece(PieceColor.Black, PieceType.Bishop);
            _board[0, 6] = new Piece(PieceColor.Black, PieceType.Knight);
            _board[0, 7] = new Piece(PieceColor.Black, PieceType.Rook);
            for (int c = 0; c < Size; c++) _board[1, c] = new Piece(PieceColor.Black, PieceType.Pawn);

            // White pieces
            _board[7, 0] = new Piece(PieceColor.White, PieceType.Rook);
            _board[7, 1] = new Piece(PieceColor.White, PieceType.Knight);
            _board[7, 2] = new Piece(PieceColor.White, PieceType.Bishop);
            _board[7, 3] = new Piece(PieceColor.White, PieceType.Queen);
            _board[7, 4] = new Piece(PieceColor.White, PieceType.King);
            _board[7, 5] = new Piece(PieceColor.White, PieceType.Bishop);
            _board[7, 6] = new Piece(PieceColor.White, PieceType.Knight);
            _board[7, 7] = new Piece(PieceColor.White, PieceType.Rook);
            for (int c = 0; c < Size; c++) _board[6, c] = new Piece(PieceColor.White, PieceType.Pawn);
        }

        private void InitializeChess960Board()
        {
            // Генерируем случайную позицию для белых фигур
            var position = Chess960Generator.GeneratePosition();

            // Расставляем черные фигуры в зеркальном порядке
            for (int c = 0; c < Size; c++)
            {
                _board[0, c] = new Piece(PieceColor.Black, position[c]);
                _board[1, c] = new Piece(PieceColor.Black, PieceType.Pawn);
            }

            // Расставляем белые фигуры
            for (int c = 0; c < Size; c++)
            {
                _board[7, c] = new Piece(PieceColor.White, position[c]);
                _board[6, c] = new Piece(PieceColor.White, PieceType.Pawn);
            }
        }

        #endregion // Инициализация

        public Piece? GetPiece(Position pos)
        {
            if (!pos.IsValid) return null;
            return _board[pos.Row, pos.Col];
        }

        public bool IsPathClear(Position from, Position to)
        {
            var movingPiece = GetPiece(from);
            if (movingPiece?.Type == PieceType.Knight) return true;

            int dRow = Math.Sign(to.Row - from.Row);
            int dCol = Math.Sign(to.Col - from.Col);

            Position current = new Position(from.Row + dRow, from.Col + dCol);
            while (current != to)
            {
                if (!current.IsValid) return false;
                if (GetPiece(current) != null) return false;
                current = new Position(current.Row + dRow, current.Col + dCol);
            }
            return true;
        }

        #region Генерация ходов

        public List<Position> GetPossibleMoves(Position fromPos)
        {
            var piece = GetPiece(fromPos);
            if (piece == null)
            {
                return new List<Position>();
            }

            var validMoves = new List<Position>();
            var validator = MoveValidatorFactory.GetValidator(piece.Type);

            // Проверяем все возможные позиции на доске
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    var toPos = new Position(r, c);

                    // Пропускаем текущую позицию
                    if (toPos == fromPos) continue;

                    // Проверяем валидность хода
                    if (validator.IsValidMove(fromPos, toPos, this))
                    {
                        // Проверяем, не оставляет ли ход короля под шахом
                        if (!MoveResultsInCheck(fromPos, toPos, piece.Color))
                        {
                            // Для короля проверяем, что это не рокировка через другие фигуры
                            if (piece.Type == PieceType.King && Math.Abs(toPos.Col - fromPos.Col) == 2)
                            {
                                // Проверяем путь, по которому король пришел в эту позицию
                                Position intermediatePos = new Position(fromPos.Row, fromPos.Col + Math.Sign(toPos.Col - fromPos.Col));
                                if (GetPiece(intermediatePos) != null)
                                {
                                    continue; // Пропускаем этот ход, если на пути есть фигура
                                }
                            }
                            validMoves.Add(toPos);
                        }
                    }
                }
            }

            // Добавляем возможность рокировки для короля
            if (piece.Type == PieceType.King && !piece.HasMoved)
            {
                AddCastlingMoves(fromPos, validMoves);
            }

            return validMoves;
        }

        private void AddCastlingMoves(Position kingPos, List<Position> moves)
        {
            var king = GetPiece(kingPos);
            if (king == null || king.Type != PieceType.King || king.HasMoved || IsKingInCheck(king.Color))
            {
                return;
            }

            // Проверяем, что король находится на своей начальной позиции
            int expectedKingRow = king.Color == PieceColor.White ? 7 : 0;
            int expectedKingCol = 4;
            if (kingPos.Row != expectedKingRow || kingPos.Col != expectedKingCol)
            {
                return;
            }

            var rights = _castlingRights[king.Color];
            int kingRow = kingPos.Row;

            if (rights.KingSide)
            {
                Position rookPos = new Position(kingRow, 7);
                var rook = GetPiece(rookPos);
                if (rook != null && rook.Type == PieceType.Rook && !rook.HasMoved)
                {
                    Position f1Pos = new Position(kingRow, 5);
                    Position g1Pos = new Position(kingRow, 6);
                    var f1Piece = GetPiece(f1Pos);
                    var g1Piece = GetPiece(g1Pos);

                    if (f1Piece == null && g1Piece == null)
                    {
                        if (!IsPositionUnderAttack(f1Pos, GetOpponentColor(king.Color)) &&
                            !IsPositionUnderAttack(g1Pos, GetOpponentColor(king.Color)))
                        {
                            moves.Add(g1Pos);
                        }
                    }
                }
            }

            if (rights.QueenSide)
            {
                Position rookPos = new Position(kingRow, 0);
                var rook = GetPiece(rookPos);
                if (rook != null && rook.Type == PieceType.Rook && !rook.HasMoved)
                {
                    Position d1Pos = new Position(kingRow, 3);
                    Position c1Pos = new Position(kingRow, 2);
                    Position b1Pos = new Position(kingRow, 1);
                    var d1Piece = GetPiece(d1Pos);
                    var c1Piece = GetPiece(c1Pos);
                    var b1Piece = GetPiece(b1Pos);

                    if (d1Piece == null && c1Piece == null && b1Piece == null)
                    {
                        if (!IsPositionUnderAttack(d1Pos, GetOpponentColor(king.Color)) &&
                            !IsPositionUnderAttack(c1Pos, GetOpponentColor(king.Color)))
                        {
                            moves.Add(c1Pos);
                        }
                    }
                }
            }
        }

        #endregion // Генерация ходов

        #region Выполнение ходов

        public List<Position> MovePiece(Position from, Position to, PieceType? promotionType = null)
        {
            var pieceToMove = GetPiece(from);
            if (pieceToMove == null) throw new InvalidOperationException("На исходной клетке нет фигуры.");
            if (pieceToMove.Color != (IsWhiteTurn ? PieceColor.White : PieceColor.Black))
                throw new InvalidOperationException("Ход другого цвета.");

            Console.WriteLine($"ХОД: Перемещаем {pieceToMove.Type} из {from.ToAlgebraic()} в {to.ToAlgebraic()}");
            Console.WriteLine($"ХОД: Состояние HasMoved до хода: {pieceToMove.HasMoved}");

            var possibleMoves = GetPossibleMoves(from);
            if (!possibleMoves.Contains(to))
                throw new InvalidOperationException($"Недопустимый ход: {from} -> {to} для {pieceToMove}");

            var changedPositions = new List<Position> { from, to };
            var capturedPiece = GetPiece(to);
            Position? oldEnPassantTarget = EnPassantTarget;
            bool isEnPassant = false;
            bool isCastling = false;
            Position? castlingRookFrom = null;
            Position? castlingRookTo = null;
            bool wasFirstMove = !pieceToMove.HasMoved;
            Console.WriteLine($"ХОД: Это первый ход фигуры? {wasFirstMove}");

            EnPassantTarget = null;

            // Создаем новую фигуру для перемещения
            var movedPiece = new Piece(pieceToMove.Color, pieceToMove.Type);
            if (!wasFirstMove)
            {
                movedPiece.MarkAsMoved();
            }

            // Проверяем рокировку
            if (pieceToMove.Type == PieceType.King && Math.Abs(to.Col - from.Col) == 2)
            {
                // Проверяем права на рокировку
                var rights = _castlingRights[pieceToMove.Color];
                bool isKingSide = to.Col > from.Col;

                if ((isKingSide && !rights.KingSide) || (!isKingSide && !rights.QueenSide))
                {
                    throw new InvalidOperationException("Недопустимая рокировка: нет прав на рокировку в эту сторону");
                }

                // Проверяем, что король находится на своей начальной позиции
                int expectedKingRow = pieceToMove.Color == PieceColor.White ? 7 : 0;
                int expectedKingCol = 4;
                if (from.Row != expectedKingRow || from.Col != expectedKingCol)
                {
                    throw new InvalidOperationException("Рокировка возможна только с начальной позиции короля");
                }

                // Проверяем наличие ладьи и что она не двигалась
                int rookFromCol = isKingSide ? 7 : 0;
                Position rookPos = new Position(from.Row, rookFromCol);
                var rook = GetPiece(rookPos);
                if (rook == null || rook.Type != PieceType.Rook || rook.HasMoved)
                {
                    throw new InvalidOperationException("Недопустимая рокировка: ладья отсутствует или уже ходила");
                }

                Console.WriteLine("ХОД: Выполняется рокировка");
                isCastling = true;
                int rookToCol = isKingSide ? 5 : 3;
                castlingRookFrom = rookPos;
                castlingRookTo = new Position(from.Row, rookToCol);

                // Создаем новую ладью с правильным состоянием
                var movedRook = new Piece(rook.Color, rook.Type);
                movedRook.MarkAsMoved();

                _board[castlingRookTo.Value.Row, castlingRookTo.Value.Col] = movedRook;
                _board[castlingRookFrom.Value.Row, castlingRookFrom.Value.Col] = null;
                changedPositions.Add(castlingRookFrom.Value);
                changedPositions.Add(castlingRookTo.Value);
            }
            else if (pieceToMove.Type == PieceType.Pawn)
            {
                // Обработка взятия на проходе
                if (to == oldEnPassantTarget && capturedPiece == null)
                {
                    Console.WriteLine("ХОД: Выполняется взятие на проходе");
                    isEnPassant = true;
                    Position capturedPawnPos = new Position(from.Row, to.Col);
                    capturedPiece = GetPiece(capturedPawnPos);
                    _board[capturedPawnPos.Row, capturedPawnPos.Col] = null;
                    changedPositions.Add(capturedPawnPos);
                }
                // Установка позиции для взятия на проходе при ходе на два поля
                else if (Math.Abs(to.Row - from.Row) == 2)
                {
                    Console.WriteLine("ХОД: Ход пешкой на два поля, устанавливаем позицию для взятия на проходе");
                    EnPassantTarget = new Position((from.Row + to.Row) / 2, from.Col);
                }
            }

            // Выполняем основной ход
            _board[to.Row, to.Col] = movedPiece;
            _board[from.Row, from.Col] = null;
            movedPiece.MarkAsMoved();

            // Обработка превращения пешки
            if (pieceToMove.Type == PieceType.Pawn && (to.Row == 0 || to.Row == Size - 1))
            {
                if (!promotionType.HasValue)
                    throw new InvalidOperationException("Требуется указать тип фигуры для превращения пешки.");
                Console.WriteLine($"ХОД: Превращение пешки в {promotionType.Value}");
                _board[to.Row, to.Col] = new Piece(movedPiece.Color, promotionType.Value);
            }

            // Обновляем права на рокировку
            UpdateCastlingRights(movedPiece, from);
            if (capturedPiece != null && capturedPiece.Type == PieceType.Rook)
            {
                UpdateCastlingRightsForCapturedRook(capturedPiece, to);
            }

            // Сохраняем ход в истории
            var move = new Move(from, to, movedPiece, capturedPiece, promotionType,
                               isEnPassant, isCastling, castlingRookFrom, castlingRookTo,
                               wasFirstMove);
            _moveHistory.Add(move);
            Console.WriteLine($"ХОД: Сохранен в истории. WasFirstMove = {move.WasFirstMove}");
            _redoStack.Clear();

            IsWhiteTurn = !IsWhiteTurn;
            return changedPositions.Distinct().ToList();
        }

        private void UpdateCastlingRights(Piece movedPiece, Position fromPos)
        {
            PieceColor color = movedPiece.Color;
            var currentRights = _castlingRights[color];

            if (movedPiece.Type == PieceType.King)
            {
                _castlingRights[color] = (KingSide: false, QueenSide: false);
            }
            else if (movedPiece.Type == PieceType.Rook)
            {
                int initialRookColKingSide = 7;
                int initialRookColQueenSide = 0;
                int initialRookRow = (color == PieceColor.White) ? 7 : 0;

                if (fromPos.Row == initialRookRow)
                {
                    if (fromPos.Col == initialRookColKingSide && currentRights.KingSide)
                    {
                        _castlingRights[color] = (KingSide: false, QueenSide: currentRights.QueenSide);
                    }
                    else if (fromPos.Col == initialRookColQueenSide && currentRights.QueenSide)
                    {
                         _castlingRights[color] = (KingSide: currentRights.KingSide, QueenSide: false);
                    }
                }
            }
        }

        private void UpdateCastlingRightsForCapturedRook(Piece capturedRook, Position capturePos)
        {
            PieceColor opponentColor = GetOpponentColor(capturedRook.Color);
            var currentOpponentRights = _castlingRights[opponentColor]; // Corrected: use opponentColor to fetch rights

            int initialRookColKingSide = 7;
            int initialRookColQueenSide = 0;
            // This should be based on the capturedRook's color to determine its original row
            int initialRookRow = (capturedRook.Color == PieceColor.White) ? 7 : 0;

             if (capturePos.Row == initialRookRow)
            {
                // The rights to update are for the capturedRook's color
                if (capturePos.Col == initialRookColKingSide && _castlingRights[capturedRook.Color].KingSide)
                {
                     _castlingRights[capturedRook.Color] = (KingSide: false, QueenSide: _castlingRights[capturedRook.Color].QueenSide);
                }
                else if (capturePos.Col == initialRookColQueenSide && _castlingRights[capturedRook.Color].QueenSide)
                {
                     _castlingRights[capturedRook.Color] = (KingSide: _castlingRights[capturedRook.Color].KingSide, QueenSide: false);
                }
            }
        }

        private void PerformCastle(Position kingFrom, Position kingTo, List<Position> changedPositions)
        {
            var king = GetPiece(kingFrom);
            _board[kingTo.Row, kingTo.Col] = king;
            _board[kingFrom.Row, kingFrom.Col] = null;

            int rookFromCol, rookToCol;
            if (kingTo.Col > kingFrom.Col)
            {
                rookFromCol = 7;
                rookToCol = kingTo.Col - 1;
            }
            else
            {
                rookFromCol = 0;
                rookToCol = kingTo.Col + 1;
            }
            var rook = GetPiece(new Position(kingFrom.Row, rookFromCol));
            _board[kingFrom.Row, rookToCol] = rook;
            _board[kingFrom.Row, rookFromCol] = null;
            rook.MarkAsMoved();
            changedPositions.Add(new Position(kingFrom.Row, rookFromCol));
            changedPositions.Add(new Position(kingFrom.Row, rookToCol));
            Console.WriteLine($"ИГРА: Рокировка {king.Color}. Король: {kingFrom}->{kingTo}, Ладья: ({kingFrom.Row},{rookFromCol})->({kingFrom.Row},{rookToCol})."); // Logger.Log
        }

        private void PerformEnPassantCapture(Position pawnFrom, Position pawnTo, List<Position> changedPositions)
        {
            var pawn = GetPiece(pawnFrom);
            _board[pawnTo.Row, pawnTo.Col] = pawn;
            _board[pawnFrom.Row, pawnFrom.Col] = null;

            int capturedPawnRow = pawnFrom.Row;
            int capturedPawnCol = pawnTo.Col;

            Position capturedPawnPos = new Position(capturedPawnRow, capturedPawnCol);
            Console.WriteLine($"ИГРА: Взятие на проходе {pawn.Color} пешкой: {pawnFrom} -> {pawnTo}. Захвачена пешка на {capturedPawnPos}."); // Logger.Log

            _board[capturedPawnRow, capturedPawnCol] = null;
            changedPositions.Add(capturedPawnPos);
        }

        #endregion // Выполнение ходов

        #region Проверка позиции

        public bool IsKingInCheck(PieceColor kingColor)
        {
            Position? kingPos = FindKing(kingColor);
            if (!kingPos.HasValue)
            {
                Console.WriteLine($"ПРЕДУПРЕЖДЕНИЕ: Король {kingColor} не найден! Невозможно проверить шах."); // Logger.Log
                return false;
            }
            bool isUnderAttack = IsPositionUnderAttack(kingPos.Value, GetOpponentColor(kingColor));
            if (isUnderAttack) Console.WriteLine($"ИГРА: Король {kingColor} под шахом на {kingPos.Value}."); // Logger.Log
            return isUnderAttack;
        }

        public Position? FindKing(PieceColor color)
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    var piece = _board[r, c];
                    if (piece != null && piece.Type == PieceType.King && piece.Color == color)
                    {
                        return new Position(r, c);
                    }
                }
            }
            return null;
        }

        private bool IsPositionUnderAttack(Position targetPos, PieceColor attackingColor)
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    var attackerPos = new Position(r, c);
                    var attackerPiece = GetPiece(attackerPos);
                    if (attackerPiece != null && attackerPiece.Color == attackingColor)
                    {
                        // Placeholder for MoveValidatorFactory
                        var validator = MoveValidatorFactory.GetValidator(attackerPiece.Type);
                        if (validator.IsValidMove(attackerPos, targetPos, this))
                        {
                            if (attackerPiece.Type == PieceType.Pawn)
                            {
                                int direction = attackerPiece.Color == PieceColor.White ? -1 : 1;
                                if (targetPos.Row - attackerPos.Row == direction && Math.Abs(targetPos.Col - attackerPos.Col) == 1)
                                {
                                   return true;
                                }
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool MoveResultsInCheck(Position from, Position to, PieceColor movingPlayerColor)
        {
            Piece pieceToMove = GetPiece(from);
            Piece capturedPiece = GetPiece(to);
            bool wasEnPassantTargetValid = EnPassantTarget.HasValue; // Renamed for clarity
            Position? originalEnPassantTarget = EnPassantTarget;
            // PieceColor originalTurn = IsWhiteTurn ? PieceColor.White : PieceColor.Black; // Not strictly needed for this check

            _board[to.Row, to.Col] = pieceToMove;
            _board[from.Row, from.Col] = null;

            Piece tempRemovedEnPassantPawn = null;
            Position? tempRemovedEnPassantPawnPos = null;
            if (pieceToMove.Type == PieceType.Pawn && to == originalEnPassantTarget && capturedPiece == null)
            {
                tempRemovedEnPassantPawnPos = new Position(from.Row, to.Col);
                tempRemovedEnPassantPawn = GetPiece(tempRemovedEnPassantPawnPos.Value); // This will be null before GetPiece, bug from original
                _board[tempRemovedEnPassantPawnPos.Value.Row, tempRemovedEnPassantPawnPos.Value.Col] = null;
            }

            bool leavesKingInCheck = IsKingInCheck(movingPlayerColor);

            _board[from.Row, from.Col] = pieceToMove;
            _board[to.Row, to.Col] = capturedPiece;

            if (tempRemovedEnPassantPawn != null && tempRemovedEnPassantPawnPos.HasValue) // tempRemovedEnPassantPawn will likely be null
            {
                 _board[tempRemovedEnPassantPawnPos.Value.Row, tempRemovedEnPassantPawnPos.Value.Col] = tempRemovedEnPassantPawn;
            }

            if(wasEnPassantTargetValid) EnPassantTarget = originalEnPassantTarget;

            return leavesKingInCheck;
        }

        public bool IsCheckmate(PieceColor kingColor)
        {
            if (!IsKingInCheck(kingColor)) return false;
            return !HasAnyLegalMoves(kingColor);
        }

        public bool IsStalemate(PieceColor kingColor)
        {
            // Если король под шахом, это не пат
            if (IsKingInCheck(kingColor)) return false;

            // Проверяем, есть ли хоть один возможный ход
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    var currentPos = new Position(r, c);
                    var piece = GetPiece(currentPos);

                    // Проверяем только фигуры указанного цвета
                    if (piece != null && piece.Color == kingColor)
                    {
                        // Получаем все возможные ходы для текущей фигуры
                        var moves = GetPossibleMoves(currentPos);
                        if (moves.Any())
                        {
                            // Если есть хотя бы один возможный ход, это не пат
                            return false;
                        }
                    }
                }
            }

            // Если не найдено ни одного возможного хода, это пат
            Console.WriteLine($"ИГРА: Обнаружен пат для {kingColor}. Нет возможных ходов.");
            return true;
        }

        private bool HasAnyLegalMoves(PieceColor color)
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    var currentPos = new Position(r, c);
                    var piece = GetPiece(currentPos);
                    if (piece != null && piece.Color == color)
                    {
                        if (GetPossibleMoves(currentPos).Any())
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private PieceColor GetOpponentColor(PieceColor playerColor)
        {
            return playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
        }

        public Piece this[int row, int col] => _board[row, col];
        public Piece this[Position pos] => _board[pos.Row, pos.Col];

        #endregion // Проверка позиции

        #region FEN и история ходов

        public string GenerateFEN()
        {
            var fen = new System.Text.StringBuilder();
            for(int r = 0; r < Size; r++)
            {
                int empty = 0;
                for(int c = 0; c < Size; c++)
                {
                    var p = _board[r,c];
                    if (p == null) empty++;
                    else
                    {
                        if (empty > 0) fen.Append(empty);
                        empty = 0;
                        char pieceChar = p.Type switch {
                            PieceType.Pawn => 'p', PieceType.Rook => 'r', PieceType.Knight => 'n',
                            PieceType.Bishop => 'b', PieceType.Queen => 'q', PieceType.King => 'k', _ => ' '
                        };
                        fen.Append(p.Color == PieceColor.White ? char.ToUpper(pieceChar) : pieceChar);
                    }
                }
                if (empty > 0) fen.Append(empty);
                if (r < Size -1) fen.Append('/');
            }
            fen.Append(IsWhiteTurn ? " w" : " b");

            // Генерируем права на рокировку
            string castlingFEN = "";
            var whiteRights = _castlingRights[PieceColor.White];
            var blackRights = _castlingRights[PieceColor.Black];

            // Проверяем белого короля
            var whiteKingPos = FindKing(PieceColor.White);
            if (whiteKingPos.HasValue)
            {
                var whiteKing = GetPiece(whiteKingPos.Value);
                // Проверяем, что король на начальной позиции и не ходил
                if (whiteKing != null && !whiteKing.HasMoved && whiteKingPos.Value.Row == 7 && whiteKingPos.Value.Col == 4)
                {
                    // Проверяем белые ладьи
                    var whiteKingsideRook = GetPiece(new Position(7, 7));
                    var whiteQueensideRook = GetPiece(new Position(7, 0));

                    if (whiteRights.KingSide && whiteKingsideRook?.Type == PieceType.Rook && !whiteKingsideRook.HasMoved)
                        castlingFEN += "K";
                    if (whiteRights.QueenSide && whiteQueensideRook?.Type == PieceType.Rook && !whiteQueensideRook.HasMoved)
                        castlingFEN += "Q";
                }
            }

            // Проверяем черного короля
            var blackKingPos = FindKing(PieceColor.Black);
            if (blackKingPos.HasValue)
            {
                var blackKing = GetPiece(blackKingPos.Value);
                // Проверяем, что король на начальной позиции и не ходил
                if (blackKing != null && !blackKing.HasMoved && blackKingPos.Value.Row == 0 && blackKingPos.Value.Col == 4)
                {
                    // Проверяем черные ладьи
                    var blackKingsideRook = GetPiece(new Position(0, 7));
                    var blackQueensideRook = GetPiece(new Position(0, 0));

                    if (blackRights.KingSide && blackKingsideRook?.Type == PieceType.Rook && !blackKingsideRook.HasMoved)
                        castlingFEN += "k";
                    if (blackRights.QueenSide && blackQueensideRook?.Type == PieceType.Rook && !blackQueensideRook.HasMoved)
                        castlingFEN += "q";
                }
            }

            fen.Append(string.IsNullOrEmpty(castlingFEN) ? " -" : $" {castlingFEN}");
            fen.Append(EnPassantTarget.HasValue ? $" {EnPassantTarget.Value.ToAlgebraic()}" : " -");
            fen.Append(" 0 1");
            return fen.ToString();
        }

        private string GetMoveNotation(Move move)
        {
            // Здесь должна быть логика преобразования объекта Move в строку (например, UCI или SAN)
            var piece = move.MovedPiece;
            var from = move.From;
            var to = move.To;

            // Специальные случаи
            if (move.IsCastling)
            {
                return to.Col > from.Col ? "O-O" : "O-O-O";
            }

            // Получаем символ фигуры
            string pieceSymbol = piece.Type switch
            {
                PieceType.King => "K",
                PieceType.Queen => "Q",
                PieceType.Rook => "R",
                PieceType.Bishop => "B",
                PieceType.Knight => "N",
                PieceType.Pawn => "",
                _ => ""
            };

            // Для пешки добавляем взятие
            if (piece.Type == PieceType.Pawn && move.CapturedPiece != null)
            {
                return $"{from.ToAlgebraic()[0]}x{to.ToAlgebraic()}";
            }

            // Добавляем символ взятия
            string captureSymbol = move.CapturedPiece != null ? "x" : "";

            // Добавляем превращение пешки
            string promotionSymbol = move.PromotionType.HasValue ? $"={move.PromotionType.Value}" : "";

            // Собираем нотацию
            return $"{pieceSymbol}{captureSymbol}{to.ToAlgebraic()}{promotionSymbol}";
        }

        #endregion // FEN и история ходов

        #region История ходов (Undo/Redo)

        public bool CanUndoMove() => _moveHistory.Count > 0;
        public bool CanRedoMove() => _redoStack.Count > 0;

        // Позволяет взглянуть на следующий ход в стеке повтора, не извлекая его.
        public Move? PeekRedoMove()
        {
            return _redoStack.Count > 0 ? _redoStack.Peek() : null;
        }

        public List<Position> UndoMove()
        {
            if (!CanUndoMove())
                throw new InvalidOperationException("Нет ходов для отмены.");

            var move = _moveHistory.Last();
            _moveHistory.RemoveAt(_moveHistory.Count - 1);
            Console.WriteLine($"ОТМЕНА ХОДА: Отменяем ход {move.From.ToAlgebraic()}->{move.To.ToAlgebraic()}");
            Console.WriteLine($"ОТМЕНА ХОДА: Это был первый ход фигуры? {move.WasFirstMove}");
            Console.WriteLine($"ОТМЕНА ХОДА: Тип фигуры: {move.MovedPiece.Type}, Цвет: {move.MovedPiece.Color}");

            var changedPositions = new List<Position> { move.From, move.To };

            // Возвращаем фигуру на исходную позицию с правильным состоянием HasMoved
            var newPiece = new Piece(move.MovedPiece.Color, move.MovedPiece.Type);
            if (!move.WasFirstMove)
            {
                Console.WriteLine("ОТМЕНА ХОДА: Помечаем фигуру как походившую, так как это не был её первый ход");
                newPiece.MarkAsMoved();
            }
            else
            {
                Console.WriteLine("ОТМЕНА ХОДА: Оставляем HasMoved = false, так как это был первый ход фигуры");
            }
            _board[move.From.Row, move.From.Col] = newPiece;
            Console.WriteLine($"ОТМЕНА ХОДА: Состояние HasMoved после восстановления: {newPiece.HasMoved}");

            // Восстанавливаем взятую фигуру, если была
            if (move.IsEnPassant)
            {
                Console.WriteLine("ОТМЕНА ХОДА: Восстанавливаем взятую на проходе пешку");
                _board[move.From.Row, move.To.Col] = move.CapturedPiece;
                _board[move.To.Row, move.To.Col] = null;
                changedPositions.Add(new Position(move.From.Row, move.To.Col));
            }
            else
            {
                _board[move.To.Row, move.To.Col] = move.CapturedPiece;
                if (move.CapturedPiece != null)
                {
                    Console.WriteLine($"ОТМЕНА ХОДА: Восстанавливаем взятую фигуру {move.CapturedPiece.Type}");
                }
            }

            // Восстанавливаем позиции ладьи при рокировке
            if (move.IsCastling && move.CastlingRookFrom.HasValue && move.CastlingRookTo.HasValue)
            {
                Console.WriteLine("ОТМЕНА ХОДА: Отменяем рокировку");
                var rook = GetPiece(move.CastlingRookTo.Value);
                if (rook != null)
                {
                    var newRook = new Piece(rook.Color, rook.Type);
                    if (!move.WasFirstMove)
                    {
                        Console.WriteLine("ОТМЕНА ХОДА: Помечаем ладью как походившую");
                        newRook.MarkAsMoved();
                    }
                    _board[move.CastlingRookFrom.Value.Row, move.CastlingRookFrom.Value.Col] = newRook;
                    _board[move.CastlingRookTo.Value.Row, move.CastlingRookTo.Value.Col] = null;
                    changedPositions.Add(move.CastlingRookFrom.Value);
                    changedPositions.Add(move.CastlingRookTo.Value);
                }
            }

            IsWhiteTurn = !IsWhiteTurn;
            _redoStack.Push(move);

            // Проверяем состояние фигуры после всех операций
            var finalPiece = GetPiece(move.From);
            Console.WriteLine($"ОТМЕНА ХОДА: Финальное состояние HasMoved: {finalPiece?.HasMoved}");
            Console.WriteLine($"ОТМЕНА ХОДА: Текущий ход: {(IsWhiteTurn ? "белых" : "черных")}");

            return changedPositions.Distinct().ToList();
        }

        public List<Position> RedoMove()
        {
            if (!CanRedoMove())
                throw new InvalidOperationException("Нет ходов для повтора.");

            var move = _redoStack.Pop();
            var changedPositions = new List<Position> { move.From, move.To };

            // Создаем новую фигуру с правильным состоянием HasMoved
            var newPiece = new Piece(move.MovedPiece.Color, move.MovedPiece.Type);
            if (!move.WasFirstMove)
            {
                newPiece.MarkAsMoved();
            }

            // Перемещаем фигуру на новую позицию
            _board[move.To.Row, move.To.Col] = newPiece;
            _board[move.From.Row, move.From.Col] = null;

            // Помечаем фигуру как походившую после перемещения
            newPiece.MarkAsMoved();

            // Обрабатываем взятие на проходе
            if (move.IsEnPassant)
            {
                Position capturedPawnPos = new Position(move.From.Row, move.To.Col);
                _board[capturedPawnPos.Row, capturedPawnPos.Col] = null;
                changedPositions.Add(capturedPawnPos);
            }

            // Обрабатываем рокировку
            if (move.IsCastling && move.CastlingRookFrom.HasValue && move.CastlingRookTo.HasValue)
            {
                var newRook = new Piece(move.MovedPiece.Color, PieceType.Rook);
                if (!move.WasFirstMove)
                {
                    newRook.MarkAsMoved();
                }
                _board[move.CastlingRookTo.Value.Row, move.CastlingRookTo.Value.Col] = newRook;
                _board[move.CastlingRookFrom.Value.Row, move.CastlingRookFrom.Value.Col] = null;
                newRook.MarkAsMoved();
                changedPositions.Add(move.CastlingRookFrom.Value);
                changedPositions.Add(move.CastlingRookTo.Value);
            }

            // Обрабатываем превращение пешки
            if (move.PromotionType.HasValue)
            {
                _board[move.To.Row, move.To.Col] = new Piece(move.MovedPiece.Color, move.PromotionType.Value);
            }

            // Сохраняем ход в истории
            _moveHistory.Add(move);

            // Меняем очередь хода
            IsWhiteTurn = !IsWhiteTurn;

            return changedPositions.Distinct().ToList();
        }

        public List<Position> UndoAllMoves()
        {
            var changedPositions = new HashSet<Position>();
            while (CanUndoMove())
            {
                var positions = UndoMove();
                foreach (var pos in positions)
                {
                    changedPositions.Add(pos);
                }
            }
            return changedPositions.ToList();
        }

        public List<Position> RedoAllMoves()
        {
            var changedPositions = new HashSet<Position>();
            while (CanRedoMove())
            {
                var positions = RedoMove();
                foreach (var pos in positions)
                {
                    changedPositions.Add(pos);
                }
            }
            return changedPositions.ToList();
        }

        public List<string> GetMoveHistory()
        {
            // Возвращает строковое представление ходов для отображения
            return _moveHistory.Select(GetMoveNotation).ToList();
        }

        public List<Move> GetFullMoveHistory()
        {
            return new List<Move>(_moveHistory);
        }

        #endregion // История ходов
    }
}