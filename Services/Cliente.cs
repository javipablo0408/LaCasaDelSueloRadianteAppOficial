using SQLite;

namespace LaCasaDelSueloRadianteApp
{
    public class Cliente
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Direccion { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? TipoServicio { get; set; }
        public string? TipoInstalacion { get; set; }
        public string? FuenteCalor { get; set; }
        public string? Ph { get; set; }
        public string? PhFoto { get; set; }
        public string? Conductividad { get; set; }
        public string? ConductividadFoto { get; set; }
        public string? ConcentracionInhibidor { get; set; }
        public string? ConcentracionInhibidorFoto { get; set; }
        public string? Turbidez { get; set; }
        public string? TurbidezFoto { get; set; }
    }
}