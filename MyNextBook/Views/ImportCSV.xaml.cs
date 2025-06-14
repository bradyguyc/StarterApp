using System.Collections;
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
}





