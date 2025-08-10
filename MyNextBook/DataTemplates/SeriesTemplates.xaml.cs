using DevExpress.Maui.CollectionView;

namespace MyNextBook.DataTemplates;

public partial class SeriesTemplates : ResourceDictionary
{
	public SeriesTemplates()
	{
		InitializeComponent();
	}

    private void ImageButton_Clicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button)
        {
            // Find the parent CollectionView named "WorksCollectionView"
            var parent = button.Parent;
            while (parent != null)
            {
                if (parent is DXCollectionView collectionView )
                {
                    collectionView.SelectedItem = null;
                    break;
                }
                parent = parent.Parent;
            }
        }
    }
}