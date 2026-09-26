using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using GestionCreditos.Data;
using GestionCreditos.Models;

namespace GestionCreditos.Messaging
{
    public class RabbitMqConsumerService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitMqConsumerService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMqConsumerService(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<RabbitMqConsumerService> logger)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var habilitado = _configuration.GetValue<bool>("RabbitMq:ConsumerEnabled", true);
            if (!habilitado)
            {
                _logger.LogWarning("El consumidor de RabbitMQ está deshabilitado (RabbitMq:ConsumerEnabled=false).");
                return;
            }

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var habilitado = _configuration.GetValue<bool>("RabbitMq:ConsumerEnabled", true);
            if (!habilitado)
            {
                return;
            }

            var connectionString = _configuration["RabbitMq:ConnectionString"]
                ?? throw new InvalidOperationException("Falta RabbitMq:ConnectionString");
            var queueName = _configuration["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";

            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            // Procesar de a un mensaje a la vez antes de pedir el siguiente
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                await ProcesarMensajeAsync(ea, stoppingToken);
            };

            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            _logger.LogInformation("Consumidor de RabbitMQ iniciado, escuchando la cola '{Queue}'.", queueName);

            // Mantener el servicio vivo mientras la app corra
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcesarMensajeAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMensaje>(json);

                if (mensaje == null || mensaje.MessageId == Guid.Empty)
                {
                    _logger.LogError("Mensaje inválido recibido (no se pudo deserializar o falta MessageId). Se rechaza sin reencolar.");
                    await _channel!.BasicRejectAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Idempotencia: si ya existe una notificación con este MessageId, confirmar sin insertar de nuevo
                var yaExiste = await context.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId, stoppingToken);
                if (yaExiste)
                {
                    _logger.LogInformation("Mensaje {MessageId} ya fue procesado anteriormente. Confirmando sin duplicar.", mensaje.MessageId);
                    await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                    return;
                }

                var notificacion = new Notificacion
                {
                    MessageId = mensaje.MessageId,
                    SolicitudId = mensaje.SolicitudId,
                    UsuarioId = mensaje.UsuarioId,
                    Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                    FechaProcesamientoUtc = DateTime.UtcNow
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync(stoppingToken);

                // Confirmar (ACK manual) SOLO después de guardar exitosamente
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);

                _logger.LogInformation("Notificación guardada y mensaje {MessageId} confirmado (ACK).", mensaje.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al procesar el mensaje. No se confirma como exitoso.");
                // No hacemos ACK ni NACK con requeue=true a propósito, para evitar reintentos infinitos en este ejercicio.
                // Documentamos el reenvío manual con el mismo MessageId en el README.
                await _channel!.BasicRejectAsync(ea.DeliveryTag, requeue: false, stoppingToken);
            }
        }

      public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null) await _channel.CloseAsync(cancellationToken);
            if (_connection != null) await _connection.CloseAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}