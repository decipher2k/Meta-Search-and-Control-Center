//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Globalization;

namespace MSCC.Localization;

/// <summary>
/// Supported languages.
/// </summary>
public class LanguageInfo
{
    public string Code { get; init; } = "en";
    public string NativeName { get; init; } = "English";
    public string EnglishName { get; init; } = "English";
}

/// <summary>
/// Provides localized strings for the application.
/// Supports runtime language switching.
/// </summary>
public class Strings : INotifyPropertyChanged
{
    private static readonly Strings _instance = new();
    private static Dictionary<string, string> _currentResources;
    private static string _currentLanguage = "en";

    public static Strings Instance => _instance;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// List of all supported languages.
    /// </summary>
    public static List<LanguageInfo> SupportedLanguages { get; } = new()
    {
        new() { Code = "en", NativeName = "English", EnglishName = "English" },
        new() { Code = "de", NativeName = "Deutsch", EnglishName = "German" },
        new() { Code = "es", NativeName = "Espa\u00f1ol", EnglishName = "Spanish" },
        new() { Code = "fr", NativeName = "Fran\u00e7ais", EnglishName = "French" },
        new() { Code = "it", NativeName = "Italiano", EnglishName = "Italian" },
        new() { Code = "hi", NativeName = "\u0939\u093f\u0928\u094d\u0926\u0940", EnglishName = "Hindi" },
        new() { Code = "ja", NativeName = "\u65e5\u672c\u8a9e", EnglishName = "Japanese" },
        new() { Code = "zh", NativeName = "\u4e2d\u6587", EnglishName = "Chinese" },
        new() { Code = "ru", NativeName = "\u0420\u0443\u0441\u0441\u043a\u0438\u0439", EnglishName = "Russian" },
    };

    static Strings()
    {
        _currentResources = Strings_en.Resources;
    }

    /// <summary>
    /// Switches the language at runtime.
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
        _currentResources = languageCode switch
        {
            "de" => Strings_de.Resources,
            "es" => Strings_es.Resources,
            "fr" => Strings_fr.Resources,
            "it" => Strings_it.Resources,
            "hi" => Strings_hi.Resources,
            "ja" => Strings_ja.Resources,
            "zh" => Strings_zh.Resources,
            "ru" => Strings_ru.Resources,
            _ => Strings_en.Resources
        };

        CultureInfo.CurrentUICulture = new CultureInfo(languageCode);
        _instance.OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Returns the current language code.
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;

