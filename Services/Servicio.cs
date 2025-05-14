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

        /// <summary>
        /// Fecha de la última modificación del registro (local o remota).
        /// Debe actualizarse cada vez que se cree o modifique el servicio.
        /// </summary>
        public DateTime FechaModificacion { get; set; }

        /// <summary>
        /// Indica si el registro está sincronizado con la nube.
        /// Debe ponerse en false al crear o modificar el servicio localmente.
        /// </summary>
        public bool IsSynced { get; set; }

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