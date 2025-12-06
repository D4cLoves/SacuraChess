using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Globalization;

namespace Lc_0_Chess.Models
{
    /// <summary>
    /// Обёртка над консольным Leela Chess Zero (lc0.exe).
    /// Отвечает за:
    ///  • запуск внешнего процесса с указанными весами;
    ///  • асинхронное взаимодействие по протоколу UCI (отправка команд, получение ответов);
    ///  • парсинг выходных данных и предоставление высокоуровневых методов «GetBestMove» / «Evaluate».
    /// Класс спроектирован потокобезопасным и использует <see cref="Channel{T}"/> для переброски
    /// вывода/ошибок, а также <see cref="TaskCompletionSource{TResult}"/> – для точного ожидания
    /// ответов на конкретные команды.
    /// </summary>
    public class Lc0Engine : IDisposable
    {
        #region Поля процесса и потоков
        /// <summary>Экземпляр запущенного процесса lc0.</summary>
        private Process _process;
        /// <summary>Поток для записи команд движку.</summary>
        private StreamWriter _writer;
        /// <summary>Поток для чтения стандартного вывода.</summary>
        private StreamReader _reader;
        /// <summary>Канал с необработанным stdout lc0.</summary>
        private readonly Channel<string> _outputChannel;
        /// <summary>Канал stderr (чаще – debug-info от CUDA).</summary>
        private readonly Channel<string> _errorChannel;
        /// <summary>Задачи-потоки, непрерывно читающие stdout/stderr.</summary>
        private Task _outputReaderTask;
        private Task _errorReaderTask;
        /// <summary>Токен для корректного отмены и завершения процесса.</summary>
        private CancellationTokenSource _cts;
        /// <summary>Флаг успешной инициализации UCI.</summary>
        private bool _isInitialized;
        private bool _isDisposed;
        /// <summary>Лок для синхронной записи в stdin (lc0 не любит одновременную запись).</summary>
        private readonly object _writeLock = new();
        /// <summary>Список ожидаемых ответов: ключ – подстрока, которая должна прийти, value – TCS для пробуждения.</summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses;
        /// <summary>Полный путь к lc0.exe.</summary>
        private readonly string _enginePath;
        /// <summary>Полный путь к файлу весов .pb.gz.</summary>
        private readonly string _weightsPath;
        /// <summary>Буфер для накопления multiline-ответов, пока не получен «trigger».</summary>
        private readonly StringBuilder _outputBuffer;
        #endregion

        public Lc0Engine(string enginePath, string weightsPath)
        {
            _enginePath = Path.GetFullPath(enginePath);
            _weightsPath = Path.GetFullPath(weightsPath);
            _outputChannel = Channel.CreateUnbounded<string>();
            _errorChannel = Channel.CreateUnbounded<string>();
            _pendingResponses = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
            _outputBuffer = new StringBuilder();
            _cts = new CancellationTokenSource();

            ValidateFiles();
        }

        /// <summary>
        /// Проверяет наличие исполняемого файла lc0, файла весов и зависимых DLL-ов CUDA.
        /// Выбрасывает <see cref="FileNotFoundException"/>, если какой-то ресурс не найден,
        /// чтобы остановить приложение ещё до запуска процесса.
        /// </summary>
        private void ValidateFiles()
        {
            if (!File.Exists(_enginePath))
                throw new FileNotFoundException($"Не найден исполняемый файл движка: {_enginePath}");

            if (!File.Exists(_weightsPath))
                throw new FileNotFoundException($"Не найден файл весов: {_weightsPath}");

            var engineDir = Path.GetDirectoryName(_enginePath);
            var requiredDlls = new[]
            {
                "cublas64_11.dll",
                "cublasLt64_11.dll",
                "cudart64_110.dll",
                "mimalloc-override.dll",
                "mimalloc-redirect.dll"
            };

            foreach (var dll in requiredDlls)
            {
                var dllPath = Path.Combine(engineDir, dll);
                if (!File.Exists(dllPath))
                    throw new FileNotFoundException($"Не найдена необходимая библиотека: {dll}");
            }
        }

