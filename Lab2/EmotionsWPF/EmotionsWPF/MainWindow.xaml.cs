using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EmotionFerPlus;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EmotionsWPF
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Emotions emoML = new Emotions();
        private CancellationTokenSource cts = new CancellationTokenSource();
        public ICommand CancelCalculations { get; private set; }
        public ICommand ClearImages { get; private set; }

        private int _barFill = 0;
        public int barFill
        {
            get
            {
                return _barFill;
            }
            set
            {
                _barFill = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(barFill)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private int _Calculations_status = 0;
        public int Calculations_status
        {
            get { return _Calculations_status; }
            set
            {
                _Calculations_status = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(Calculations_status)));
                }
            }
        }
        public bool[] options_arr { get; private set; } = new bool[] { true, false, false, false, false, false, false, false };
        private string[] hardcoded_options = new string[] { "happiness", "neutral", "surprise", "sadness",
            "anger", "disgust", "fear", "contempt"};

        public int SelectedMode
        {
            get { return Array.IndexOf(options_arr, true); }
        }

        private ObservableCollection<photoLine> photo_list = new ObservableCollection<photoLine>();

        //private List<photoLine> ph_list;

        class photoLine //for binding list of myClass in .xaml
        {
            public string fileName { get; set; }
            public string imagePath { get; set; }
            public IEnumerable<(string, double)> emoList { get; set; }
            public string option_emotion { get; set; } = "Calculations in process...";
            public void Set_emoList (IEnumerable<(string, double)> l)
            {
                emoList = l;
            }
            public photoLine(string fN, string iP)
            {
                fileName = fN;
                imagePath = iP;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            PhotoList.ItemsSource = photo_list;
            CancelCalculations = new RelayCommand(_ =>
            {
                DoCancel(this);
            }, CanCancel
            );
            ClearImages = new RelayCommand(_ =>
            {
                DoClear(this);
            }, CanClear
            );
        }
        private async Task ProceedImages(string path)
        {
            var tmp = path.Split("\\");
            var relPath = tmp[tmp.Length - 1];
            var photo_obj = new photoLine(relPath, path);
            photo_list.Add(photo_obj);

            var rt = await Task.Run(async ()  =>
            {
                var img = await File.ReadAllBytesAsync(path, cts.Token);
                var res = await emoML.GetMostLikelyEmotionsAsync(img, cts);
                photo_obj.Set_emoList(res);
                return res;
            }, cts.Token);

        }
        private async void FilePicker_Click(object sender, RoutedEventArgs? e = null)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Multiselect = true;
                ofd.Filter = "Images (*.jpg, *.png)|*.jpg;*.png";
                var projectRootFolder = System.IO.Path.GetFullPath("../../../../Images");
                ofd.InitialDirectory = projectRootFolder;
                var response = ofd.ShowDialog();

                if (response == true)
                {
                    Calculations_status = 1;
                    barFill = 0;

                    ProgressBar.Maximum = ofd.FileNames.Length;

                    foreach (var path in ofd.FileNames)
                    {
                        await ProceedImages(path);
                        barFill += 1;
                    }
                    Calculations_status = 2;
                    PhotoList.Focus();
                    RadioButton_Checked(this, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                DoClear(this);
                cts.TryReset();
                cts = new CancellationTokenSource();
                MessageBox.Show(ex.Message);
            }
        }

        private bool CanCancel(object sender)
        {
            if (Calculations_status == 1)
                return true;
            return false;
        }
        private void DoCancel(object sender)
        {
            cts.Cancel();
            
        }

        private bool CanClear(object sender)
        {
            if (Calculations_status == 2)
                return true;
            return false;
        }
        private void DoClear(object sender)
        {
            photo_list = new ObservableCollection<photoLine>();
            PhotoList.ItemsSource = photo_list;
            Calculations_status = 0;
            barFill = 0;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (Calculations_status == 1)
                return;
            int mode = SelectedMode;
            photo_list = new ObservableCollection<photoLine>
                (photo_list.OrderByDescending(p => p.emoList.Where(p => p.Item1 == hardcoded_options[mode]).Max(p => p.Item2)));
            foreach (var photo in photo_list)
            {
                var stt = 
                    photo.emoList.Where(p => p.Item1 == hardcoded_options[mode]).Select
                    (p => "\t" + p.Item1 + ": " + String.Format("{0:0.000}", p.Item2));
                photo.option_emotion = stt.First();
            }
            PhotoList.ItemsSource = photo_list;
        }
    }
}
