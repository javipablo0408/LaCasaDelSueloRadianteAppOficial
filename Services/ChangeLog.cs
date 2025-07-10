using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class ChangeLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public string Tabla { get; set; } // "Cliente" o "Servicio"
        public string TipoCambio { get; set; } // "Insert", "Update", "Delete"
        public string EntidadId { get; set; }
        public string DatosJson { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Fuente { get; set; } = "local"; // "local" o "remoto"
    }
}
