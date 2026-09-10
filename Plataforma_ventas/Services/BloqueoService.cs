using System.Collections.Concurrent;

namespace Plataforma_ventas.Services
{
    /// <summary>Cuenta bloqueada por intentos fallidos, para mostrarla en el panel.</summary>
    public record CuentaBloqueada(string Usuario, DateTime BloqueadaHastaUtc, int MinutosRestantes);

    /// <summary>
    /// Control de intentos fallidos y bloqueo temporal de cuentas.
    /// </summary>
    /// <remarks>
    /// Antes vivía suelto en IMemoryCache, que no permite enumerar claves: no había
    /// forma de saber qué cuentas estaban bloqueadas ni de liberarlas antes de tiempo.
    /// Aquí el estado queda en una estructura consultable.
    /// El estado es por instancia (igual que antes). Si algún día se escala a varias
    /// instancias de App Service, habrá que moverlo a caché distribuida junto con la sesión.
    /// </remarks>
    public interface IBloqueoService
    {
        int MaxIntentos { get; }
        TimeSpan Duracion { get; }

        /// <summary>Indica si la cuenta está bloqueada en este momento.</summary>
        bool EstaBloqueado(string usuario);

        /// <summary>Suma un intento fallido y devuelve el total acumulado.</summary>
        int RegistrarFallo(string usuario);

        /// <summary>Bloquea la cuenta por la duración configurada.</summary>
        void Bloquear(string usuario);

        /// <summary>Limpia intentos y bloqueo tras un ingreso exitoso o un reset de clave.</summary>
        void Limpiar(string usuario);

        /// <summary>Libera manualmente una cuenta bloqueada. Devuelve false si no lo estaba.</summary>
        bool Liberar(string usuario);

        /// <summary>Cuentas bloqueadas ahora mismo, ya depuradas las expiradas.</summary>
        IReadOnlyList<CuentaBloqueada> Listar();
    }

    /// <inheritdoc cref="IBloqueoService"/>
    public class BloqueoService : IBloqueoService
    {
        // usuario en minúsculas → instante (UTC) en que expira el bloqueo
        private readonly ConcurrentDictionary<string, DateTime> _bloqueos = new();
        // usuario en minúsculas → (intentos, instante del último intento)
        private readonly ConcurrentDictionary<string, (int Intentos, DateTime Ultimo)> _intentos = new();

        public int MaxIntentos => 5;
        public TimeSpan Duracion => TimeSpan.FromMinutes(15);

        private static string Norm(string? u) => (u ?? "").Trim().ToLowerInvariant();

        public bool EstaBloqueado(string usuario)
        {
            var k = Norm(usuario);
            if (!_bloqueos.TryGetValue(k, out var hasta)) return false;
            if (DateTime.UtcNow < hasta) return true;
            _bloqueos.TryRemove(k, out _);   // expiró
            return false;
        }

        public int RegistrarFallo(string usuario)
        {
            var k = Norm(usuario);
            var ahora = DateTime.UtcNow;
            var actual = _intentos.AddOrUpdate(k,
                _ => (1, ahora),
                (_, prev) =>
                    // La ventana se reinicia si pasó más tiempo que la duración del bloqueo,
                    // igual que hacía el SlidingExpiration de la caché.
                    (ahora - prev.Ultimo) > Duracion ? (1, ahora) : (prev.Intentos + 1, ahora));
            return actual.Intentos;
        }

        public void Bloquear(string usuario)
        {
            var k = Norm(usuario);
            _bloqueos[k] = DateTime.UtcNow.Add(Duracion);
            _intentos.TryRemove(k, out _);
        }

        public void Limpiar(string usuario)
        {
            var k = Norm(usuario);
            _intentos.TryRemove(k, out _);
            _bloqueos.TryRemove(k, out _);
        }

        public bool Liberar(string usuario)
        {
            var k = Norm(usuario);
            bool estaba = _bloqueos.TryRemove(k, out _);
            _intentos.TryRemove(k, out _);
            return estaba;
        }

        public IReadOnlyList<CuentaBloqueada> Listar()
        {
            var ahora = DateTime.UtcNow;
            var vivas = new List<CuentaBloqueada>();
            foreach (var (usuario, hasta) in _bloqueos.ToArray())
            {
                if (hasta <= ahora) { _bloqueos.TryRemove(usuario, out _); continue; }
                vivas.Add(new CuentaBloqueada(usuario, hasta,
                    (int)Math.Ceiling((hasta - ahora).TotalMinutes)));
            }
            return vivas.OrderBy(c => c.BloqueadaHastaUtc).ToList();
        }
    }
}