    private static string GetString(string key)
    {
        return _currentResources.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Returns a formatted string.
    /// </summary>
    public static string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // === Application ===
    public string AppTitle => GetString("AppTitle");
    public string Ready => GetString("Ready");
    public string Error => GetString("Error");
    public string Warning => GetString("Warning");
    public string Success => GetString("Success");
    public string Cancel => GetString("Cancel");
    public string Save => GetString("Save");
    public string Delete => GetString("Delete");
    public string Edit => GetString("Edit");
    public string Add => GetString("Add");
    public string Close => GetString("Close");
    public string Yes => GetString("Yes");
    public string No => GetString("No");
    public string OK => GetString("OK");

    // === Menu ===
    public string MenuFile => GetString("MenuFile");
    public string MenuExit => GetString("MenuExit");
    public string MenuPlugins => GetString("MenuPlugins");
    public string MenuScriptManager => GetString("MenuScriptManager");
    public string MenuReloadScripts => GetString("MenuReloadScripts");
    public string MenuSettings => GetString("MenuSettings");
    public string MenuHelp => GetString("MenuHelp");
    public string MenuAbout => GetString("MenuAbout");

    // === Search ===
    public string Search => GetString("Search");
    public string SearchResults => GetString("SearchResults");
    public string Searching => GetString("Searching");
    public string SearchCompleted => GetString("SearchCompleted");
    public string NoResults => GetString("NoResults");
    public string ResultsFound => GetString("ResultsFound");
    public string Source => GetString("Source");

    // === DataSources ===
    public string DataSources => GetString("DataSources");
    public string NewDataSource => GetString("NewDataSource");
    public string EditDataSource => GetString("EditDataSource");
    public string DeleteDataSource => GetString("DeleteDataSource");
    public string DataSourceName => GetString("DataSourceName");
    public string ConnectorType => GetString("ConnectorType");
    public string Configuration => GetString("Configuration");
    public string Enabled => GetString("Enabled");
    public string NoDataSourcesSelected => GetString("NoDataSourcesSelected");

    // === Groups ===
    public string Groups => GetString("Groups");
    public string NewGroup => GetString("NewGroup");
    public string EditGroup => GetString("EditGroup");
    public string DeleteGroup => GetString("DeleteGroup");
    public string GroupName => GetString("GroupName");
    public string GroupColor => GetString("GroupColor");
    public string NoGroup => GetString("NoGroup");

    // === Details ===
    public string DetailView => GetString("DetailView");
    public string SelectResultForDetails => GetString("SelectResultForDetails");
    public string Open => GetString("Open");
    public string OpenFolder => GetString("OpenFolder");
    public string CopyPath => GetString("CopyPath");

    // === Labels ===
    public string AddLabel => GetString("AddLabel");
    public string SelectResultFirst => GetString("SelectResultFirst");
    public string KeywordSearch => GetString("KeywordSearch");
    public string SearchByKeyword => GetString("SearchByKeyword");
    public string CurrentQuery => GetString("CurrentQuery");
    public string SavedQueries => GetString("SavedQueries");
    public string Load => GetString("Load");
    public string Labels => GetString("Labels");

    // === Script Editor ===
    public string ScriptEditor => GetString("ScriptEditor");
    public string ScriptManager => GetString("ScriptManager");
    public string NewScript => GetString("NewScript");
    public string Compile => GetString("Compile");
    public string Validate => GetString("Validate");
    public string Insert => GetString("Insert");
    public string Errors => GetString("Errors");
    public string Warnings => GetString("Warnings");
    public string Line => GetString("Line");
    public string Column => GetString("Column");
    public string CompileSuccess => GetString("CompileSuccess");
    public string CompileFailed => GetString("CompileFailed");

    // === Settings ===
    public string Settings => GetString("Settings");
    public string Language => GetString("Language");
    public string SelectLanguage => GetString("SelectLanguage");
    public string RestartRequired => GetString("RestartRequired");
    public string General => GetString("General");
    public string Appearance => GetString("Appearance");

    // === Validation ===
    public string FieldRequired => GetString("FieldRequired");
    public string InvalidValue => GetString("InvalidValue");
    public string ValidationError => GetString("ValidationError");

    // === Status ===
    public string Loading => GetString("Loading");
    public string Saving => GetString("Saving");
    public string Compiling => GetString("Compiling");
    public string ScriptConnectorsLoaded => GetString("ScriptConnectorsLoaded");
    public string DataSourceCreated => GetString("DataSourceCreated");
    public string DataSourceUpdated => GetString("DataSourceUpdated");
    public string DataSourceDeleted => GetString("DataSourceDeleted");
    public string GroupCreated => GetString("GroupCreated");
    public string GroupUpdated => GetString("GroupUpdated");
    public string GroupDeleted => GetString("GroupDeleted");

    // === Confirmation dialogs ===
    public string ConfirmDelete => GetString("ConfirmDelete");
    public string ConfirmDeleteDataSource => GetString("ConfirmDeleteDataSource");
    public string ConfirmDeleteGroup => GetString("ConfirmDeleteGroup");

    // === Connector: FileSystem ===
    public string Connector_FileSystem_Name => GetString("Connector_FileSystem_Name");
    public string Connector_FileSystem_Description => GetString("Connector_FileSystem_Description");
    public string Connector_FileSystem_BasePath => GetString("Connector_FileSystem_BasePath");
    public string Connector_FileSystem_BasePath_Desc => GetString("Connector_FileSystem_BasePath_Desc");
    public string Connector_FileSystem_SearchPattern => GetString("Connector_FileSystem_SearchPattern");
    public string Connector_FileSystem_SearchPattern_Desc => GetString("Connector_FileSystem_SearchPattern_Desc");
    public string Connector_FileSystem_IncludeSubdirs => GetString("Connector_FileSystem_IncludeSubdirs");
    public string Connector_FileSystem_IncludeSubdirs_Desc => GetString("Connector_FileSystem_IncludeSubdirs_Desc");
    public string Connector_FileSystem_Open => GetString("Connector_FileSystem_Open");
    public string Connector_FileSystem_Open_Desc => GetString("Connector_FileSystem_Open_Desc");
    public string Connector_FileSystem_OpenFolder => GetString("Connector_FileSystem_OpenFolder");
    public string Connector_FileSystem_OpenFolder_Desc => GetString("Connector_FileSystem_OpenFolder_Desc");
    public string Connector_FileSystem_CopyPath => GetString("Connector_FileSystem_CopyPath");
    public string Connector_FileSystem_CopyPath_Desc => GetString("Connector_FileSystem_CopyPath_Desc");
    public string Connector_FileSystem_Path => GetString("Connector_FileSystem_Path");
    public string Connector_FileSystem_Size => GetString("Connector_FileSystem_Size");
    public string Connector_FileSystem_Created => GetString("Connector_FileSystem_Created");
    public string Connector_FileSystem_Modified => GetString("Connector_FileSystem_Modified");
    public string Connector_FileSystem_Type => GetString("Connector_FileSystem_Type");

    // === Connector: DuckDuckGo ===
    public string Connector_DuckDuckGo_Name => GetString("Connector_DuckDuckGo_Name");
    public string Connector_DuckDuckGo_Description => GetString("Connector_DuckDuckGo_Description");
    public string Connector_DuckDuckGo_MaxResults => GetString("Connector_DuckDuckGo_MaxResults");
    public string Connector_DuckDuckGo_MaxResults_Desc => GetString("Connector_DuckDuckGo_MaxResults_Desc");
    public string Connector_DuckDuckGo_Region => GetString("Connector_DuckDuckGo_Region");
    public string Connector_DuckDuckGo_Region_Desc => GetString("Connector_DuckDuckGo_Region_Desc");
    public string Connector_DuckDuckGo_SafeSearch => GetString("Connector_DuckDuckGo_SafeSearch");
    public string Connector_DuckDuckGo_SafeSearch_Desc => GetString("Connector_DuckDuckGo_SafeSearch_Desc");
    public string Connector_DuckDuckGo_OpenBrowser => GetString("Connector_DuckDuckGo_OpenBrowser");
    public string Connector_DuckDuckGo_OpenBrowser_Desc => GetString("Connector_DuckDuckGo_OpenBrowser_Desc");
    public string Connector_DuckDuckGo_CopyUrl => GetString("Connector_DuckDuckGo_CopyUrl");
    public string Connector_DuckDuckGo_CopyUrl_Desc => GetString("Connector_DuckDuckGo_CopyUrl_Desc");
    public string Connector_DuckDuckGo_SearchMore => GetString("Connector_DuckDuckGo_SearchMore");
    public string Connector_DuckDuckGo_SearchMore_Desc => GetString("Connector_DuckDuckGo_SearchMore_Desc");
    public string Connector_DuckDuckGo_WebResult => GetString("Connector_DuckDuckGo_WebResult");
    public string Connector_DuckDuckGo_Position => GetString("Connector_DuckDuckGo_Position");

    // === Connector: Microsoft 365 ===
    public string Connector_M365_Name => GetString("Connector_M365_Name");
    public string Connector_M365_Description => GetString("Connector_M365_Description");
    public string Connector_M365_ClientId => GetString("Connector_M365_ClientId");
    public string Connector_M365_ClientId_Desc => GetString("Connector_M365_ClientId_Desc");
    public string Connector_M365_TenantId => GetString("Connector_M365_TenantId");
    public string Connector_M365_TenantId_Desc => GetString("Connector_M365_TenantId_Desc");
    public string Connector_M365_SearchCalendar => GetString("Connector_M365_SearchCalendar");
    public string Connector_M365_SearchCalendar_Desc => GetString("Connector_M365_SearchCalendar_Desc");
    public string Connector_M365_SearchToDo => GetString("Connector_M365_SearchToDo");
    public string Connector_M365_SearchToDo_Desc => GetString("Connector_M365_SearchToDo_Desc");
    public string Connector_M365_SearchMail => GetString("Connector_M365_SearchMail");
    public string Connector_M365_SearchMail_Desc => GetString("Connector_M365_SearchMail_Desc");
    public string Connector_M365_SearchOneNote => GetString("Connector_M365_SearchOneNote");
    public string Connector_M365_SearchOneNote_Desc => GetString("Connector_M365_SearchOneNote_Desc");
    public string Connector_M365_MaxDaysBack => GetString("Connector_M365_MaxDaysBack");
    public string Connector_M365_MaxDaysBack_Desc => GetString("Connector_M365_MaxDaysBack_Desc");
    public string Connector_M365_OpenOutlook => GetString("Connector_M365_OpenOutlook");
    public string Connector_M365_OpenOutlook_Desc => GetString("Connector_M365_OpenOutlook_Desc");
    public string Connector_M365_OpenToDo => GetString("Connector_M365_OpenToDo");
    public string Connector_M365_OpenToDo_Desc => GetString("Connector_M365_OpenToDo_Desc");
    public string Connector_M365_OpenOneNote => GetString("Connector_M365_OpenOneNote");
    public string Connector_M365_OpenOneNote_Desc => GetString("Connector_M365_OpenOneNote_Desc");
    public string Connector_M365_CopyLink => GetString("Connector_M365_CopyLink");
    public string Connector_M365_CopyLink_Desc => GetString("Connector_M365_CopyLink_Desc");
    public string Connector_M365_OpenBrowser => GetString("Connector_M365_OpenBrowser");
    public string Connector_M365_OpenBrowser_Desc => GetString("Connector_M365_OpenBrowser_Desc");
    public string Connector_M365_CalendarEntry => GetString("Connector_M365_CalendarEntry");
    public string Connector_M365_Task => GetString("Connector_M365_Task");
    public string Connector_M365_Email => GetString("Connector_M365_Email");
    public string Connector_M365_OneNotePage => GetString("Connector_M365_OneNotePage");
    public string Connector_M365_Subject => GetString("Connector_M365_Subject");
    public string Connector_M365_Start => GetString("Connector_M365_Start");
    public string Connector_M365_End => GetString("Connector_M365_End");
    public string Connector_M365_Location => GetString("Connector_M365_Location");
    public string Connector_M365_Organizer => GetString("Connector_M365_Organizer");
    public string Connector_M365_List => GetString("Connector_M365_List");
    public string Connector_M365_Status => GetString("Connector_M365_Status");
    public string Connector_M365_Priority => GetString("Connector_M365_Priority");
    public string Connector_M365_DueDate => GetString("Connector_M365_DueDate");
    public string Connector_M365_Notes => GetString("Connector_M365_Notes");
    public string Connector_M365_From => GetString("Connector_M365_From");
    public string Connector_M365_Received => GetString("Connector_M365_Received");
    public string Connector_M365_Attachments => GetString("Connector_M365_Attachments");
    public string Connector_M365_Preview => GetString("Connector_M365_Preview");
    public string Connector_M365_Title => GetString("Connector_M365_Title");
    public string Connector_M365_Section => GetString("Connector_M365_Section");

    // === Connector: SQL Database ===
    public string Connector_SQL_Name => GetString("Connector_SQL_Name");
    public string Connector_SQL_Description => GetString("Connector_SQL_Description");
    public string Connector_SQL_ConnectionString => GetString("Connector_SQL_ConnectionString");
    public string Connector_SQL_ConnectionString_Desc => GetString("Connector_SQL_ConnectionString_Desc");
    public string Connector_SQL_DatabaseType => GetString("Connector_SQL_DatabaseType");
    public string Connector_SQL_DatabaseType_Desc => GetString("Connector_SQL_DatabaseType_Desc");
    public string Connector_SQL_Tables => GetString("Connector_SQL_Tables");
    public string Connector_SQL_Tables_Desc => GetString("Connector_SQL_Tables_Desc");
    public string Connector_SQL_CustomQuery => GetString("Connector_SQL_CustomQuery");
    public string Connector_SQL_CustomQuery_Desc => GetString("Connector_SQL_CustomQuery_Desc");
    public string Connector_SQL_CopyJson => GetString("Connector_SQL_CopyJson");
    public string Connector_SQL_CopyJson_Desc => GetString("Connector_SQL_CopyJson_Desc");
    public string Connector_SQL_CopyInsert => GetString("Connector_SQL_CopyInsert");
    public string Connector_SQL_CopyInsert_Desc => GetString("Connector_SQL_CopyInsert_Desc");
    public string Connector_SQL_MatchesIn => GetString("Connector_SQL_MatchesIn");
    public string Connector_SQL_Record => GetString("Connector_SQL_Record");

    // === Connector: OpenAI ===
    public string OpenAiConnectorName => GetString("OpenAiConnectorName");
    public string OpenAiConnectorDescription => GetString("OpenAiConnectorDescription");
    public string OpenAiApiEndpoint => GetString("OpenAiApiEndpoint");
    public string OpenAiApiEndpointDesc => GetString("OpenAiApiEndpointDesc");
    public string OpenAiApiKey => GetString("OpenAiApiKey");
    public string OpenAiApiKeyDesc => GetString("OpenAiApiKeyDesc");
    public string OpenAiModel => GetString("OpenAiModel");
    public string OpenAiModelDesc => GetString("OpenAiModelDesc");
    public string OpenAiSystemPrompt => GetString("OpenAiSystemPrompt");
    public string OpenAiSystemPromptDesc => GetString("OpenAiSystemPromptDesc");
    public string OpenAiMaxTokens => GetString("OpenAiMaxTokens");
    public string OpenAiMaxTokensDesc => GetString("OpenAiMaxTokensDesc");
    public string OpenAiTemperature => GetString("OpenAiTemperature");
    public string OpenAiTemperatureDesc => GetString("OpenAiTemperatureDesc");
    public string OpenAiResponse => GetString("OpenAiResponse");
    public string OpenAiError => GetString("OpenAiError");
    public string OpenAiErrorDesc => GetString("OpenAiErrorDesc");
    public string OpenAiMetaModel => GetString("OpenAiMetaModel");
    public string OpenAiMetaPromptTokens => GetString("OpenAiMetaPromptTokens");
    public string OpenAiMetaCompletionTokens => GetString("OpenAiMetaCompletionTokens");
    public string OpenAiMetaTotalTokens => GetString("OpenAiMetaTotalTokens");
    public string OpenAiMetaFinishReason => GetString("OpenAiMetaFinishReason");
    public string OpenAiMetaQuery => GetString("OpenAiMetaQuery");
    public string OpenAiActionCopyResponse => GetString("OpenAiActionCopyResponse");
    public string OpenAiActionCopyResponseDesc => GetString("OpenAiActionCopyResponseDesc");
    public string OpenAiActionCopyQuery => GetString("OpenAiActionCopyQuery");
    public string OpenAiActionCopyQueryDesc => GetString("OpenAiActionCopyQueryDesc");

    // === Connector: Generic API ===
    public string GenericApiConnectorName => GetString("GenericApiConnectorName");
    public string GenericApiConnectorDescription => GetString("GenericApiConnectorDescription");
    public string GenericApiEndpoint => GetString("GenericApiEndpoint");
    public string GenericApiEndpointDesc => GetString("GenericApiEndpointDesc");
    public string GenericApiHttpMethod => GetString("GenericApiHttpMethod");
    public string GenericApiHttpMethodDesc => GetString("GenericApiHttpMethodDesc");
    public string GenericApiAuthType => GetString("GenericApiAuthType");
    public string GenericApiAuthTypeDesc => GetString("GenericApiAuthTypeDesc");
    public string GenericApiAuthHeaderName => GetString("GenericApiAuthHeaderName");
    public string GenericApiAuthHeaderNameDesc => GetString("GenericApiAuthHeaderNameDesc");
    public string GenericApiAuthHeaderValue => GetString("GenericApiAuthHeaderValue");
    public string GenericApiAuthHeaderValueDesc => GetString("GenericApiAuthHeaderValueDesc");
    public string GenericApiAuthToken => GetString("GenericApiAuthToken");
    public string GenericApiAuthTokenDesc => GetString("GenericApiAuthTokenDesc");
    public string GenericApiOAuth2TokenEndpoint => GetString("GenericApiOAuth2TokenEndpoint");
    public string GenericApiOAuth2TokenEndpointDesc => GetString("GenericApiOAuth2TokenEndpointDesc");
    public string GenericApiOAuth2ClientId => GetString("GenericApiOAuth2ClientId");
    public string GenericApiOAuth2ClientIdDesc => GetString("GenericApiOAuth2ClientIdDesc");
    public string GenericApiOAuth2ClientSecret => GetString("GenericApiOAuth2ClientSecret");
    public string GenericApiOAuth2ClientSecretDesc => GetString("GenericApiOAuth2ClientSecretDesc");
    public string GenericApiOAuth2Scope => GetString("GenericApiOAuth2Scope");
    public string GenericApiOAuth2ScopeDesc => GetString("GenericApiOAuth2ScopeDesc");
    public string GenericApiQueryParameters => GetString("GenericApiQueryParameters");
    public string GenericApiQueryParametersDesc => GetString("GenericApiQueryParametersDesc");
    public string GenericApiPostBody => GetString("GenericApiPostBody");
    public string GenericApiPostBodyDesc => GetString("GenericApiPostBodyDesc");
    public string GenericApiContentType => GetString("GenericApiContentType");
    public string GenericApiContentTypeDesc => GetString("GenericApiContentTypeDesc");
    public string GenericApiCustomHeaders => GetString("GenericApiCustomHeaders");
    public string GenericApiCustomHeadersDesc => GetString("GenericApiCustomHeadersDesc");
    public string GenericApiResultJsonPath => GetString("GenericApiResultJsonPath");
    public string GenericApiResultJsonPathDesc => GetString("GenericApiResultJsonPathDesc");
    public string GenericApiResultTitleProperty => GetString("GenericApiResultTitleProperty");
    public string GenericApiResultTitlePropertyDesc => GetString("GenericApiResultTitlePropertyDesc");
    public string GenericApiResultDescriptionProperty => GetString("GenericApiResultDescriptionProperty");
    public string GenericApiResultDescriptionPropertyDesc => GetString("GenericApiResultDescriptionPropertyDesc");
    public string GenericApiResultUrlProperty => GetString("GenericApiResultUrlProperty");
    public string GenericApiResultUrlPropertyDesc => GetString("GenericApiResultUrlPropertyDesc");
    public string GenericApiTimeout => GetString("GenericApiTimeout");
    public string GenericApiTimeoutDesc => GetString("GenericApiTimeoutDesc");
    public string GenericApiError => GetString("GenericApiError");
    public string GenericApiRawResponse => GetString("GenericApiRawResponse");
    public string GenericApiMetaQuery => GetString("GenericApiMetaQuery");
    public string GenericApiActionOpenUrl => GetString("GenericApiActionOpenUrl");
    public string GenericApiActionOpenUrlDesc => GetString("GenericApiActionOpenUrlDesc");
    public string GenericApiActionCopyJson => GetString("GenericApiActionCopyJson");
    public string GenericApiActionCopyJsonDesc => GetString("GenericApiActionCopyJsonDesc");
    public string GenericApiActionCopyUrl => GetString("GenericApiActionCopyUrl");
    public string GenericApiActionCopyUrlDesc => GetString("GenericApiActionCopyUrlDesc");

    // === Connector: IMAP ===
    public string ImapConnectorName => GetString("ImapConnectorName");
    public string ImapConnectorDescription => GetString("ImapConnectorDescription");
    public string ImapServer => GetString("ImapServer");
    public string ImapServerDesc => GetString("ImapServerDesc");
    public string ImapPort => GetString("ImapPort");
    public string ImapPortDesc => GetString("ImapPortDesc");
    public string ImapEmailAddress => GetString("ImapEmailAddress");
    public string ImapEmailAddressDesc => GetString("ImapEmailAddressDesc");
    public string ImapAuthType => GetString("ImapAuthType");
    public string ImapAuthTypeDesc => GetString("ImapAuthTypeDesc");
    public string ImapPassword => GetString("ImapPassword");
    public string ImapPasswordDesc => GetString("ImapPasswordDesc");
    public string ImapOAuth2Token => GetString("ImapOAuth2Token");
    public string ImapOAuth2TokenDesc => GetString("ImapOAuth2TokenDesc");
    public string ImapEncryption => GetString("ImapEncryption");
    public string ImapEncryptionDesc => GetString("ImapEncryptionDesc");
    public string ImapFolderName => GetString("ImapFolderName");
    public string ImapFolderNameDesc => GetString("ImapFolderNameDesc");
    public string ImapMaxResults => GetString("ImapMaxResults");
    public string ImapMaxResultsDesc => GetString("ImapMaxResultsDesc");
    public string ImapMaxDaysBack => GetString("ImapMaxDaysBack");
    public string ImapMaxDaysBackDesc => GetString("ImapMaxDaysBackDesc");
    public string ImapNoSubject => GetString("ImapNoSubject");
    public string ImapError => GetString("ImapError");
    public string ImapErrorDesc => GetString("ImapErrorDesc");
    public string ImapMetaFrom => GetString("ImapMetaFrom");
    public string ImapMetaFromEmail => GetString("ImapMetaFromEmail");
    public string ImapMetaSubject => GetString("ImapMetaSubject");
    public string ImapMetaDate => GetString("ImapMetaDate");
    public string ImapMetaHasAttachments => GetString("ImapMetaHasAttachments");
    public string ImapMetaFolder => GetString("ImapMetaFolder");
    public string ImapActionCopyBody => GetString("ImapActionCopyBody");
    public string ImapActionCopyBodyDesc => GetString("ImapActionCopyBodyDesc");
    public string ImapActionCopySender => GetString("ImapActionCopySender");
    public string ImapActionCopySenderDesc => GetString("ImapActionCopySenderDesc");

    // === Connector: Find In Files ===
    public string Connector_FindInFiles_Name => GetString("Connector_FindInFiles_Name");
    public string Connector_FindInFiles_Description => GetString("Connector_FindInFiles_Description");
    public string Connector_FindInFiles_BasePath => GetString("Connector_FindInFiles_BasePath");
    public string Connector_FindInFiles_BasePath_Desc => GetString("Connector_FindInFiles_BasePath_Desc");
    public string Connector_FindInFiles_FilePattern => GetString("Connector_FindInFiles_FilePattern");
    public string Connector_FindInFiles_FilePattern_Desc => GetString("Connector_FindInFiles_FilePattern_Desc");
    public string Connector_FindInFiles_IncludeSubdirs => GetString("Connector_FindInFiles_IncludeSubdirs");
    public string Connector_FindInFiles_IncludeSubdirs_Desc => GetString("Connector_FindInFiles_IncludeSubdirs_Desc");
    public string Connector_FindInFiles_UseRegex => GetString("Connector_FindInFiles_UseRegex");
    public string Connector_FindInFiles_UseRegex_Desc => GetString("Connector_FindInFiles_UseRegex_Desc");
    public string Connector_FindInFiles_CaseSensitive => GetString("Connector_FindInFiles_CaseSensitive");
    public string Connector_FindInFiles_CaseSensitive_Desc => GetString("Connector_FindInFiles_CaseSensitive_Desc");
    public string Connector_FindInFiles_MatchesFound => GetString("Connector_FindInFiles_MatchesFound");
    public string Connector_FindInFiles_Path => GetString("Connector_FindInFiles_Path");
    public string Connector_FindInFiles_Directory => GetString("Connector_FindInFiles_Directory");
    public string Connector_FindInFiles_FileName => GetString("Connector_FindInFiles_FileName");
    public string Connector_FindInFiles_Size => GetString("Connector_FindInFiles_Size");
    public string Connector_FindInFiles_Modified => GetString("Connector_FindInFiles_Modified");
    public string Connector_FindInFiles_MatchCount => GetString("Connector_FindInFiles_MatchCount");
    public string Connector_FindInFiles_Matches => GetString("Connector_FindInFiles_Matches");
    public string Connector_FindInFiles_SearchTerm => GetString("Connector_FindInFiles_SearchTerm");
    public string Connector_FindInFiles_Line => GetString("Connector_FindInFiles_Line");
    public string Connector_FindInFiles_Column => GetString("Connector_FindInFiles_Column");
    public string Connector_FindInFiles_FileInfo => GetString("Connector_FindInFiles_FileInfo");
    public string Connector_FindInFiles_MatchesHeader => GetString("Connector_FindInFiles_MatchesHeader");
    public string Connector_FindInFiles_AndMore => GetString("Connector_FindInFiles_AndMore");
    public string Connector_FindInFiles_MoreMatches => GetString("Connector_FindInFiles_MoreMatches");
    public string Connector_FindInFiles_Open => GetString("Connector_FindInFiles_Open");
    public string Connector_FindInFiles_Open_Desc => GetString("Connector_FindInFiles_Open_Desc");
    public string Connector_FindInFiles_OpenFolder => GetString("Connector_FindInFiles_OpenFolder");
    public string Connector_FindInFiles_OpenFolder_Desc => GetString("Connector_FindInFiles_OpenFolder_Desc");
    public string Connector_FindInFiles_CopyPath => GetString("Connector_FindInFiles_CopyPath");
    public string Connector_FindInFiles_CopyPath_Desc => GetString("Connector_FindInFiles_CopyPath_Desc");
    public string Connector_FindInFiles_CopyMatches => GetString("Connector_FindInFiles_CopyMatches");
    public string Connector_FindInFiles_CopyMatches_Desc => GetString("Connector_FindInFiles_CopyMatches_Desc");

    /// <summary>
    /// Indexer for dynamic string access by key.
    /// </summary>
    public string this[string key] => GetString(key);
}
