using SQLite;
using System;

namespace LaCasaDelSueloRadianteApp.Models
{
    public class SyncQueue
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Identificador global único para sincronización
        public Guid SyncId { get; set; } = Guid.NewGuid();

        public string DeviceId { get; set; } = string.Empty;
        public string Tabla { get; set; } = string.Empty; // "Cliente", "Servicio"
        public string TipoCambio { get; set; } = string.Empty; // "Insert", "Update", "Delete"
        public string EntidadId { get; set; } = string.Empty;
        public string DatosJson { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ProcesadoPor { get; set; } = string.Empty; // Lista de DeviceIds separados por coma
    }
}