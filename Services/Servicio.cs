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

        public string? ProximaVisita { get; set; }
        public DateTime FechaModificacionProximaVisita { get; set; }

        public string ValorTurbidez { get; set; }
        public DateTime FechaModificacionValorTurbidez { get; set; }

        public string FotoPhUrl { get; set; }
        public DateTime FechaModificacionFotoPhUrl { get; set; }

        public string FotoConductividadUrl { get; set; }
        public DateTime FechaModificacionFotoConductividadUrl { get; set; }

        public string FotoConcentracionUrl { get; set; }
        public DateTime FechaModificacionFotoConcentracionUrl { get; set; }

        public string FotoTurbidezUrl { get; set; }
        public DateTime FechaModificacionFotoTurbidezUrl { get; set; }

        // Comentarios
        public string Comentarios { get; set; }
        public DateTime FechaModificacionComentarios { get; set; }

        public string ComentariosInstalador { get; set; }
        public DateTime FechaModificacionComentariosInstalador { get; set; }

        // Hasta 10 fotos de instalación, cada una con su fecha de modificación
        public string FotoInstalacion1Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion1Url { get; set; }
        public string FotoInstalacion2Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion2Url { get; set; }
        public string FotoInstalacion3Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion3Url { get; set; }
        public string FotoInstalacion4Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion4Url { get; set; }
        public string FotoInstalacion5Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion5Url { get; set; }
        public string FotoInstalacion6Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion6Url { get; set; }
        public string FotoInstalacion7Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion7Url { get; set; }
        public string FotoInstalacion8Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion8Url { get; set; }
        public string FotoInstalacion9Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion9Url { get; set; }
        public string FotoInstalacion10Url { get; set; }
        public DateTime FechaModificacionFotoInstalacion10Url { get; set; }
        // === EQUIPAMIENTO UTILIZADO ===
        // Formato sugerido: "Nombre:Cantidad;Nombre2:Cantidad2"
        public string EquipamientoUtilizado { get; set; }
        public DateTime FechaModificacionEquipamientoUtilizado { get; set; }

        // === PRODUCTOS UTILIZADOS POR CATEGORÍA ===

        // Formato: "Nombre:Cantidad;Nombre2:Cantidad2"
        public string InhibidoresUtilizados { get; set; }
        public DateTime FechaModificacionInhibidoresUtilizados { get; set; }

        public string LimpiadoresUtilizados { get; set; }
        public DateTime FechaModificacionLimpiadoresUtilizados { get; set; }

        public string BiocidasUtilizados { get; set; }
        public DateTime FechaModificacionBiocidasUtilizados { get; set; }

        public string AnticongelantesUtilizados { get; set; }
        public DateTime FechaModificacionAnticongelantesUtilizados { get; set; }

        public bool IsSynced { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime FechaModificacionEliminado { get; set; }

        // === NUEVOS CAMPOS ===
        // === NUEVOS CAMPOS ===
        public int? AntiguedadInstalacion { get; set; }
        public DateTime FechaModificacionAntiguedadInstalacion { get; set; }

        public int? AntiguedadAparatoProduccion { get; set; }
        public DateTime FechaModificacionAntiguedadAparatoProduccion { get; set; }

        public string Modelo { get; set; }
        public DateTime FechaModificacionModelo { get; set; }

        public string Marca { get; set; }
        public DateTime FechaModificacionMarca { get; set; }

        public int? UltimaRevision { get; set; }
        public DateTime FechaModificacionUltimaRevision { get; set; }
    }
}