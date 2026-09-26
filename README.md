# Gestión de Créditos - Examen Parcial

ASP.NET Core 10 MVC + Identity + EF Core/SQLite + Redis + SignalR + RabbitMQ

## URL desplegada
https://gestion-creditos-sjdt.onrender.com/

## Usuarios de prueba
| Rol | Email | Password |
|---|---|---|
| Analista | analista@tecnogas.com | Analista123! |
| Cliente | cliente@tecnogas.com | Cliente123! |
| Cliente 2 | cliente2@tecnogas.com | Cliente123! |

## Rutas principales
| Ruta | Quién la usa | Qué hace |
|---|---|---|
| `/Identity/Account/Login` | Todos | Iniciar sesión |
| `/Identity/Account/Register` | Todos | Registrar cuenta nueva |
| `/Solicitudes/Mis` | Cliente | Listado de solicitudes propias, con filtros (estado, monto, fechas) |
| `/Solicitudes/Detalle/{id}` | Cliente | Detalle de una solicitud + estado en tiempo real (WebSocket) |
| `/Solicitudes/Crear` | Cliente | Registrar una nueva solicitud de crédito |
| `/Solicitudes/MisNotificaciones` | Cliente | Notificaciones recibidas (vía Cloud MQ) |
| `/Analista` | Analista | Panel: aprobar/rechazar solicitudes Pendientes |
| `/hubs/solicitudes` | Interno | Hub de SignalR (WebSocket), no se visita directamente |

## Correr localmente