//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Localization;

/// <summary>
/// Italienische Übersetzungen.
/// </summary>
public static class Strings_it
{
    public static Dictionary<string, string> Resources { get; } = new()
    {
        // Application
        ["AppTitle"] = "MSCC - Centro di Comando Meta Ricerca",
        ["Ready"] = "Pronto",
        ["Error"] = "Errore",
        ["Warning"] = "Avviso",
        ["Success"] = "Successo",
        ["Cancel"] = "Annulla",
        ["Save"] = "Salva",
        ["Delete"] = "Elimina",
        ["Edit"] = "Modifica",
        ["Add"] = "Aggiungi",
        ["Close"] = "Chiudi",
        ["Yes"] = "Sì",
        ["No"] = "No",
        ["OK"] = "OK",

        // Menu
        ["MenuFile"] = "File",
        ["MenuExit"] = "Esci",
        ["MenuPlugins"] = "Plugin",
        ["MenuScriptManager"] = "Gestore Script...",
        ["MenuReloadScripts"] = "Ricarica Script",
        ["MenuSettings"] = "Impostazioni...",
        ["MenuHelp"] = "Aiuto",
        ["MenuAbout"] = "Informazioni su MSCC",

        // Search
        ["Search"] = "Cerca",
        ["SearchResults"] = "Risultati della Ricerca",
        ["Searching"] = "Ricerca in corso...",
        ["SearchCompleted"] = "Ricerca completata",
        ["NoResults"] = "Nessun risultato trovato",
        ["ResultsFound"] = "{0} risultati trovati",
        ["Source"] = "Fonte",

        // DataSources
        ["DataSources"] = "Origini Dati",
        ["NewDataSource"] = "Nuova Origine Dati",
        ["EditDataSource"] = "Modifica Origine Dati",
        ["DeleteDataSource"] = "Elimina Origine Dati",
        ["DataSourceName"] = "Nome",
        ["ConnectorType"] = "Tipo di Connettore",
        ["Configuration"] = "Configurazione",
        ["Enabled"] = "Abilitato",
        ["NoDataSourcesSelected"] = "Nessuna origine dati selezionata o disponibile",

        // Groups
        ["Groups"] = "Gruppi",
        ["NewGroup"] = "Nuovo Gruppo",
        ["EditGroup"] = "Modifica Gruppo",
        ["DeleteGroup"] = "Elimina Gruppo",
        ["GroupName"] = "Nome",
        ["GroupColor"] = "Colore",
        ["NoGroup"] = "(Nessun Gruppo)",

        // Details
        ["DetailView"] = "Vista Dettagli",
        ["SelectResultForDetails"] = "Seleziona un risultato per visualizzare i dettagli.",
        ["Open"] = "Apri",
        ["OpenFolder"] = "Apri Cartella",
        ["CopyPath"] = "Copia Percorso",

        // Labels
        ["AddLabel"] = "Aggiungi Etichetta",
        ["SelectResultFirst"] = "Seleziona prima un risultato",
        ["KeywordSearch"] = "Ricerca per Parola Chiave",
        ["SearchByKeyword"] = "Cerca per Parola Chiave",
        ["CurrentQuery"] = "Query Attuale",
        ["SavedQueries"] = "Query Salvate",
        ["Load"] = "Carica",
        ["Labels"] = "Etichette",

        // Script Editor
        ["ScriptEditor"] = "Editor Script",
        ["ScriptManager"] = "Gestore Script",
        ["NewScript"] = "Nuovo Script",
        ["Compile"] = "Compila",
        ["Validate"] = "Valida",
        ["Insert"] = "Inserisci",
        ["Errors"] = "Errori",
        ["Warnings"] = "Avvisi",
        ["Line"] = "Riga",
        ["Column"] = "Colonna",
        ["CompileSuccess"] = "Compilazione riuscita - Connettore registrato",
        ["CompileFailed"] = "Compilazione fallita - {0} errori",

        // Settings
        ["Settings"] = "Impostazioni",
        ["Language"] = "Lingua",
        ["SelectLanguage"] = "Seleziona Lingua",
        ["RestartRequired"] = "Alcune modifiche richiedono un riavvio.",
        ["General"] = "Generale",
        ["Appearance"] = "Aspetto",

        // Validation
        ["FieldRequired"] = "Il campo '{0}' è obbligatorio.",
        ["InvalidValue"] = "Valore non valido",
        ["ValidationError"] = "Errore di Validazione",

        // Status
        ["Loading"] = "Caricamento...",
        ["Saving"] = "Salvataggio...",
        ["Compiling"] = "Compilazione...",
        ["ScriptConnectorsLoaded"] = "{0} connettori script caricati",
        ["DataSourceCreated"] = "Origine dati '{0}' creata",
        ["DataSourceUpdated"] = "Origine dati '{0}' aggiornata",
        ["DataSourceDeleted"] = "Origine dati '{0}' eliminata",
        ["GroupCreated"] = "Gruppo '{0}' creato",
        ["GroupUpdated"] = "Gruppo '{0}' aggiornato",
        ["GroupDeleted"] = "Gruppo '{0}' eliminato",

        // Confirmation dialogs
        ["ConfirmDelete"] = "Sei sicuro di voler eliminare '{0}'?",
        ["ConfirmDeleteDataSource"] = "Vuoi davvero eliminare l'origine dati '{0}'?",
        ["ConfirmDeleteGroup"] = "Vuoi davvero eliminare il gruppo '{0}'?\n\nLe origini dati in questo gruppo non verranno eliminate.",
    };
}
