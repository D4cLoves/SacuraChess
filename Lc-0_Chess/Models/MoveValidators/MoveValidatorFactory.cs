using System;
using System.Collections.Generic;

namespace Lc_0_Chess.Models.MoveValidators // Пространство имен Lc_0_Chess
{
    public static class MoveValidatorFactory
    {
        private static readonly Dictionary<PieceType, IMoveValidator> _validators = new Dictionary<PieceType, IMoveValidator>();

        // Статический конструктор для инициализации валидаторов
        static MoveValidatorFactory()
        {
            _validators[PieceType.Pawn] = new PawnMoveValidator();
            _validators[PieceType.Rook] = new RookMoveValidator();
            _validators[PieceType.Knight] = new KnightMoveValidator();
            _validators[PieceType.Bishop] = new BishopMoveValidator();
            _validators[PieceType.Queen] = new QueenMoveValidator();
            _validators[PieceType.King] = new KingMoveValidator();
        }

        public static IMoveValidator GetValidator(PieceType pieceType)
        {
            if (_validators.TryGetValue(pieceType, out var validator))
            {
                return validator;
            }
            // Для отладки лучше явно выбрасывать исключение, если валидатор не найден,
            // вместо возврата null или некоего "дефолтного" валидатора, чтобы сразу видеть проблему.
            throw new NotSupportedException($"No validator registered for piece type {pieceType}");
        }

        // Опциональный метод для динамической регистрации, если потребуется в будущем (сейчас не используется)
        public static void RegisterValidator(PieceType pieceType, IMoveValidator validator)
        {
            _validators[pieceType] = validator ?? throw new ArgumentNullException(nameof(validator));
        }
    }
}