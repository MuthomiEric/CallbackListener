# Callback Listener

A simple ASP.NET Core service that receives HTTP callbacks and broadcasts them in real-time via WebSockets.

---

## Endpoints

### Dashboard

GET /

Live UI showing incoming callbacks.

---

### Receive Callback

POST /callback


Example:
```bash
curl -X POST http://localhost:5055/callback \
-H "Content-Type: application/json" \
-d '{"event":"test","amount":100}'
WebSocket
ws://localhost:5055/ws

Real-time stream of callbacks.

Run Locally
dotnet run

Open:

http://localhost:5055
Publish
dotnet publish -c Release -o publish
Windows Service Setup (Auto Start)
1. Copy files
C:\Services\CallbackListener
2. Create service (Auto Start)
sc create CallbackListener binPath= "C:\Services\CallbackListener\CallbackListener.exe" start= auto
3. Start service now
sc start CallbackListener
4. Check status
sc query CallbackListener
Firewall
netsh advfirewall firewall add rule name="Callback Listener" dir=in action=allow protocol=TCP localport=5055
Usage

Send a callback:

curl -X POST http://YOUR_SERVER:5055/callback \
-H "Content-Type: application/json" \
-d '{"type":"test"}'

View live:

http://YOUR_SERVER:5055