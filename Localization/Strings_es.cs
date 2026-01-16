//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Localization;

/// <summary>
/// Spanische Übersetzungen.
/// </summary>
public static class Strings_es
{
    public static Dictionary<string, string> Resources { get; } = new()
    {
        // Application
        ["AppTitle"] = "MSCC - Centro de Comando de Meta Búsqueda",
        ["Ready"] = "Listo",
        ["Error"] = "Error",
        ["Warning"] = "Advertencia",
        ["Success"] = "Éxito",
        ["Cancel"] = "Cancelar",
        ["Save"] = "Guardar",
        ["Delete"] = "Eliminar",
        ["Edit"] = "Editar",
        ["Add"] = "Añadir",
        ["Close"] = "Cerrar",
        ["Yes"] = "Sí",
        ["No"] = "No",
        ["OK"] = "Aceptar",

        // Menu
        ["MenuFile"] = "Archivo",
        ["MenuExit"] = "Salir",
        ["MenuPlugins"] = "Plugins",
        ["MenuScriptManager"] = "Gestor de Scripts...",
        ["MenuReloadScripts"] = "Recargar Scripts",
        ["MenuSettings"] = "Configuración...",
        ["MenuHelp"] = "Ayuda",
        ["MenuAbout"] = "Acerca de MSCC",

        // Search
        ["Search"] = "Buscar",
        ["SearchResults"] = "Resultados de Búsqueda",
        ["Searching"] = "Buscando...",
        ["SearchCompleted"] = "Búsqueda completada",
        ["NoResults"] = "No se encontraron resultados",
        ["ResultsFound"] = "{0} resultados encontrados",
        ["Source"] = "Fuente",

        // DataSources
        ["DataSources"] = "Fuentes de Datos",
        ["NewDataSource"] = "Nueva Fuente de Datos",
        ["EditDataSource"] = "Editar Fuente de Datos",
        ["DeleteDataSource"] = "Eliminar Fuente de Datos",
        ["DataSourceName"] = "Nombre",
        ["ConnectorType"] = "Tipo de Conector",
        ["Configuration"] = "Configuración",
        ["Enabled"] = "Activado",
        ["NoDataSourcesSelected"] = "No hay fuentes de datos seleccionadas o disponibles",

        // Groups
        ["Groups"] = "Grupos",
        ["NewGroup"] = "Nuevo Grupo",
        ["EditGroup"] = "Editar Grupo",
        ["DeleteGroup"] = "Eliminar Grupo",
        ["GroupName"] = "Nombre",
        ["GroupColor"] = "Color",
        ["NoGroup"] = "(Sin Grupo)",

        // Details
        ["DetailView"] = "Vista de Detalles",
        ["SelectResultForDetails"] = "Seleccione un resultado para ver los detalles.",
        ["Open"] = "Abrir",
        ["OpenFolder"] = "Abrir Carpeta",
        ["CopyPath"] = "Copiar Ruta",

        // Labels
        ["AddLabel"] = "Añadir Etiqueta",
        ["SelectResultFirst"] = "Seleccione primero un resultado",
        ["KeywordSearch"] = "Búsqueda por Palabra Clave",
        ["SearchByKeyword"] = "Buscar por Palabra Clave",
        ["CurrentQuery"] = "Consulta Actual",
        ["SavedQueries"] = "Consultas Guardadas",
        ["Load"] = "Cargar",
        ["Labels"] = "Etiquetas",

        // Script Editor
        ["ScriptEditor"] = "Editor de Scripts",
        ["ScriptManager"] = "Gestor de Scripts",
        ["NewScript"] = "Nuevo Script",
        ["Compile"] = "Compilar",
        ["Validate"] = "Validar",
        ["Insert"] = "Insertar",
        ["Errors"] = "Errores",
        ["Warnings"] = "Advertencias",
        ["Line"] = "Línea",
        ["Column"] = "Columna",
        ["CompileSuccess"] = "Compilación exitosa - Conector registrado",
        ["CompileFailed"] = "Compilación fallida - {0} errores",

        // Settings
        ["Settings"] = "Configuración",
        ["Language"] = "Idioma",
        ["SelectLanguage"] = "Seleccionar Idioma",
        ["RestartRequired"] = "Algunos cambios requieren reiniciar la aplicación.",
        ["General"] = "General",
        ["Appearance"] = "Apariencia",

        // Validation
        ["FieldRequired"] = "El campo '{0}' es obligatorio.",
        ["InvalidValue"] = "Valor inválido",
        ["ValidationError"] = "Error de Validación",

        // Status
        ["Loading"] = "Cargando...",
        ["Saving"] = "Guardando...",
        ["Compiling"] = "Compilando...",
        ["ScriptConnectorsLoaded"] = "{0} conectores de script cargados",
        ["DataSourceCreated"] = "Fuente de datos '{0}' creada",
        ["DataSourceUpdated"] = "Fuente de datos '{0}' actualizada",
        ["DataSourceDeleted"] = "Fuente de datos '{0}' eliminada",
        ["GroupCreated"] = "Grupo '{0}' creado",
        ["GroupUpdated"] = "Grupo '{0}' actualizado",
        ["GroupDeleted"] = "Grupo '{0}' eliminado",

        // Confirmation dialogs
        ["ConfirmDelete"] = "¿Está seguro de que desea eliminar '{0}'?",
        ["ConfirmDeleteDataSource"] = "¿Realmente desea eliminar la fuente de datos '{0}'?",
        ["ConfirmDeleteGroup"] = "¿Realmente desea eliminar el grupo '{0}'?\n\nLas fuentes de datos en este grupo no serán eliminadas.",
    };
}
