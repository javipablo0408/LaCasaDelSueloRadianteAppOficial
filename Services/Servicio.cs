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
        public string TipoServicio { get; set; }
        public string TipoInstalacion { get; set; }
        public string FuenteCalor { get; set; }

        public double? ValorPh { get; set; }
        public double? ValorConductividad { get; set; }
        public double? ValorConcentracion { get; set; }
        public double? ValorTurbidez { get; set; }

        // URLs de las fotos en OneDrive
        public string FotoPhUrl { get; set; }
        public string FotoConductividadUrl { get; set; }
        public string FotoConcentracionUrl { get; set; }
        public string FotoTurbidezUrl { get; set; }
    }
}