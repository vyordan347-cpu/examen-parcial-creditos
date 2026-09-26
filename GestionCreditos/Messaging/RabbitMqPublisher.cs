using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace GestionCreditos.Messaging
{
    public class SolicitudRegistradaMensaje
    {
        public Guid MessageId { get; set; }
        public int SolicitudId { get; set; }
        public string UsuarioId { get; set; } = string.Empty;
        public DateTime FechaEventoUtc { get; set; }
    }

    public class RabbitMqPublisher
    {
        private readonly string _connectionString;
        private readonly string _queueName;
        private readonly ILogger<RabbitMqPublisher> _logger;

        public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
        {
            _connectionString = configuration["RabbitMq:ConnectionString"]
                ?? throw new InvalidOperationException("Falta RabbitMq:ConnectionString");
            _queueName = configuration["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";
            _logger = logger;
        }

        public async Task<bool> PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId)
        {
            var mensaje = new SolicitudRegistradaMensaje
            {
                MessageId = Guid.NewGuid(),
                SolicitudId = solicitudId,
                UsuarioId = usuarioId,
                FechaEventoUtc = DateTime.UtcNow
            };

            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
                using var connection = await factory.CreateConnectionAsync();

                // Habilitar publisher confirms al crear el canal (API de RabbitMQ.Client v7)
                var opciones = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                using var channel = await connection.CreateChannelAsync(opciones);

                await channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                var json = JsonSerializer.Serialize(mensaje);
                var body = Encoding.UTF8.GetBytes(json);

                var props = new BasicProperties
                {
                    Persistent = true, // mensaje persistente en disco
                    ContentType = "application/json"
                };

                // Con publisher confirms habilitados, el "await" espera la confirmación
                // del broker; si el mensaje es rechazado (nack), lanza una excepción.
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _queueName,
                    mandatory: true,
                    basicProperties: props,
                    body: body);

                _logger.LogInformation("Mensaje {MessageId} publicado y confirmado para solicitud {SolicitudId}", mensaje.MessageId, solicitudId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al publicar mensaje para solicitud {SolicitudId}. La notificación no pudo encolarse.", solicitudId);
                return false;
            }
        }
    }
}