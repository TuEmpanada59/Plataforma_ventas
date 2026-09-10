using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Plataforma_ventas.Services
{
    public interface IEmailService
    {
        Task<bool> EnviarAsync(string destinatario, string asunto, string cuerpoHtml);
    }

    /// <summary>
    /// Envío SMTP vía MailKit. Configurable en appsettings ("Smtp": Host, Port, User, Password, From).
    /// Si no hay SMTP configurado devuelve false y el llamador decide el fallback.
    /// </summary>
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var host = _config["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("SMTP no configurado — el correo a {Destinatario} no fue enviado.", destinatario);
                return false;
            }

            try
            {
                int port = int.TryParse(_config["Smtp:Port"], out int p) ? p : 587;
                string user = _config["Smtp:User"] ?? "";
                string password = _config["Smtp:Password"] ?? "";
                string from = _config["Smtp:From"] ?? user;

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(from));
                message.To.Add(MailboxAddress.Parse(destinatario));
                message.Subject = asunto;
                message.Body = new TextPart("html") { Text = cuerpoHtml };

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(user, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando correo a {Destinatario}", destinatario);
                return false;
            }
        }
    }
}
