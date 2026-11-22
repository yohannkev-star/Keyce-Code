using System.Windows.Controls;
using System.Windows.Input;
using Keyce.ViewModels;

namespace Keyce.Controls
{
    public partial class ExplorerPanel : UserControl
    {
        public ExplorerPanel()
        {
            InitializeComponent();
        }

        private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ExplorerViewModel vm && Tree.SelectedItem is ExplorerNode node)
                vm.ActivateNode(node);
        }
    }
}