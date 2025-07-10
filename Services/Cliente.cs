using SQLite;

namespace LaCasaDelSueloRadianteApp
{
    public class Cliente
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string NombreCliente { get; set; }
        public DateTime FechaModificacionNombre { get; set; }
        public string Direccion { get; set; }
        public DateTime FechaModificacionDireccion { get; set; }
        public string Email { get; set; }
        public DateTime FechaModificacionEmail { get; set; }
        public string Telefono { get; set; }
        public DateTime FechaModificacionTelefono { get; set; }
        public bool IsSynced { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime FechaModificacionEliminado { get; set; }
    }
}