using SQLite;
using System;

namespace LaCasaDelSueloRadianteApp
{
    public class Servicio
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ClienteId { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime FechaModificacionFecha { get; set; }

        public string TipoServicio { get; set; }
        public DateTime FechaModificacionTipoServicio { get; set; }

        public string TipoInstalacion { get; set; }
        public DateTime FechaModificacionTipoInstalacion { get; set; }

        public string FuenteCalor { get; set; }
        public DateTime FechaModificacionFuenteCalor { get; set; }

        public double? ValorPh { get; set; }
        public DateTime FechaModificacionValorPh { get; set; }

        public double? ValorConductividad { get; set; }
        public DateTime FechaModificacionValorConductividad { get; set; }

        public double? ValorConcentracion { get; set; }
        public DateTime FechaModificacionValorConcentracion { get; set; }

        public double? ValorTurbidez { get; set; }
        public DateTime FechaModificacionValorTurbidez { get; set; }

        public string FotoPhUrl { get; set; }
        public DateTime FechaModificacionFotoPhUrl { get; set; }

        public string FotoConductividadUrl { get; set; }
        public DateTime FechaModificacionFotoConductividadUrl { get; set; }

        public string FotoConcentracionUrl { get; set; }
        public DateTime FechaModificacionFotoConcentracionUrl { get; set; }

        public string FotoTurbidezUrl { get; set; }
        public DateTime FechaModificacionFotoTurbidezUrl { get; set; }

        public bool IsSynced { get; set; }
    }
}