using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using SvgConverter;
using SvgToXaml.Command;
using SvgToXaml.Infrastructure;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SvgToXaml.ViewModels
{
    public class SvgImagesViewModel : ViewModelBase
    {
        private string _currentDir;
        private ObservableCollectionSafe<ImageBaseViewModel> _images;
        private ImageBaseViewModel _selectedItem;
        private ICommand
            openFileCommand, openFolderCommand,
            exportDirCommand, infoCommand;

        public SvgImagesViewModel()
        {
            _images = new ObservableCollectionSafe<ImageBaseViewModel>();

            ContextMenuCommands = new ObservableCollection<Tuple<object, ICommand>>();
            ContextMenuCommands.Add(new Tuple<object, ICommand>("Open Explorer", new DelegateCommand<string>(OpenExplorerExecute))); 
        }

        private void OpenFolderExecute()
        {//TODO ERROR FolderBrowserDialog was not disposed
            using (var folderDialog = new FolderBrowserDialog { Description = "Open Folder", SelectedPath = CurrentDir, ShowNewFolderButton = false })
                if (folderDialog.ShowDialog() == DialogResult.OK)
                    CurrentDir = folderDialog.SelectedPath;
        }

        private void OpenFileExecute()
        {
            var openDlg = new OpenFileDialog { CheckFileExists = true, Filter = "Svg-Files|*.svg*", Multiselect = false };
            if (openDlg.ShowDialog() == true)
            {
                ImageBaseViewModel.OpenDetailWindow(new SvgImageViewModel(openDlg.FileName));
            }
        }

        private void ExportDirExecute()
        {//TODO ERROR SaveFileDialog was not disposed
            string outFileName = Path.GetFileNameWithoutExtension(CurrentDir) + ".xaml";
            using (var saveDlg = new SaveFileDialog { AddExtension = true, DefaultExt = ".xaml", Filter = "Xaml-File|*.xaml", InitialDirectory = CurrentDir, FileName = outFileName })
                if (saveDlg.ShowDialog() == DialogResult.OK)
                {
                    string namePrefix = null;

                    bool useComponentResKeys = false;
                    string nameSpaceName = null;
                    var nameSpace = Microsoft.VisualBasic.Interaction.InputBox("Enter a NameSpace for using static ComponentResKeys (or leave empty to not use it)", "NameSpace");
                    if (!string.IsNullOrWhiteSpace(nameSpace))
                    {
                        useComponentResKeys = true;
                        nameSpaceName =
                            Microsoft.VisualBasic.Interaction.InputBox(
                                "Enter a Name of NameSpace for using static ComponentResKeys", "NamespaceName");
                    }
                    else
                    {
                        namePrefix = Microsoft.VisualBasic.Interaction.InputBox("Enter a namePrefix (or leave empty to not use it)", "Name Prefix");
                        if (string.IsNullOrWhiteSpace(namePrefix))
                            namePrefix = null;
                    }

                    outFileName = Path.GetFullPath(saveDlg.FileName);
                    var resKeyInfo = new ResKeyInfo
                    {
                        XamlName = Path.GetFileNameWithoutExtension(outFileName),
                        Prefix = namePrefix,
                        UseComponentResKeys = useComponentResKeys,
                        NameSpace = nameSpace,
                        NameSpaceName = nameSpaceName,

                    };
                    File.WriteAllText(outFileName, ConverterLogic.SvgDirToXaml(CurrentDir, resKeyInfo, false));

                    BuildBatchFile(outFileName, resKeyInfo);
                }
        }

        private void BuildBatchFile(string outFileName, ResKeyInfo compResKeyInfo)
        {
            if (MessageBox.Show(outFileName + "\nhas been written\nCreate a BatchFile to automate next time?",
                null, MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
            {
                var outputname = Path.GetFileNameWithoutExtension(outFileName);
                var outputdir = Path.GetDirectoryName(outFileName);
                var relOutputDir = FileUtils.MakeRelativePath(CurrentDir, true, outputdir, true);
                var svgToXamlPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                var relSvgToXamlPath = FileUtils.MakeRelativePath(CurrentDir, true, svgToXamlPath, false);
                var batchText = $"{relSvgToXamlPath} BuildDict /inputdir \".\" /outputdir \"{relOutputDir}\" /outputname {outputname}";

                if (compResKeyInfo.UseComponentResKeys)
                {
                    batchText += $" /useComponentResKeys=true /compResKeyNSName={compResKeyInfo.NameSpaceName} /compResKeyNS={compResKeyInfo.NameSpace}";
                    WriteT4Template(outFileName);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(compResKeyInfo.Prefix))
                    {
                        batchText += " /nameprefix \"" + compResKeyInfo.Prefix + "\"";
                    }
                }

                batchText += "\r\npause";

                File.WriteAllText(Path.Combine(CurrentDir, "Update.cmd"), batchText);

                ////Copy ExeFile
                //var srcFile = Environment.GetCommandLineArgs().First();
                //var destFile = Path.Combine(CurrentDir, Path.GetFileName(srcFile));
                ////Console.WriteLine("srcFile:", srcFile);
                ////Console.WriteLine("destFile:", destFile);
                //if (!string.Equals(srcFile, destFile, StringComparison.OrdinalIgnoreCase))
                //{
                //    Console.WriteLine("Copying file...");
                //    File.Copy(srcFile, destFile, true);
                //}
            }
        }

        private void WriteT4Template(string outFileName)
        {
            //BuildAction: "Embedded Resource"
            var appType = typeof(App);
            var assembly = appType.Assembly;
            //assembly.GetName().Name
            var resourceName = appType.Namespace + ".Payload.T4Template.tt"; //Achtung: hier Punkt statt Slash
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidDataException($"Error: {resourceName} not found in payload file");
            string text;
            //tODO ERROR StreamReader was not disposed
            using (var str = new StreamReader(stream, Encoding.UTF8))
                text = str.ReadToEnd();

            var t4FileName = Path.ChangeExtension(outFileName, ".tt");
            File.WriteAllText(t4FileName, text, Encoding.UTF8);
        }

        private void InfoExecute()
        {
            MessageBox.Show(
                string.Format("SvgToXaml © {0} Bernd Klaiber\n\nPowered by\nsharpvectors.codeplex.com (Svg-Support),\nicsharpcode (AvalonEdit)", DateTime.Today.Year),
                "About");
        }
        private void OpenExplorerExecute(string path)
        {
            Process.Start(path);
        }

        public static SvgImagesViewModel DesignInstance
        {
            get
            {
                var result = new SvgImagesViewModel();
                result.Images.Add(SvgImageViewModel.DesignInstance);//TODO why twicely?
               // result.Images.Add(SvgImageViewModel.DesignInstance);
                return result;
            }
        }

        public string CurrentDir
        {
            get { return _currentDir; }
            set
            {
                if (SetProperty(ref _currentDir, value))
                    ReadImagesFromDir(_currentDir);
            }
        }

        public ImageBaseViewModel SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

        public ObservableCollectionSafe<ImageBaseViewModel> Images
        {
            get { return _images; }
            set { SetProperty(ref _images, value); }
        }
        
        public ICommand OpenFolderCommand
        {
            get
            {
                if (openFileCommand == null)
                    openFileCommand = new DelegateCommand(OpenFileExecute);

                return openFileCommand;
            }
        }

        public ICommand OpenFileCommand
        {
            get
            {
                if (openFolderCommand == null)
                    openFolderCommand = new DelegateCommand(OpenFolderExecute);

                return openFolderCommand;
            }
        }

        public ICommand ExportDirCommand
        {
            get
            {
                if (exportDirCommand == null)
                    exportDirCommand = new DelegateCommand(ExportDirExecute);

                return exportDirCommand;
            }
        }

        public ICommand InfoCommand
        {
            get
            {
                if (infoCommand == null)
                    infoCommand = new DelegateCommand(InfoExecute);

                return infoCommand;
            }
        }

        public ObservableCollection<Tuple<object, ICommand>> ContextMenuCommands { get; }

        private void ReadImagesFromDir(string folder)
        {
            Images.Clear();
            var svgFiles = ConverterLogic.SvgFilesFromFolder(folder);
            var svgImages = svgFiles.Select(f => new SvgImageViewModel(f));

            var graphicFiles = GetFilesMulti(folder, GraphicImageViewModel.SupportedFormats);
            var graphicImages = graphicFiles.Select(f => new GraphicImageViewModel(f));
            
            var allImages = svgImages.Concat<ImageBaseViewModel>(graphicImages).OrderBy(e=>e.Filepath);
            
            Images.AddRange(allImages);
        }

        private static IEnumerable<string> GetFilesMulti(
            string sourceFolder, string filters, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            try
            {
                if (Directory.Exists(sourceFolder))
                    return filters.Split('|').SelectMany(filter => Directory.GetFiles(sourceFolder, filter, searchOption));
                else
                    return new string[0];
            }
            catch// (Exception)
            {
                return new string[0];
            }
        }
    }
}
