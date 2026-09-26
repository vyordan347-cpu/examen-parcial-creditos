using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GestionCreditos.Hubs
{
    [Authorize]
    public class SolicitudesHub : Hub
    {
        
    }
}