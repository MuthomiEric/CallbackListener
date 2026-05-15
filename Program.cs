using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting.WindowsServices;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};

var builder = WebApplication.CreateBuilder(options);

builder.Host.UseWindowsService();

builder.WebHost.UseUrls("http://0.0.0.0:5055");

var app = builder.Build();

var sockets = new List<WebSocket>();

app.UseWebSockets();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";

    await context.Response.WriteAsync("""
<!DOCTYPE html>
<html>
<head>
    <title>Callback Listener</title>

    <style>

        body {
            font-family: Arial, sans-serif;
            margin: 30px;
            background: #111;
            color: white;
        }

        h1 {
            margin-bottom: 10px;
        }

        .topbar {
            display: flex;
            gap: 10px;
            align-items: center;
            margin-bottom: 20px;
        }

        #status {
            padding: 5px 10px;
            border-radius: 6px;
            background: #333;
        }

        button {
            background: #222;
            color: white;
            border: 1px solid #444;
            padding: 8px 12px;
            border-radius: 6px;
            cursor: pointer;
        }

        button:hover {
            background: #333;
        }

        #container {
            display: flex;
            flex-direction: column;
            gap: 20px;
        }

        .card {
            background: #1b1b1b;
            border: 1px solid #333;
            border-radius: 10px;
            padding: 20px;
        }

        .time {
            color: #00ff99;
            margin-bottom: 15px;
            font-size: 14px;
        }

        pre {
            margin: 0;
            overflow: auto;
            white-space: pre-wrap;
            word-wrap: break-word;
            color: #00ff00;
        }

        .empty {
            color: #888;
        }

    </style>
</head>
<body>

<h1>Live Callback Listener</h1>

<div class="topbar">

    <div id="status">
        Connecting...
    </div>

    <button onclick="clearCallbacks()">
        Clear
    </button>

</div>

<div id="container">

    <div class="empty" id="empty">
        Waiting for callbacks...
    </div>

</div>

<script>

const container =
    document.getElementById("container");

const statusElement =
    document.getElementById("status");

const emptyElement =
    document.getElementById("empty");

function clearCallbacks() {

    container.innerHTML = "";

    container.appendChild(emptyElement);

    emptyElement.style.display = "block";
}

function addCallback(data) {

    emptyElement.style.display = "none";

    const card =
        document.createElement("div");

    card.className = "card";

    const time =
        document.createElement("div");

    time.className = "time";

    time.textContent =
        new Date().toLocaleString();

    const pre =
        document.createElement("pre");

    pre.textContent =
        JSON.stringify(data, null, 2);

    card.appendChild(time);

    card.appendChild(pre);

    container.prepend(card);
}

function connectWebSocket() {

    const protocol =
        location.protocol === "https:"
        ? "wss"
        : "ws";

    const ws =
        new WebSocket(
            `${protocol}://${location.host}/ws`
        );

    ws.onopen = () => {

        statusElement.textContent =
            "Connected";

        statusElement.style.background =
            "#14532d";
    };

    ws.onmessage = (event) => {

        const data =
            JSON.parse(event.data);

        addCallback(data);
    };

    ws.onclose = () => {

        statusElement.textContent =
            "Disconnected - reconnecting...";

        statusElement.style.background =
            "#7f1d1d";

        setTimeout(connectWebSocket, 2000);
    };

    ws.onerror = () => {

        ws.close();
    };
}

connectWebSocket();

</script>

</body>
</html>
""");
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket =
        await context.WebSockets.AcceptWebSocketAsync();

    sockets.Add(socket);

    var buffer = new byte[1024];

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result =
                await socket.ReceiveAsync(
                    buffer,
                    CancellationToken.None);

            if (result.MessageType ==
                WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
    catch
    {
    }
    finally
    {
        sockets.Remove(socket);

        try
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Closed",
                CancellationToken.None);
        }
        catch
        {
        }
    }
});

app.MapPost("/callback", async context =>
{
    using var reader =
        new StreamReader(context.Request.Body);

    var body =
        await reader.ReadToEndAsync();

    object parsedBody;

    try
    {
        parsedBody =
            JsonSerializer.Deserialize<object>(body)!;
    }
    catch
    {
        parsedBody = body;
    }

    var payload = new
    {
        timestamp = DateTime.UtcNow,
        method = context.Request.Method,
        path = context.Request.Path,
        query = context.Request.Query.ToDictionary(
            x => x.Key,
            x => x.Value.ToString()),
        headers = context.Request.Headers.ToDictionary(
            x => x.Key,
            x => x.Value.ToString()),
        body = parsedBody
    };

    var json =
        JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

    var bytes =
        Encoding.UTF8.GetBytes(json);

    var deadSockets =
        new List<WebSocket>();

    foreach (var socket in sockets.ToList())
    {
        try
        {
            if (socket.State != WebSocketState.Open)
            {
                deadSockets.Add(socket);
                continue;
            }

            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch
        {
            deadSockets.Add(socket);
        }
    }

    foreach (var deadSocket in deadSockets)
    {
        sockets.Remove(deadSocket);
    }

    await context.Response.WriteAsJsonAsync(new
    {
        success = true,
        connectedClients = sockets.Count
    });
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("Callback Listener started successfully");
});

app.Run();