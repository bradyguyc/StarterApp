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

        if (e.Column.FieldName == "Series.Name")
        {
            dgv.GroupBy(e.Column);
        }
        if (e.Column.FieldName == "Ready")
        {
            e.Column.Width = 80;
            e.Column.IsReadOnly = false;
        }
        else
        {
            e.Column.IsReadOnly = true;
            e.Column.Width = 200;
        }


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





