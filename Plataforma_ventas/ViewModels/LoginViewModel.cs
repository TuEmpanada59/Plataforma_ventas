using System.ComponentModel.DataAnnotations;

namespace Plataforma_ventas.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Ingrese su usuario.")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese su contraseña.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
