using System.Collections;
using System.Data;
using System.Globalization;
using Microsoft.Maui.Controls;

using DevExpress.Maui.Core;
using DevExpress.Maui.DataGrid;

using MyNextBook.Models;
using MyNextBook.ViewModels;

namespace MyNextBook.Views;

public partial class ImportCSV : ContentPage
{
    List<string> validHeaderNames = new List<string>
    {
        "Series.Name", "Book.Title","Book.Title", "Book.ISBN_10", "Book.Notes", "Book.Tags", "Book.ISBN_13", "Book.OLID","None"
    };

    ImportCSVViewModel _vm;
    private bool _olReadyColumnAdded = false;

    public ImportCSV(ImportCSVViewModel vm)
    {

        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Remove all columns from the data grid
        gridControl.Columns.Clear();
        _olReadyColumnAdded = false;
        if (BindingContext is ImportCSVViewModel vm && vm.AppearingCommand.CanExecute(null))
        {

            vm.AppearingCommand.Execute(null);
        }
        
        
    }
    
    // Event handler for search icon tap
    private async void OnSearchIconTapped(object sender, EventArgs e)
    {
        try
        {
            if (sender is Label label && label.BindingContext != null)
            {
                await SearchForBookData(label.BindingContext);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Search failed: {ex.Message}", "OK");
        }
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
                var templateColumn = new DevExpress.Maui.DataGrid.TemplateColumn
                {
                    FieldName = "OLReady",
                    Caption = "Ready/Search",
                    Width = 120,
                    DisplayTemplate = new DataTemplate(() =>
                    {
                        var grid = new Grid
                        {
                            ColumnDefinitions = 
                            {
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = GridLength.Auto }
                            },
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };

                        var checkBox = new CheckBox
                        {
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalOptions = LayoutOptions.Start,
                            Color = ThemeManager.Theme.Scheme.Primary,
                            BackgroundColor  = ThemeManager.Theme.Scheme.PrimaryContainer

                        };
                        // Fix: Use the correct binding path for DevExpress DataGrid
                        checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding("Value", BindingMode.TwoWay));

                        // Create search icon using Label with Material Design icon
                        var searchLabel = new Label
                        {
                            FontFamily = "MD",
                            Text = "\ue8b6", // Material Design search icon
                            FontSize = 16,
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalOptions = LayoutOptions.Start,
                            Margin = new Thickness(5, 0, 0, 0),
                            TextColor =  ThemeManager.Theme.Scheme.Primary,
                            BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer
                        };

                        // Create converter to invert boolean values for visibility
                        var converter = new InvertedBoolConverter();
                        searchLabel.SetBinding(Label.IsVisibleProperty, new Binding("Value", converter: converter));

                        // Make the search icon clickable using TapGestureRecognizer
                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += OnSearchIconTapped;
                        searchLabel.GestureRecognizers.Add(tapGesture);

                        Grid.SetColumn(checkBox, 0);
                        Grid.SetColumn(searchLabel, 1);
                        
                        grid.Children.Add(checkBox);
                        grid.Children.Add(searchLabel);

                        return grid;
                    })
                };
                
                dgv.Columns.Insert(0, templateColumn); // Insert at the beginning
                _olReadyColumnAdded = true;
            }
            return;
        }
        else
        {
            e.Column.IsReadOnly = true;
            e.Column.Width = 200;

            int columnIndex = e.Column.Column;
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

                    var picker = new Picker
                    {
                        ItemsSource = validHeaderNames,
                        VerticalTextAlignment = TextAlignment.Center,
                        HorizontalTextAlignment = TextAlignment.Center,

                        SelectedIndex = validHeaderNames.IndexOf(fieldName),
                        WidthRequest = 200,
                        HeightRequest = 30,
                        BackgroundColor = ThemeManager.Theme.Scheme.TertiaryContainer
                    };

                    /*
                    // Event handler for Picker selection change
                    picker.SelectedIndexChanged += (object s, EventArgs args) =>
                    {

                        var p = (Picker)s;
                        _vm.iCSVData.columnHeaderMap[p.SelectedItem.ToString()] = label.Text;


                    };
                    */
                    var layout = new VerticalStackLayout();
                    layout.BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer;
                    layout.Margin = new Thickness(0, 0, 0, 0);
                    layout.Children.Add(label);
                    //layout.Children.Add(picker);

                    return layout;
                });
                _vm.iCSVData.columnHeaderMap[fieldName] = fieldName;
                e.Column.HeaderContentTemplate = dt;
            }
        }
    }

    // Simple converter to invert boolean values
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }

    private void gridControl_CustomGroupDisplayText(object sender, CustomGroupDisplayTextEventArgs e)
    {
        DataGridView gridControl = sender as DataGridView;
        if (e.Column.FieldName == "Series.Name") // Replace with your grouped column
        {
            int totalRows = gridControl.GetChildRowCount(e.RowHandle);
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
        // Get the number of immediate children 
        int childCount = view.GetChildRowCount(groupRowHandle);
        for (int i = 0; i < childCount; i++)
        {
            // Get the handle of a child row 
            int childHandle = view.GetChildRowHandle(groupRowHandle, i);
            // If the child is a group row, add its children to the list 
            if (view.IsGroupRow(childHandle))
                GetChildRows(view, childHandle, childRows);
            else
            {
                // The child is a data row
                // Add the row to childRows if it wasn't added before 
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

    // Add this method to handle search functionality
    private async Task SearchForBookData(object bookRowData)
    {
        try
        {
            // Cast to DataRowView to access the underlying DataRow
            if (bookRowData is DataRowView rowView)
            {
                var row = rowView.Row;
                
                // Extract book information
                string bookTitle = row["Book.Title"]?.ToString() ?? "";
                string author = row["Book.Author"]?.ToString() ?? "";
                
                // Show some feedback to the user
                await DisplayAlertAsync("Searching", $"Searching for: {bookTitle} by {author}", "OK");
                
                // You can add your actual search logic here
                // For example, call your OpenLibrary search service
                // await _vm.SearchForSpecificBook(row);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Search failed: {ex.Message}", "OK");
        }
    }

 
}













