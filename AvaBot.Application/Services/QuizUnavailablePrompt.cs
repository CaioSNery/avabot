namespace AvaBot.Application.Services;

internal static class QuizUnavailablePrompt
{
    public static async Task<bool> ConfirmReturnAsync(
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await output.WriteLineAsync(
                "Questionário indisponível: nenhum botão de abertura apareceu em 10 segundos. " +
                "Voltar à página geral de disciplinas e continuar na próxima? [S/N]");
            var answer = await input.ReadLineAsync(cancellationToken);

            if (answer is null)
            {
                await output.WriteLineAsync(
                    "Entrada do console indisponível. Execução pausada; use Ctrl+C para encerrar.");
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (string.Equals(answer?.Trim(), "S", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer?.Trim(), "Sim", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(answer?.Trim(), "N", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer?.Trim(), "Não", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer?.Trim(), "Nao", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
    }
}
