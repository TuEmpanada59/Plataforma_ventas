namespace Plataforma_ventas.ViewModels
{
    public class AccesoViewModel
    {
        public LoginViewModel Login { get; set; } = new();
        public RegistroViewModel Registro { get; set; } = new();

    }
}