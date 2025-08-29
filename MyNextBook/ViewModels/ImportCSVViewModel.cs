using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using Microsoft.Maui.Storage;

using CommonCode.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using ImportSeries;
using ImportSeries.Models;
using ImportSeries.Services;
using DevExpress.Maui.DataGrid;

using MyNextBook.Helpers;
using MyNextBook.Models;
using MyNextBook.Services;
using MyNextBook.Views;

namespace MyNextBook.ViewModels
{
    public partial class ImportCSVViewModel : ObservableObject
    {
        // Use null-forgiving so the compiler knows we'll assign in ctor (prevents CS8618 if ctor throws early)
        private readonly IOpenLibraryService _openLibraryService = null!;
        private readonly IPendingTransactionService _transactionService = null!;

        private const string ImportStateFileName = "import_state.json";
        private string ImportStateFilePath => Path.Combine(FileSystem.AppDataDirectory, ImportStateFileName);

        public ImportCSVViewModel(IOpenLibraryService openLibraryService, IPendingTransactionService transactionService)
        {
            // Assign first (so even if later code throws, fields are initialized)
            _openLibraryService = openLibraryService ?? throw new ArgumentNullException(nameof(openLibraryService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));

            // Initialize non-null observable-backed fields to avoid CS8618
            popupDetails = new ShowPopUpDetails();
            importProgressText = "Importing CSV";

            try
            {
                IsBusy = false;
                ShowInitial = true;
                ShowImporting = false;
                ShowImport = false;

                WeakReferenceMessenger.Default.Register<ImportProgressMessage>(this, async (r, m) =>
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            Debug.WriteLine($"Progress: {m.Progress} - {m.Text}");
                            ImportProgressValue = m.Progress;
                            ImportProgressText = m.Text;
                        });
                        await Task.Delay(150);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Progress handler error: {ex}");
                    }
                });

                iCSVData = new ImportCSVData(_openLibraryService, _transactionService);
                FileToImport = string.Empty;
            }
            catch (Exception ex)
            {
                // popupDetails already non-null
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-000";
                ErrorHandler.AddLog(ex.ToString());
            }
        }

        #region Properties
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private ShowPopUpDetails popupDetails = new();
        [ObservableProperty] private bool isMenuPopupOpen;
        [ObservableProperty] private string importInstructions = Constants.ImportInstructions;
        [ObservableProperty] private string importProgressText = "Importing CSV";
        [ObservableProperty] private double importProgressValue;
        [ObservableProperty] private string? fileToImport;
        [ObservableProperty] private bool showInitial;
        [ObservableProperty] private bool showImporting;
        [ObservableProperty] private string importText = string.Empty;
        [ObservableProperty] private bool showImport;
        [ObservableProperty] private bool showImportProgress;
        [ObservableProperty] private ObservableCollection<ImportSeriesResults> bookProcesingList = new();
        public ImportCSVData? iCSVData { get; set; }
        #endregion

        // Provide Appearing relay command (restored) so the page can call AppearingCommand
        [RelayCommand]
        public Task Appearing() => AppearingInternal();

        private async Task AppearingInternal()
        {
            try
            {
                IsBusy = false;
                var loaded = await LoadSavedStateAsync();
                if (!loaded)
                {
                    ShowInitial = true;
                    ShowImporting = false;
                    ShowImport = false;
                    ImportProgressText = "Importing CSV";
                    ShowImportProgress = false;
                    ImportProgressValue = 0;
                    iCSVData = new ImportCSVData(_openLibraryService, _transactionService);
                    PopupDetails = new ShowPopUpDetails();
                    FileToImport = string.Empty;
                }
                OnPropertyChanged(nameof(ShowInitial));
            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-000";
                ErrorHandler.AddLog(ex.Message);
            }
        }

        // Enhanced diagnostics + safer read (line ~115 area)
        private async Task<bool> LoadSavedStateAsync()
        {
            
            try
            {
                Debug.WriteLine($"[LoadSavedState] Path: {ImportStateFilePath}");
                if (!File.Exists(ImportStateFilePath))
                {
                    Debug.WriteLine("[LoadSavedState] File does not exist");
                    return false;
                }

                string? json = null;
                try
                {
#if DEBUG
                    var fi = new FileInfo(ImportStateFilePath);
                    Debug.WriteLine($"[LoadSavedState] Size={fi.Length} LastWrite={fi.LastWriteTimeUtc:u}");
#endif
                    json = await SafeReadAllTextWithRetriesAsync(ImportStateFilePath, 3, TimeSpan.FromMilliseconds(120))
                               .ConfigureAwait(false);
                    Debug.WriteLine(json is null
                        ? "[LoadSavedState] Read returned null"
                        : $"[LoadSavedState] Read length={json.Length}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[[Error reading state file]] {ex}");
                    return false; // Treat as no saved state
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.WriteLine("[LoadSavedState] Empty JSON content");
                    return false;
                }

                List<Dictionary<string, object?>>? rows = null;
                try
                {
                    rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[[Error deserializing state file]] {ex}");
                    return false;
                }

                if (rows == null || rows.Count == 0)
                {
                    Debug.WriteLine("[LoadSavedState] No rows after deserialize");
                    return false;
                }

                iCSVData ??= new ImportCSVData(_openLibraryService, _transactionService);

                // Rebuild DataTable from deserialized rows
                var table = new DataTable("ImportState");

                // 1. Determine all columns (preserve order: first row's order, then any new ones as encountered)
                var orderedColumns = new List<string>();
                void EnsureColumn(string colName)
                {
                    if (!table.Columns.Contains(colName))
                    {
                        table.Columns.Add(colName, typeof(string)); // Use string for broad compatibility
                        orderedColumns.Add(colName);
                    }
                }

                // From first row
                foreach (var col in rows[0].Keys)
                    EnsureColumn(col);

                // From remaining rows
                foreach (var dict in rows.Skip(1))
                    foreach (var key in dict.Keys)
                        EnsureColumn(key);

                // 2. Populate rows
                foreach (var dict in rows)
                {
                    var dr = table.NewRow();
                    foreach (var kvp in dict)
                    {
                        EnsureColumn(kvp.Key); // In case of late-appearing columns
                        var converted = ConvertDeserializedValue(kvp.Value);
                        dr[kvp.Key] = converted ?? DBNull.Value;
                    }
                    table.Rows.Add(dr);
                }

                // Assign to backing field (ObservableProperty generated: csvData -> CsvData)
                iCSVData.csvData = table;

                // 3. Recompute summary metrics safely
                var dt = iCSVData.csvData;

                iCSVData.BooksFound = dt.Rows.Count;

                bool HasCol(string name) => dt.Columns.Contains(name);

                iCSVData.SeriesFound = HasCol("Series.Name")
                    ? dt.AsEnumerable()
                        .Where(r => r["Series.Name"] != DBNull.Value &&
                                    !string.IsNullOrWhiteSpace(r["Series.Name"]?.ToString()))
                        .Select(r => r["Series.Name"]!.ToString()!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()
                    : 0;

                if (HasCol("Series.Name") && HasCol("OLReady"))
                {
                    iCSVData.SeriesThatAreReadyToImport = dt.AsEnumerable()
                        .Where(r => !string.IsNullOrWhiteSpace(r["Series.Name"]?.ToString()))
                        .GroupBy(r => r["Series.Name"]!.ToString()!.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Count(g => g.All(r =>
                        {
                            var val = r["OLReady"]?.ToString();
                            return bool.TryParse(val, out var ready) && ready;
                        }));
                }
                else
                {
                    iCSVData.SeriesThatAreReadyToImport = 0;
                }

                ShowInitial = false;
                ShowImporting = true;
                ShowImport = false;
                ShowImportProgress = false;
                ImportProgressValue = 0;
                OnPropertyChanged(nameof(iCSVData));

                Debug.WriteLine("[LoadSavedState] Completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSavedState failed (outer): {ex}");
                return false;
            }

            // Local helper to convert deserialized object (JsonElement or primitive) to string / primitive
            static object? ConvertDeserializedValue(object? value)
            {
                if (value is null)
                    return null;

                if (value is JsonElement je)
                {
                    try
                    {
                        return je.ValueKind switch
                        {
                            JsonValueKind.String => je.GetString(),
                            JsonValueKind.Number => je.TryGetInt64(out var l) ? l.ToString() :
                                                    je.TryGetDouble(out var d) ? d.ToString("R") : je.ToString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => null,
                            JsonValueKind.Undefined => null,
                            JsonValueKind.Object => je.ToString(),
                            JsonValueKind.Array => je.ToString(),
                            _ => je.ToString()
                        };
                    }
                    catch
                    {
                        return je.ToString();
                    }
                }

                // Already a primitive (string/int/bool/etc.)
                return value.ToString();
            }
        }

        // Adds retry logic (helps if a write is in progress when we try to read)
        private static async Task<string?> SafeReadAllTextWithRetriesAsync(
            string path,
            int retries,
            TimeSpan delay)
        {
            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    return await sr.ReadToEndAsync().ConfigureAwait(false);
                }
                catch (IOException ioEx) when (attempt < retries)
                {
                    Debug.WriteLine($"[SafeReadAllText] IO retry {attempt}: {ioEx.Message}");
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException uaEx) when (attempt < retries)
                {
                    Debug.WriteLine($"[SafeReadAllText] Unauthorized retry {attempt}: {uaEx.Message}");
                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }
            return null;
        }

        private async Task SaveCurrentStateAsync()
        {
            try
            {
                if (iCSVData?.CsvData == null || iCSVData.CsvData.Rows.Count == 0)
                {
                    Debug.WriteLine("[SaveCurrentState] Nothing to save");
                    return;
                }

                var dt = iCSVData.CsvData;
                var list = new List<Dictionary<string, object?>>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (DataColumn c in dt.Columns)
                    {
                        var val = r[c];
                        dict[c.ColumnName] = val == DBNull.Value ? null : val;
                    }
                    list.Add(dict);
                }
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(ImportStateFilePath, json).ConfigureAwait(false);
                Debug.WriteLine($"[SaveCurrentState] Wrote {list.Count} rows");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveCurrentState failed: {ex}");
            }
        }

        private async Task<bool> PromptSaveIfDataAsync()
        {
            try
            {
                if (iCSVData?.CsvData != null && iCSVData.CsvData.Rows.Count > 0)
                {
#pragma warning disable CS0618
                    var shell = Shell.Current;
                    if (shell?.CurrentPage is Page p)
                    {
                        bool save = await p.DisplayAlert("Save Progress",
                            "Do you want to save your current import progress?",
                            "Yes", "No");
#pragma warning restore CS0618
                        if (save)
                        {
                            await SaveCurrentStateAsync();
                            return true;
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PromptSaveIfDataAsync error: {ex}");
            }
            return false;
        }

        [RelayCommand]
        private async Task PerformImport(object param)
        {
            try
            {
                ShowImporting = false;
                ShowInitial = false;
                var options = new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "com.microsoft.excel.xls", "public.item" } },
                        { DevicePlatform.Android, new[] { "*/*" } },
                    }),
                    PickerTitle = "Please select an Excel file"
                };
                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    IsBusy = true;
                    await Task.Yield();
                    FileToImport = "File: " + result.FileName;
                    try
                    {
                        ShowImportProgress = true;
                        using var stream = await result.OpenReadAsync();
                        if (iCSVData == null)
                            iCSVData = new ImportCSVData(_openLibraryService, _transactionService);

                        await iCSVData.Import(stream);
                        OnPropertyChanged(nameof(iCSVData));
                        await Task.Delay(100);

                        if (param is DataGridView gridView && gridView.ItemsSource != null)
                            gridView.GroupBy("Series.Name");

                        ShowImporting = true;
                        ShowInitial = false;
                        ShowImportProgress = false;
                    }
                    catch (Exception ex)
                    {
                        PopupDetails = new ShowPopUpDetails { IsOpen = true, ErrorMessage = ex.Message, ErrorCode = "ERR-000" };
                        ShowInitial = true;
                        ShowImportProgress = false;
                        ErrorHandler.AddError(ex);
                        OnPropertyChanged(nameof(PopupDetails));
                    }
                    finally
                    {
                        ShowInitial = false;
                        IsBusy = false;
                    }
                }
                else
                {
                    ShowInitial = true;
                }
            }
            catch (Exception ex)
            {
                IsBusy = false;
                ErrorHandler.AddError(ex);
            }
        }

        [RelayCommand]
        private async Task GetDetails(object param)
        {
            try
            {
                ShowImport = false;
                ShowImporting = true;
                IsBusy = true;
                await Task.CompletedTask; // Prevent CS1998 (placeholder)
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
            }
        }

        [RelayCommand] void ShowMenu()
        {
            IsMenuPopupOpen = true;
        }
        [RelayCommand] async Task ImportReadyItems()
        {
            IsMenuPopupOpen = false;
        }
    }
}