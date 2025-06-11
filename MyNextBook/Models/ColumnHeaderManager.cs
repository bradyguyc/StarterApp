using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using DevExpress.Maui.DataGrid;

namespace MyNextBook.Models
{
    public class ColumnHeaderManager
    {
        public Dictionary<string, List<string>> HeaderOptions { get; set; } = new();

        public Dictionary<string, string> CurrentHeaders { get; set; } = new();

        public ColumnHeaderManager(DataTable table)
        {
            HeaderOptions["All"] = new List<string> { "Series.Name", "Book.Title", "Book.ISBN_10","Book.Note","Book.Tags","Book.ISBN_13","Book.OLID" };
            

            // Default header mapping
            foreach (DataColumn column in table.Columns)
            {
                CurrentHeaders[column.ColumnName] = column.ColumnName;
            }
            
          
        }

        public partial class MainPage : ContentPage
        {
            private ColumnHeaderManager headerManager;

        

           
        }

    }

}
