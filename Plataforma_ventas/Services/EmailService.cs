using System.Net;
using System.Net.Mail;

namespace Plataforma_ventas.Services
{
    public interface IEmailService
    {
        Task<bool> EnviarAsync(string destinatario, string asunto, string cuerpoHtml);
    }

    /// <summary>
    /// Envío SMTP configurable vía appsettings ("Smtp": Host, Port, User, Password, From).
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

        /// <summary>
        /// Sends an HTML email via SMTP. Returns true on success, false if SMTP is not
        /// configured (Smtp:Host missing in appsettings) or if an error occurs.
        /// When false is returned the caller is responsible for fallback behaviour
        /// (e.g. writing the recovery link to the application log in development).
        /// Credentials are read from Smtp:User / Smtp:Password in configuration;
        /// never store SMTP credentials in source control.
        /// </summary>
        /// <param name="destinatario">Recipient email address.</param>
        /// <param name="asunto">Email subject line.</param>
        /// <param name="cuerpoHtml">HTML body of the email.</param>
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
                using var client = new SmtpClient(host, int.TryParse(_config["Smtp:Port"], out int p) ? p : 587)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Password"])
                };
                using var msg = new MailMessage(
                    _config["Smtp:From"] ?? _config["Smtp:User"] ?? "no-reply@localhost",
                    destinatario, asunto, cuerpoHtml)
                { IsBodyHtml = true };

                await client.SendMailAsync(msg);
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
