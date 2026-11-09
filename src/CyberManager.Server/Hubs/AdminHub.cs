using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CyberManager.Server.Hubs;

/// <summary>Consola de administración / caja. Requiere JWT.</summary>
[Authorize]
public class AdminHub : Hub
{
    private readonly RealtimeService _realtime;
    public AdminHub(RealtimeService realtime) => _realtime = realtime;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        await base.OnConnectedAsync();
    }

    public Task SendCommand(int stationId, AgentCommandType command, string? argument)
    {
        // Los comandos de energía los ejecuta el agente (servicio de Windows).
        if (command is AgentCommandType.Shutdown or AgentCommandType.Restart or AgentCommandType.Logoff
            or AgentCommandType.KillProcess or AgentCommandType.Screenshot)
            return _realtime.SendAgentCommandAsync(stationId, command, argument);

        // Bloqueo/desbloqueo y mensajes se envían a ambos (estación y agente).
        return Task.WhenAll(
            _realtime.SendAgentCommandAsync(stationId, command, argument),
            _realtime.SendStationCommandAsync(stationId, command, argument));
    }

    /// <summary>Solicita una captura de pantalla de un equipo para el visualizador remoto.</summary>
    public Task RequestScreen(int stationId, int maxWidth, int quality)
        => _realtime.RequestScreenAsync(stationId, maxWidth, quality);

    /// <summary>Reenvía un evento de mouse o teclado a la estación seleccionada.</summary>
    public Task SendRemoteInput(int stationId, RemoteInputEvent input)
        => _realtime.SendRemoteInputAsync(stationId, input);

    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
}
