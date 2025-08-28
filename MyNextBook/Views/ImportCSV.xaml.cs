using System.Collections;
using System.Data;
using System.Globalization;
using System.Linq; // added for LINQ queries
using Microsoft.Maui.Controls;
using DevExpress.Maui.Core;
using DevExpress.Maui.DataGrid;
using MyNextBook.Models;
using MyNextBook.ViewModels;
using MyNextBook.Converters; // added for InvertedBoolConverter

namespace MyNextBook.Views;

public partial class ImportCSV : ContentPage
{
    List<string> validHeaderNames = new List<string>
    {
        "Series.Name", "Book.Title","Book.Title", "Book.ISBN_10", "Book.Notes", "Book.Tags", "Book.ISBN_13", "Book.OLID","None"
    };

    ImportCSVViewModel _vm;
    private bool _olReadyColumnAdded = false;
    private bool _seriesGrouped = false; // track grouping

    public ImportCSV(ImportCSVViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset grid so grouping can be reapplied after data load
        gridControl.Columns.Clear();
        _olReadyColumnAdded = false;
        _seriesGrouped = false; // reset grouping state
        if (BindingContext is ImportCSVViewModel vm && vm.AppearingCommand.CanExecute(null))
        {
            vm.AppearingCommand.Execute(null);
        }
        // Attempt deferred grouping after data loads (if LoadSavedState populated already)
        Device.StartTimer(TimeSpan.FromMilliseconds(150), () =>
        {
            TryGroupSeries();
            return false; // run once
        });
    }

    private void TryGroupSeries()
    {
        if (_seriesGrouped) return; // already grouped
        if (gridControl.ItemsSource == null) return; // nothing to group yet
        if (!gridControl.Columns.Any()) return; // columns not generated
        if (!gridControl.Columns.Any(c => c.FieldName == "Series.Name")) return; // no series column yet
        try
        {
            gridControl.GroupBy("Series.Name");
            gridControl.RefreshData();
            _seriesGrouped = true;
        }
        catch { /* swallow – grouping not critical */ }
    }

    private void DataGridView_OnAutoGeneratingColumn(object? sender, AutoGeneratingColumnEventArgs e)
    {
        DataGridView dgv = sender as DataGridView;
    
        if (e.Column.FieldName == "OLReady")
        {
            // Cancel auto-generation for OLReady column since we'll add a custom TemplateColumn
            e.Cancel = true;
            
            // Add custom TemplateColumn for OLReady if not already added
            if (!_olReadyColumnAdded && dgv != null)
            {
                var page = this;
                var templateColumn = new DevExpress.Maui.DataGrid.TemplateColumn
                {
                    FieldName = "OLReady",
                    Caption = "Ready/Search",
                    Width = 140,
                    DisplayTemplate = new DataTemplate(() =>
                    {
                        var root = new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = GridLength.Auto }
                            },
                            Padding = new Thickness(0),
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };

                        var checkBox = new CheckBox
                        {
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalOptions = LayoutOptions.Start,
                            Color = ThemeManager.Theme.Scheme.Primary,
                            BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer
                        };
                        checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding("Value", BindingMode.TwoWay));

                        // ImageButton using a FontImageSource for the Material search glyph
                        var searchButton = new ImageButton
                        {
                            BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer,
                            Padding = new Thickness(4),
                            WidthRequest = 32,
                            HeightRequest = 32,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Center,
                            CornerRadius = 4
                        };

                        searchButton.Source = new FontImageSource
                        {
                            FontFamily = "MD",
                            Glyph = "\ue8b6", // search icon
                            Size = 20,
                            Color = ThemeManager.Theme.Scheme.Primary
                        };

                        // Hide button when OLReady == true (same logic as old label visibility)
                        searchButton.SetBinding(IsVisibleProperty, new Binding("Value", converter: new InvertedBoolConverter()));

                        // Bind Command to the VM's SearchForBookCommand on the page's BindingContext
                        searchButton.SetBinding(ImageButton.CommandProperty,
                            new Binding("BindingContext.SearchForBookCommand", source: page));

                        // Pass the CellData itself as CommandParameter; VM will extract DataRowView via reflection
                        searchButton.SetBinding(ImageButton.CommandParameterProperty, new Binding("."));

                        Grid.SetColumn(checkBox, 0);
                        Grid.SetColumn(searchButton, 1);

                        root.Children.Add(checkBox);
                        root.Children.Add(searchButton);

                        return root;
                    })
                };

                dgv.Columns.Insert(0, templateColumn);
                _olReadyColumnAdded = true;
            }
            return;
        }
        else
        {
            e.Column.IsReadOnly = true;
            e.Column.Width = 200;
            string fieldName = e.Column.FieldName;

            if (e.Column.HeaderContentTemplate == null)
            {
                DataTemplate dt = new DataTemplate(() =>
                {
                    var label = new Label
                    {
                        Text = fieldName,
                        VerticalTextAlignment = TextAlignment.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        WidthRequest = 200,
                        HeightRequest = 30,
                        FontAttributes = FontAttributes.Bold
                    };

                    var layout = new VerticalStackLayout
                    {
                        BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer,
                        Margin = new Thickness(0)
                    };
                    layout.Children.Add(label);
                    return layout;
                });
                _vm.iCSVData.columnHeaderMap[fieldName] = fieldName;
                e.Column.HeaderContentTemplate = dt;
            }
            if (e.Column.FieldName == "Series.Name")
            {
                MainThread.BeginInvokeOnMainThread(TryGroupSeries);
            }
        }
    }

    private void gridControl_CustomGroupDisplayText(object sender, CustomGroupDisplayTextEventArgs e)
    {
        DataGridView gridControl = sender as DataGridView;
        int totalRows = 0;
        if (e.Column.FieldName == "Series.Name")
        {
            totalRows = gridControl.GetChildRowCount(e.RowHandle);
            int readyCount = 0;
            ArrayList groupsRows = new ArrayList();
            GetChildRows(gridControl, e.RowHandle, groupsRows);

            foreach (DataRowView row in groupsRows)
            {
                if (row["OLReady"] != DBNull.Value && (bool)row["OLReady"])
                {
                    readyCount++;
                }
            }

            e.DisplayText = $"{e.Value}\n{totalRows} Books, {readyCount} ready to import";
        }
    }

    public void GetChildRows(DataGridView view, int groupRowHandle, ArrayList childRows)
    {
        if (!view.IsGroupRow(groupRowHandle)) return;
        int childCount = view.GetChildRowCount(groupRowHandle);
        for (int i = 0; i < childCount; i++)
        {
            int childHandle = view.GetChildRowHandle(groupRowHandle, i);
            if (view.IsGroupRow(childHandle))
                GetChildRows(view, childHandle, childRows);
            else
            {
                object row = view.GetRowItem(childHandle);
                if (!childRows.Contains(row))
                    childRows.Add(row);
            }
        }
    }

    private void gridControl_ValidateAndSave(object sender, ValidateItemEventArgs e)
    {
        e.ForceUpdateItemsSource();
        gridControl.RefreshData();
    }
}


















































