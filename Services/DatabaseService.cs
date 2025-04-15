using SQLite;

namespace LaCasaDelSueloRadianteApp
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public DatabaseService(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<Servicio>().Wait();
        }

        public Task<List<Servicio>> GetServiciosAsync()
        {
            return _database.Table<Servicio>().ToListAsync();
        }

        public Task<int> GuardarServicioAsync(Servicio servicio)
        {
            if (servicio.Id != 0)
            {
                return _database.UpdateAsync(servicio);
            }
            else
            {
                return _database.InsertAsync(servicio);
            }
        }

        public Task<int> BorrarServicioAsync(Servicio servicio)
        {
            return _database.DeleteAsync(servicio);
        }
    }

    [Table("Servicios")]
    public class Servicio
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string? Direccion { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? TipoServicio { get; set; }
        public string? TipoInstalacion { get; set; }
        public string? FuenteCalor { get; set; }
        public double? ValorPh { get; set; }
        public double? ValorConductividad { get; set; }
        public double? ValorConcentracion { get; set; }
        public double? ValorTurbidez { get; set; }
        public string? FotoPhUrl { get; set; }
        public string? FotoConductividadUrl { get; set; }
        public string? FotoConcentracionUrl { get; set; }
        public string? FotoTurbidezUrl { get; set; }
    }
}