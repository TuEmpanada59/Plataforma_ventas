using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Plataforma_ventas.ViewModels
{
    public class LoginViewModel
    {
        // El campo del formulario se llama "usuario" en minúscula; el enlace de
        // modelos no distingue mayúsculas, así que enlaza sin ayuda.
        [Required(ErrorMessage = "Ingrese su usuario.")]
        public string Usuario { get; set; } = string.Empty;

        // Aquí sí hace falta decirlo: la pantalla de ingreso llama al campo
        // "contrasena" y la propiedad se llama Password. Se mapea el nombre en
        // lugar de renombrar el campo, porque ese nombre lo fija login.js, que
        // lo lee para validar antes de enviar. Tocarlo obligaría a editar un
        // archivo que llega hecho y que se va a volver a recibir actualizado.
        [Required(ErrorMessage = "Ingrese su contraseña.")]
        [DataType(DataType.Password)]
        [FromForm(Name = "contrasena")]
        public string Password { get; set; } = string.Empty;
    }
}
