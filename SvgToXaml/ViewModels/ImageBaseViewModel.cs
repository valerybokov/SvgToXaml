using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using SvgToXaml.Command;

namespace SvgToXaml.ViewModels
{
    public abstract class ImageBaseViewModel : ViewModelBase
    {
        ICommand fOpenDetailCommand, fOpenFileCommand;

        protected ImageBaseViewModel(string filepath)
        {
            Filepath = filepath;
        }

        public string Filepath { get; }
        public string Filename => Path.GetFileName(Filepath);
        public ImageSource PreviewSource => GetImageSource();
        public ICommand OpenDetailCommand
        {
            get {
                if (fOpenDetailCommand == null)
                    fOpenDetailCommand = new DelegateCommand(OpenDetailExecute);

                return fOpenDetailCommand;
            }
        }
        public ICommand OpenFileCommand
        {
            get {
                if (fOpenFileCommand == null)
                    fOpenFileCommand = new DelegateCommand(OpenFileExecute);

                return fOpenFileCommand;
            } 
        }
        protected abstract ImageSource GetImageSource();
        public abstract bool HasXaml { get; }
        public abstract bool HasSvg { get; }
        public string SvgDesignInfo => GetSvgDesignInfo();

        private void OpenDetailExecute()
        {
            OpenDetailWindow(this);
        }

        public static void OpenDetailWindow(ImageBaseViewModel imageBaseViewModel)
        {
            new DetailWindow { DataContext = imageBaseViewModel }.Show();
        }

        private void OpenFileExecute()
        {
            Process.Start(Filepath);
        }

        protected abstract string GetSvgDesignInfo();
    }
}