using SQLite;

namespace LaCasaDelSueloRadianteApp
{
    public class Cliente
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string NombreCliente { get; set; }
        public string Direccion { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public DateTime FechaModificacion { get; set; } // Marca de tiempo
        public bool IsSynced { get; set; } // Flag de sincronización
    }
}