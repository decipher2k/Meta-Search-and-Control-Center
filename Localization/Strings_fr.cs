//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Localization;

/// <summary>
/// Französische Übersetzungen.
/// </summary>
public static class Strings_fr
{
    public static Dictionary<string, string> Resources { get; } = new()
    {
        // Application
        ["AppTitle"] = "MSCC - Centre de Commande de Méta Recherche",
        ["Ready"] = "Prêt",
        ["Error"] = "Erreur",
        ["Warning"] = "Avertissement",
        ["Success"] = "Succès",
        ["Cancel"] = "Annuler",
        ["Save"] = "Enregistrer",
        ["Delete"] = "Supprimer",
        ["Edit"] = "Modifier",
        ["Add"] = "Ajouter",
        ["Close"] = "Fermer",
        ["Yes"] = "Oui",
        ["No"] = "Non",
        ["OK"] = "OK",

        // Menu
        ["MenuFile"] = "Fichier",
        ["MenuExit"] = "Quitter",
        ["MenuPlugins"] = "Plugins",
        ["MenuScriptManager"] = "Gestionnaire de Scripts...",
        ["MenuReloadScripts"] = "Recharger les Scripts",
        ["MenuSettings"] = "Paramètres...",
        ["MenuHelp"] = "Aide",
        ["MenuAbout"] = "À propos de MSCC",

        // Search
        ["Search"] = "Rechercher",
        ["SearchResults"] = "Résultats de Recherche",
        ["Searching"] = "Recherche en cours...",
        ["SearchCompleted"] = "Recherche terminée",
        ["NoResults"] = "Aucun résultat trouvé",
        ["ResultsFound"] = "{0} résultats trouvés",
        ["Source"] = "Source",

        // DataSources
        ["DataSources"] = "Sources de Données",
        ["NewDataSource"] = "Nouvelle Source de Données",
        ["EditDataSource"] = "Modifier la Source de Données",
        ["DeleteDataSource"] = "Supprimer la Source de Données",
        ["DataSourceName"] = "Nom",
        ["ConnectorType"] = "Type de Connecteur",
        ["Configuration"] = "Configuration",
        ["Enabled"] = "Activé",
        ["NoDataSourcesSelected"] = "Aucune source de données sélectionnée ou disponible",

        // Groups
        ["Groups"] = "Groupes",
        ["NewGroup"] = "Nouveau Groupe",
        ["EditGroup"] = "Modifier le Groupe",
        ["DeleteGroup"] = "Supprimer le Groupe",
        ["GroupName"] = "Nom",
        ["GroupColor"] = "Couleur",
        ["NoGroup"] = "(Aucun Groupe)",

        // Details
        ["DetailView"] = "Vue Détaillée",
        ["SelectResultForDetails"] = "Sélectionnez un résultat pour afficher les détails.",
        ["Open"] = "Ouvrir",
        ["OpenFolder"] = "Ouvrir le Dossier",
        ["CopyPath"] = "Copier le Chemin",

        // Labels
        ["AddLabel"] = "Ajouter une Étiquette",
        ["SelectResultFirst"] = "Sélectionnez d'abord un résultat",
        ["KeywordSearch"] = "Recherche par Mot-clé",
        ["SearchByKeyword"] = "Rechercher par Mot-clé",
        ["CurrentQuery"] = "Requête Actuelle",
        ["SavedQueries"] = "Requêtes Enregistrées",
        ["Load"] = "Charger",
        ["Labels"] = "Étiquettes",

        // Script Editor
        ["ScriptEditor"] = "Éditeur de Scripts",
        ["ScriptManager"] = "Gestionnaire de Scripts",
        ["NewScript"] = "Nouveau Script",
        ["Compile"] = "Compiler",
        ["Validate"] = "Valider",
        ["Insert"] = "Insérer",
        ["Errors"] = "Erreurs",
        ["Warnings"] = "Avertissements",
        ["Line"] = "Ligne",
        ["Column"] = "Colonne",
        ["CompileSuccess"] = "Compilation réussie - Connecteur enregistré",
        ["CompileFailed"] = "Échec de la compilation - {0} erreurs",

        // Settings
        ["Settings"] = "Paramètres",
        ["Language"] = "Langue",
        ["SelectLanguage"] = "Sélectionner la Langue",
        ["RestartRequired"] = "Certains changements nécessitent un redémarrage.",
        ["General"] = "Général",
        ["Appearance"] = "Apparence",

        // Validation
        ["FieldRequired"] = "Le champ '{0}' est obligatoire.",
        ["InvalidValue"] = "Valeur invalide",
        ["ValidationError"] = "Erreur de Validation",

        // Status
        ["Loading"] = "Chargement...",
        ["Saving"] = "Enregistrement...",
        ["Compiling"] = "Compilation...",
        ["ScriptConnectorsLoaded"] = "{0} connecteurs de script chargés",
        ["DataSourceCreated"] = "Source de données '{0}' créée",
        ["DataSourceUpdated"] = "Source de données '{0}' mise à jour",
        ["DataSourceDeleted"] = "Source de données '{0}' supprimée",
        ["GroupCreated"] = "Groupe '{0}' créé",
        ["GroupUpdated"] = "Groupe '{0}' mis à jour",
        ["GroupDeleted"] = "Groupe '{0}' supprimé",

        // Confirmation dialogs
        ["ConfirmDelete"] = "Êtes-vous sûr de vouloir supprimer '{0}' ?",
        ["ConfirmDeleteDataSource"] = "Voulez-vous vraiment supprimer la source de données '{0}' ?",
        ["ConfirmDeleteGroup"] = "Voulez-vous vraiment supprimer le groupe '{0}' ?\n\nLes sources de données de ce groupe ne seront pas supprimées.",
    };
}