        /// <summary>
        /// Полная асинхронная инициализация движка:
        /// 1. Запускает внешний процесс (<see cref="StartProcessAsync"/>);
        /// 2. Переходит в режим UCI и настраивает параметры (<see cref="InitializeUciAsync"/>).
        /// Повторный вызов безопасен — метод просто вернёт управление, если _isInitialized.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                await StartProcessAsync();
                await InitializeUciAsync();
                _isInitialized = true;
                Log("Движок успешно инициализирован");
            }
            catch (Exception ex)
            {
                Log($"Ошибка инициализации: {ex.Message}");
                await ShutdownAsync();
                throw;
            }
        }

        /// <summary>
        /// Запускает lc0.exe с нужными аргументами и перенаправлением stdin/stdout.
        /// Подписывается на события OutputDataReceived / ErrorDataReceived, чтобы ловить вывод
        /// в реальном времени, и убеждается, что процесс не упал в течение первой секунды.
        /// </summary>
        private async Task StartProcessAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _enginePath,
                Arguments = $"--weights=\"{_weightsPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_enginePath)
            };

            Log($"Запуск процесса: {startInfo.FileName} {startInfo.Arguments}");
            Log($"Рабочая директория: {startInfo.WorkingDirectory}");

            try
            {
                _process = new Process { StartInfo = startInfo };
                _process.OutputDataReceived += (sender, e) => ProcessOutput(e.Data);
                _process.ErrorDataReceived += (sender, e) => ProcessError(e.Data);

                if (!_process.Start())
                    throw new InvalidOperationException("Не удалось запустить процесс движка");

                _writer = _process.StandardInput;
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // Ждем немного, чтобы убедиться, что процесс стартовал успешно
                await Task.Delay(1000);

                if (_process.HasExited)
                    throw new InvalidOperationException($"Процесс неожиданно завершился с кодом {_process.ExitCode}");

                Log("Процесс успешно запущен");
            }
            catch (Exception ex)
            {
                Log($"Ошибка запуска процесса: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Обработчик строк stdout от lc0. Накапливает их в буфере и, когда встречает триггерную
        /// подстроку (<paramref name="_pendingResponses"/>), пробуждает ожидающий Task.
        /// </summary>
        private void ProcessOutput(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            Log($"ВЫВОД: {data}");

            foreach (var pair in _pendingResponses)
            {
                if (data.Contains(pair.Key))
                {
                    _outputBuffer.AppendLine(data);
                    pair.Value.TrySetResult(_outputBuffer.ToString());
                    _outputBuffer.Clear();
                    _pendingResponses.TryRemove(pair.Key, out _);
                    return;
                }
            }
            _outputBuffer.AppendLine(data);
        }

        private void ProcessError(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            Log($"STDERR: {data}");
        }

        #region Инициализация UCI

        private async Task InitializeUciAsync()
        {
            try
            {
                // Отправляем UCI команду и ждем ответа
                var uciResponse = await SendCommandAsync("uci", "uciok", TimeSpan.FromSeconds(30));
                Log($"UCI ответ получен: {uciResponse}");

                // Настраиваем параметры движка
                await SendCommandAsync("setoption name MultiPV value 1", null, TimeSpan.FromSeconds(1));

                // Включаем вывод вероятностей WDL и статистики Policy
                await SendCommandAsync("setoption name UCI_ShowWDL value true", null, TimeSpan.FromSeconds(1));
                await SendCommandAsync("setoption name VerboseMoveStats value true", null, TimeSpan.FromSeconds(1));

                // Проверяем готовность
                var readyResponse = await SendCommandAsync("isready", "readyok", TimeSpan.FromSeconds(30));
                Log($"Ready ответ получен: {readyResponse}");
            }
            catch (Exception ex)
            {
                Log($"Ошибка UCI инициализации: {ex.Message}");
                throw;
            }
        }

        #endregion // Инициализация UCI

        #region Вспомогательные команды (Send/Wait)

        /// <summary>
        /// Универсальная обёртка для отправки любой UCI-команды.
        /// Если указан <paramref name="expectedResponse"/>, метод вернёт весь вывода до
        /// появления этой подстроки либо бросит <see cref="TimeoutException"/> после
        /// <paramref name="timeout"/>.
        /// </summary>
        private async Task<string> SendCommandAsync(string command, string expectedResponse = null, TimeSpan? timeout = null)
        {
            if (_isDisposed || _process?.HasExited == true)
                throw new InvalidOperationException("Движок не запущен или был освобожден");

            var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);
            TaskCompletionSource<string> responseTask = null;

            try
            {
                if (expectedResponse != null)
                {
                    responseTask = new TaskCompletionSource<string>();
                    _pendingResponses[expectedResponse] = responseTask;
                }

                lock (_writeLock)
                {
                    Log($"Отправка команды: {command}");
                    _writer.WriteLine(command);
                    _writer.Flush();
                }

                if (responseTask != null)
                {
                    using var cts = new CancellationTokenSource(actualTimeout);
                    using var registration = cts.Token.Register(() =>
                    {
                        responseTask.TrySetCanceled();
                        _pendingResponses.TryRemove(expectedResponse, out _);
                    });

                    try
                    {
                        return await responseTask.Task;
                    }
                    catch (OperationCanceledException)
                    {
                        var error = $"Таймаут ожидания ответа на команду: {command}. Последний вывод:{Environment.NewLine}{_outputBuffer}";
                        Log($"ОШИБКА: {error}");
                        throw new TimeoutException(error);
                    }
                }

                return string.Empty;
            }
            catch (Exception ex) when (ex is not TimeoutException)
            {
                var error = $"Ошибка при обработке команды {command}: {ex.Message}";
                Log($"ОШИБКА: {error}");
                throw new InvalidOperationException(error, ex);
            }
        }

        #endregion // Вспомогательные команды (Send/Wait)

        #region API высокого уровня

        /// <summary>
        /// Вычисляет лучший ход для позиции <paramref name="fen"/> за отведённое время (ms).
        /// Возвращает кортеж: сам ход в UCI, оценку (centipawns), principal variation и глубину.
        /// Если ход не найден (мат/пат), возвращает <c>null</c>.
        /// </summary>
        public async Task<(string BestMove, double ScoreCp, string Pv, int Depth)?> GetBestMoveAsync(string fen, int thinkingTimeMs)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Движок не инициализирован");

            try
            {
                // Устанавливаем позицию
                await SendCommandAsync($"position fen {fen}", null, TimeSpan.FromSeconds(5));

                // Запускаем поиск
                var response = await SendCommandAsync(
                    $"go movetime {thinkingTimeMs}",
                    "bestmove",
                    TimeSpan.FromMilliseconds(thinkingTimeMs + 5000)
                );

                // Парсим ответ
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var bestMoveLine = lines.FirstOrDefault(l => l.StartsWith("bestmove"));
                if (bestMoveLine == null)
                    return null;

                var parts = bestMoveLine.Split(' ');
                if (parts.Length <= 1 || parts[1] == "(none)")
                    return null;

                // Найдём последнюю строку info со score cp
                double cp = 0;
                bool cpFound = false;
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (line.Contains(" score mate "))
                    {
                        // Пример: info depth 20 ... score mate -3
                        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (int t = 0; t < tokens.Length - 1; t++)
                        {
                            if (tokens[t] == "mate")
                            {
                                if (int.TryParse(tokens[t + 1], out int mateVal))
                                {
                                    // Превращаем матовую дистанцию в условный cp (чем меньше ходов до мата, тем больше значение)
                                    cp = mateVal > 0 ? 10000 : -10000;
                                    cpFound = true;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                    if (line.Contains(" score cp "))
                    {
                        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (int t = 0; t < tokens.Length - 1; t++)
                        {
                            if (tokens[t] == "cp")
                            {
                                if (double.TryParse(tokens[t + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out cp))
                                {
                                    cpFound = true;
                                    break;
                                }
                            }
                        }
                        if (cpFound) break;
                    }
                }

                // Парсим PV (principal variation) и глубину поиска
                int depthVal = 0;
                string pv = "";
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (line.StartsWith("info ") && line.Contains(" depth "))
                    {
                        var toks = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for(int t=0;t<toks.Length-1;t++)
                        {
                            if(toks[t]=="depth" && int.TryParse(toks[t+1], out int depthTok)) { depthVal = depthTok; }
                        }
                    }
                    int pvIndex = line.IndexOf(" pv ");
                    if (pvIndex >= 0)
                    {
                        // Возьмём всё после " pv "
                        pv = line[(pvIndex + 4)..].Trim();
                        break;
                    }
                }

                return (parts[1], cp, pv, depthVal);
            }
            catch (Exception ex)
            {
                Log($"Ошибка получения хода: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Быстрая оценка позиции без вывода лучшего хода.
        /// Удобно, чтобы обновлять «бар оценки» в GUI, не тратя много времени.
        /// </summary>
        public async Task<double?> EvaluateCpAsync(string fen, int thinkingTimeMs = 150)
        {
            if (!_isInitialized)
                return null;

            try
            {
                await SendCommandAsync($"position fen {fen}", null, TimeSpan.FromSeconds(2));

                var response = await SendCommandAsync(
                    $"go movetime {thinkingTimeMs}",
                    "bestmove",
                    TimeSpan.FromMilliseconds(thinkingTimeMs + 6000));

                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                // ищем снизу вверх score cp / mate
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (line.Contains(" score mate "))
                    {
                        // матовые оценки – условно ±10000 cp
                        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (int t = 0; t < tokens.Length - 1; t++)
                        {
                            if (tokens[t] == "mate" && int.TryParse(tokens[t + 1], out int mateVal))
                            {
                                return mateVal > 0 ? 10000 : -10000;
                            }
                        }
                    }
                    if (line.Contains(" score cp "))
                    {
                        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (int t = 0; t < tokens.Length - 1; t++)
                        {
                            if (tokens[t] == "cp" && double.TryParse(tokens[t + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out var cp))
                            {
                                return cp;
                            }
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null; // не критично
            }
        }

        /// <summary>
        /// Запрашивает расширенный анализ (несколько линий PV, статистику policy, WDL).
        /// Используется при нажатии кнопки «Показать план».
        /// </summary>
        public async Task<AnalysisData?> GetAnalysisAsync(string fen, int thinkingTimeMs = 300, int multiPv = 3)
        {
            if (!_isInitialized) return null;

            try
            {
                // Устанавливаем MultiPV, если нужно
                await SendCommandAsync($"setoption name MultiPV value {multiPv}", null, TimeSpan.FromSeconds(1));

                await SendCommandAsync($"position fen {fen}", null, TimeSpan.FromSeconds(3));

                var response = await SendCommandAsync($"go movetime {thinkingTimeMs}", "bestmove", TimeSpan.FromMilliseconds(thinkingTimeMs + 10000));

                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                int depth = 0; double nps = 0; double scoreCp = 0; int? matePly = null; double timeMs=0; string policyStr="";
                double w=0,d=0,l=0;
                var pvDict = new Dictionary<int, PvInfo>();
                var policyLines = new List<string>();

                foreach (var line in lines)
                {
                    if (line.StartsWith("info string"))
                    {
                        // формата: info string e2e4 (123) N: ... (P: 9.81%) ...
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if(parts.Length>2)
                        {
                            string move=parts[2];
                            var pIndex=line.IndexOf("(P:");
                            if(pIndex>0)
                            {
                                int percentStart=pIndex+3; // after '(P:'
                                int percentEnd=line.IndexOf('%', percentStart);
                                if(percentEnd>percentStart)
                                {
                                    string perc=line.Substring(percentStart, percentEnd-percentStart).Trim();
                                    policyLines.Add($"{move}:{perc}%");
                                }
                            }
                        }
                        continue;
                    }

                    if (!line.StartsWith("info ")) continue;

                    var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int multipv = 1;
                    double lineCp = scoreCp;
                    string pvStr = string.Empty;

                    for(int i=0;i<tokens.Length;i++)
                    {
                        switch(tokens[i])
                        {
                            case "depth": if(i+1<tokens.Length && int.TryParse(tokens[i+1],out int depthTok)) depth = Math.Max(depth,depthTok); break;
                            case "nps": if(i+1<tokens.Length && double.TryParse(tokens[i+1], out double nVal)) nps = Math.Max(nps,nVal); break;
                            case "multipv": if(i+1<tokens.Length && int.TryParse(tokens[i+1],out int mVal)) multipv = mVal; break;
                            case "mate": if(i+1<tokens.Length && int.TryParse(tokens[i+1], out int mateVal)) matePly = mateVal; break;
                            case "cp": if(i+1<tokens.Length && double.TryParse(tokens[i+1], out double cpVal)) {
                                lineCp = cpVal;
                                if(multipv==1) scoreCp = cpVal; // основной ход
                            } break;
                            case "time": if(i+1<tokens.Length && double.TryParse(tokens[i+1], out double tVal)) timeMs = tVal; break;
                            case "wdl":
                                if(i+3<tokens.Length && double.TryParse(tokens[i+1], out double wVal) && double.TryParse(tokens[i+2],out double dVal) && double.TryParse(tokens[i+3],out double lVal))
                                {w=wVal; d=dVal; l=lVal;}
                                break;
                            case "pv":
                                pvStr = string.Join(' ', tokens.Skip(i+1));
                                i = tokens.Length; // break loop
                                break;
                            case "policy":
                                policyStr = string.Join(' ', tokens.Skip(i+1));
                                i = tokens.Length;
                                break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(pvStr))
                    {
                        pvDict[multipv] = new PvInfo(multipv, pvStr, lineCp);
                    }
                }

                // Нормализуем WDL в проценты, если сумма >0
                double sumWdl = w + d + l;
                if (sumWdl > 0)
                {
                    w = w / sumWdl * 100.0;
                    d = d / sumWdl * 100.0;
                    l = l / sumWdl * 100.0;
                }

                string policyJoined = policyLines.Any()? string.Join(", ", policyLines.Take(5)) : policyStr;
                return new AnalysisData(depth, scoreCp, matePly, w,d,l, nps, timeMs, policyJoined, pvDict.Values.OrderBy(p=>p.Index).ToList());
            }
            catch { return null; }
        }

        #endregion // API высокого уровня

        #region Завершение и логирование

        private void Log(string message)
        {
            Console.WriteLine($"[Lc0Engine] {message}");
        }

        /// <summary>
        /// Корректно останавливает процесс lc0, отправляя «quit» и ожидая выхода. При таймауте — Kill().
        /// </summary>
        private async Task ShutdownAsync()
        {
            if (_isDisposed)
                return;

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    await SendCommandAsync("quit", null, TimeSpan.FromSeconds(3));
                    if (!_process.WaitForExit(3000))
                        _process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при завершении работы: {ex.Message}");
            }
        }

        /// <summary>
        /// Реализация <see cref="IDisposable"/>. Завершает работу движка и освобождает ресурсы.
        /// После вызова у экземпляра нельзя вызывать какие-либо публичные методы.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cts.Cancel();

            try
            {
                ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch { }

            _cts.Dispose();
            _process?.Dispose();
            _writer?.Dispose();
            _reader?.Dispose();
        }

        #endregion // Завершение и логирование

        public record PvInfo(int Index, string Pv, double ScoreCp);
        public record AnalysisData(int Depth, double ScoreCp, int? MatePly, double Win, double Draw, double Loss, double Nps, double TimeMs, string Policy, List<PvInfo> Lines);
    }
}