using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace PictureSite.Api.Photos;

public sealed class PhotoUpdateHub
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _clients = new();

    public void Add(WebSocket socket) => _clients.TryAdd(socket, new SemaphoreSlim(1, 1));

    public void Remove(WebSocket socket) => _clients.TryRemove(socket, out _);

    public async Task BroadcastPhotoAddedAsync(object photo, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.SerializeToUtf8Bytes(new { type = "photo-added", photo }, WebJsonOptions);
        var staleClients = new List<WebSocket>();

        foreach (var (socket, sendLock) in _clients)
        {
            if (socket.State != WebSocketState.Open)
            {
                staleClients.Add(socket);
                continue;
            }

            try
            {
                await sendLock.WaitAsync(cancellationToken);
                try
                {
                    if (socket.State == WebSocketState.Open)
                        await socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
                    else
                        staleClients.Add(socket);
                }
                finally
                {
                    sendLock.Release();
                }
            }
            catch (Exception exception) when (exception is WebSocketException or ObjectDisposedException)
            {
                staleClients.Add(socket);
            }
        }

        foreach (var socket in staleClients)
            Remove(socket);
    }
}
