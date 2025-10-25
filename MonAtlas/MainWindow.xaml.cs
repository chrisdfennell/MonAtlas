using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MonAtlas.Models;
using MonAtlas.ViewModels;

namespace MonAtlas
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // If you didn’t wire these in XAML, you can also attach here:
            // ResultsList.SelectionChanged += ResultsList_SelectionChanged;
            // ResultsList.MouseDoubleClick += ResultsList_MouseDoubleClick;
            // ResultsList.KeyDown += ResultsList_KeyDown;

            // Suggest list (autocomplete) pick behavior
            SuggestList.SelectionChanged += SuggestList_SelectionChanged;
        }

        private async void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultsList.SelectedItem is PokemonListItem item && DataContext is MainViewModel vm)
            {
                await vm.SelectByListItemAsync(item);
            }
        }

        private async void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem is PokemonListItem item && DataContext is MainViewModel vm)
            {
                await vm.SelectByListItemAsync(item);
            }
        }

        private async void ResultsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResultsList.SelectedItem is PokemonListItem item && DataContext is MainViewModel vm)
            {
                await vm.SelectByListItemAsync(item);
                e.Handled = true;
            }
        }

        private async void SuggestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestList.SelectedItem is PokemonListItem item && DataContext is MainViewModel vm)
            {
                vm.Query = item.DisplayName;   // show the chosen name in the search box
                vm.IsSuggestOpen = false;      // hide popup
                await vm.SelectByListItemAsync(item);
            }
        }
    }
}
