using System.Buffers;
using System.Text;

namespace NFEConsulta.Infrastructure;

/// <summary>
/// Le streams XML com limite explícito para evitar alocações de memória sem controle.
/// </summary>
public static class XmlInputLimits
{
    public const long DefaultMaxXmlBytes = 5 * 1024 * 1024;

    public static void EnsureTextWithinLimit(
        string content,
        long maxBytes = DefaultMaxXmlBytes)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "O limite deve ser positivo.");

        if (Encoding.UTF8.GetByteCount(content) > maxBytes)
            throw new InvalidDataException($"O XML excede o limite permitido de {maxBytes} bytes.");
    }

    public static async Task<MemoryStream> CopyToMemoryAsync(
        Stream source,
        long maxBytes = DefaultMaxXmlBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "O limite deve ser positivo.");

        MemoryStream destination = new(capacity: (int)Math.Min(maxBytes, 64 * 1024));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            long totalBytes = 0;
            while (true)
            {
                int bytesRead = await source
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    throw new InvalidDataException(
                        $"O XML excede o limite permitido de {maxBytes} bytes.");
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
            }

            destination.Position = 0;
            return destination;
        }
        catch
        {
            destination.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<string> ReadTextAsync(
        Stream source,
        long maxBytes = DefaultMaxXmlBytes,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        using MemoryStream buffer = await CopyToMemoryAsync(
            source,
            maxBytes,
            cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(
            buffer,
            encoding ?? Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
