using System.Data;

using DevExpress.Maui.Core;
using DevExpress.Maui.DataGrid;

using MyNextBook.Models;

namespace MyNextBook.Views;

public partial class ImportCSV : ContentPage
{
    List<string> validHeaderNames = new List<string>
    {
        "Series.Name", "Book.Title", "Book.ISBN_10", "Book.Notes", "Book.Tags", "Book.ISBN_13", "Book.OLID,None"
    };

    private Dictionary<int, string> columnHeaderMap = new Dictionary<int, string>();

    public ImportCSV()
    {
        InitializeComponent();

    }

    private void DataGridView_OnAutoGeneratingColumn(object? sender, AutoGeneratingColumnEventArgs e)
    {
        DataGridView dgv = sender as DataGridView;

        if (e.Column.FieldName == "OLReady")
        {
            e.Column.Width = 80;
            e.Column.IsReadOnly = false;
            if (e.Column.HeaderContentTemplate == null)
            {
                DataTemplate dt = new DataTemplate(() =>
                {
                    var label = new Label
                    {
                        Text = e.Column.FieldName,

                        HorizontalTextAlignment = TextAlignment.Center,
                        WidthRequest = 200,
                        FontAttributes = FontAttributes.Bold
                    };


                    var label1 = new Label
                    {
                        Text = "Mapped to:",

                        HorizontalTextAlignment = TextAlignment.Center,
                        WidthRequest = 200,
                        FontAttributes = FontAttributes.Bold
                    };

                    var layout = new VerticalStackLayout();

                    layout.BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer;
                    layout.Margin = new Thickness(1, 1, 1, 1);
                    layout.Children.Add(label);
                    layout.Children.Add(label1);

                    return layout;
                });
                e.Column.HeaderContentTemplate = dt;
            }
        }
        else
        {
            e.Column.IsReadOnly = true;
            e.Column.Width = 200;



            if (!columnHeaderMap.ContainsKey(e.Column.Column))
            {
                columnHeaderMap.Add(e.Column.Column, e.Column.FieldName);
            }
            else
            {
                columnHeaderMap[e.Column.Column] = e.Column.FieldName;
            }

            if (e.Column.HeaderContentTemplate == null)
            {
                DataTemplate dt = new DataTemplate(() =>
                {
                    var label = new Label
                    {
                        Text = e.Column.FieldName,

                        HorizontalTextAlignment = TextAlignment.Center,
                        WidthRequest = 200,
                        FontAttributes = FontAttributes.Bold
                    };



                    var picker = new Picker
                    {
                        ItemsSource = validHeaderNames,
                        SelectedIndex = validHeaderNames.IndexOf(e.Column.FieldName),
                        WidthRequest = 200,
                        BackgroundColor = ThemeManager.Theme.Scheme.TertiaryContainer


                    };

                    var layout = new VerticalStackLayout();

                    layout.BackgroundColor = ThemeManager.Theme.Scheme.PrimaryContainer;
                    layout.Margin = new Thickness(0, 0, 0, 0);
                    layout.Children.Add(label);
                    layout.Children.Add(picker);
                    return layout;
                });
                e.Column.HeaderContentTemplate = dt;
            }
        }
    }

    private void gridControl_CustomGroupDisplayText(object sender, CustomGroupDisplayTextEventArgs e)
    {
        DataGridView gridControl = sender as DataGridView;
        if (e.Column.FieldName == "Series.Name") // Replace with your grouped column
        {
            int totalRows = gridControl.GetChildRowCount(e.RowHandle);
            int readyCount = 0;

            for (int i = 0; i < totalRows; i++)
            {
                int childRowHandle = gridControl.GetChildRowHandle(e.RowHandle, i); // Updated to use gridControl
                var rowData = gridControl.GetRowItem(childRowHandle) as DataRow; // Updated to use gridControl

                if (rowData != null && rowData["OLReady"] != DBNull.Value && (bool)rowData["OLReady"])
                {
                    readyCount++;
                }


            }

            e.DisplayText = $"{e.Value}\n{totalRows} Books, {readyCount} ready to import";
        }

    }

    private void gridControl_ValidateCell(object sender, ValidateCellEventArgs e)
    {
        if (e.FieldName == "OLReady")
        {
            int totalRows = gridControl.GetChildRowCount(e.RowHandle);
            int readyCount = 0;

            for (int i = 0; i < totalRows; i++)
            {
                int childRowHandle = gridControl.GetChildRowHandle(e.RowHandle, i); // Updated to use gridControl
                var rowData = gridControl.GetRowItem(childRowHandle) as DataRow; // Updated to use gridControl

                if (rowData != null && rowData["OLReady"] != DBNull.Value && (bool)rowData["OLReady"])
                {
                    readyCount++;
                }


            }
            DataGridView gridInstance = sender as DataGridView;
            gridInstance.RefreshData();
    
        }
    }
}





