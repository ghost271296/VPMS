using System.Windows;
using System.Windows.Controls;
using VPMS.ViewModels;

namespace VPMS.Views;

public partial class UploadView : UserControl
{
    public UploadView() => InitializeComponent();

    private UploadViewModel? Vm => DataContext as UploadViewModel;

    private void DataLogDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && Vm != null)
            {
                Vm.DataLogPath = files[0];
                Vm.HasDataLog = true;
            }
        }
    }

    private void AlarmLogDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && Vm != null)
            {
                Vm.AlarmLogPath = files[0];
                Vm.HasAlarmLog = true;
            }
        }
    }

    private void Drop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
}
