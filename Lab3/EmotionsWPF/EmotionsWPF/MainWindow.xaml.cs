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
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EmotionsWPF
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Emotions emoML = new Emotions();
        private CancellationTokenSource cts = new CancellationTokenSource();
        public ICommand CancelCalculations { get; private set; }
        public ICommand ClearImages { get; private set; }
        public ICommand DeleteImage { get; private set; }

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
        public bool[] showData { get; private set; } = new bool[] { true, false };

        public int ShowData
        {
            get { return Array.IndexOf(showData, true); }
        }
        public int SelectedMode
        {
            get { return Array.IndexOf(options_arr, true); }
        }
        private string[] hardcoded_options = new string[] { "happiness", "neutral", "surprise", "sadness",
            "anger", "disgust", "fear", "contempt"};

        private ObservableCollection<photoLine> photo_list = new ObservableCollection<photoLine>();

        public class photoLine //for binding list of myClass in .xaml
        {
            [Key]
            public int photoId { get; set; }
            public string fileName { get; set; }
            public string imagePath { get; set; }
            public int imgHashCode { get; set; }
            public photoDetails Details { get; set; }
            public ICollection<emotion> emotions { get; set; }
            public photoLine()
            {
                emotions = new List<emotion>();
            }
            public string option_emotion { get; set; } = "Calculations in process...";
            public void CreateHashCode (byte[] img)
            {
                int hc = img.Length;
                foreach (int val in img)
                {
                    hc = unchecked(hc * 314159 + val);
                }
                imgHashCode = hc;
            }
        }
        public class photoDetails
        {
            [Key]
            [ForeignKey(nameof(photoLine))]
            public int photoId { get; set; }
            public byte[] imageBLOB { get; set; }
            public photoLine photo { get; set; }
        }
        public class emotion
        {
            [Key]
            public int emotionID { get; set; }
            public double emoOdds { get; set; }
            public string emoName { get; set; }
            public int photoLineId { get; set; }
            public photoLine photo { get; set; }
            public override string ToString()
            {
                return "  " + emoName + ": " + String.Format("{0:0.000}", emoOdds) + "\n";
            }
        }

        public class LibraryContext : DbContext
        {
            public DbSet<photoLine> photos { get; set; }
            public DbSet<emotion> emotions { get; set; }
            public DbSet<photoDetails> details { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder o) => 
                o.UseSqlite("Data Source=library.db");
        }
        private SemaphoreSlim smphore = new SemaphoreSlim(1, 1);
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
            DeleteImage = new RelayCommand(_ =>
                {
                    DoDelete(this);
                }, CanDelete
            );
        }
        private async Task<photoLine?> ProceedImages(string path)
        {
            var tmp = path.Split("\\");
            var relPath = tmp[tmp.Length - 1];
            var photo_obj = new photoLine { fileName = relPath, imagePath = path };

            var img = await File.ReadAllBytesAsync(path, cts.Token);
            photo_obj.CreateHashCode(img); // creating hashcode for our BLOB

            await smphore.WaitAsync();
            using (var db = new LibraryContext()) // check if image exists in DB
            {
                var res = await Task<(photoLine?, bool)>.Run(() => //if bool will be true - it means we must leave
                {
                    photo_obj.Details = new photoDetails { imageBLOB = img, photo = photo_obj };

                    if (db.photos.Any(x => x.imgHashCode == photo_obj.imgHashCode)) // if HashCode is the same
                    {
                        var query = db.photos.Where(x => x.imgHashCode == photo_obj.imgHashCode)
                            .Include(item => item.Details);
                        // if BLOBs are the same - and ShowData is true - returning photoLine from DB and then adding to photo_list
                        if (query.Any(x => Enumerable.SequenceEqual(x.Details.imageBLOB, photo_obj.Details.imageBLOB)))
                        {
                            if (ShowData == 1)
                            {
                                (photoLine?, bool) g = (null, true);
                                return g;
                            }

                            var item = query
                                .Where(x => Enumerable.SequenceEqual(x.Details.imageBLOB, photo_obj.Details.imageBLOB))
                                .Include(x => x.emotions)
                                .FirstOrDefault();
                            return (item, true);
                        }
                    }
                    return (null, false);
                }, cts.Token);
                if (res.Item1 != null)
                {
                    smphore.Release();
                    return res.Item1;
                }
                if (res.Item2 == true)
                {
                    smphore.Release();
                    return null;
                }
            }
            var rt = await Task.Run(async () =>
            {

                var res = await emoML.GetMostLikelyEmotionsAsync(img, cts.Token);

                using (LibraryContext db = new LibraryContext())
                {
                    db.photos.Add(photo_obj);
                    db.SaveChanges();
                    var emoList = new List<emotion>();
                    foreach (var item in res)
                    {
                        emoList.Add(new emotion() { emoOdds = item.Item2, emoName = item.Item1, photo = photo_obj });
                    }
                    db.emotions.AddRange(emoList);
                    db.SaveChanges();
                }
                return res;
            }, cts.Token);
            smphore.Release();
            return photo_obj;
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

                    var Tasks = new Task[ofd.FileNames.Length];

                    for (int i = 0; i < Tasks.Length; i++) {
                        var path = ofd.FileNames[i];

                        Tasks[i] = Task.Run(async () =>
                        {
                            var res = await ProceedImages(path);
                            return res;
                        })
                        .ContinueWith(t => {
                            var res = t.Result;
                            barFill += 1;
                            if (res != null)
                                photo_list.Add(res);
                        }, TaskScheduler.FromCurrentSynchronizationContext()); //from the main thread to be able to add items to list
                        
                    }
                    await Task.WhenAll(Tasks);
                    Calculations_status = 2;
                    PhotoList.Focus();
                    RadioButton_Checked(this, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                if (smphore.CurrentCount == 0)
                    smphore.Release();
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

        private bool CanDelete(object sender)
        {
            if (PhotoList.SelectedItem != null)
                return true;
            return false;
        }
        private async void DoDelete(object sender)
        {
            var item = PhotoList.SelectedItem as photoLine;
            if (item == null)
                return;
            await smphore.WaitAsync();
            using (var db = new LibraryContext())
            {
                var photo = db.photos.Where(x => x.imgHashCode == item.imgHashCode)
                    .Include(x => x.Details)
                    .Where(x => Enumerable.Equals(x.Details.imageBLOB, item.Details.imageBLOB))
                    .Include(x => x.emotions)
                    .FirstOrDefault();
                if (photo == null)
                {
                    smphore.Release();
                    return;
                }

                db.details.Remove(photo.Details);
                foreach (var elem in photo.emotions)
                {
                    db.emotions.Remove(elem);
                }
                db.photos.Remove(photo);
                db.SaveChanges();
                photo_list.Remove(item);
            }
            smphore.Release();
        }
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (Calculations_status == 1)
                return;
            int mode = SelectedMode;
            photo_list = new ObservableCollection<photoLine>
                (photo_list.OrderByDescending(p => p.emotions.Where(p => p.emoName == hardcoded_options[mode]).Max(p => p.emoOdds)));
            foreach (var photo in photo_list)
            {
                var stt =
                    photo.emotions.Where(p => p.emoName == hardcoded_options[mode])
                    .Select(p => "\t" + p.emoName + ": " + String.Format("{0:0.000}", p.emoOdds));
                photo.option_emotion = stt.First();
            }
            PhotoList.ItemsSource = photo_list;
        }

        private async void LoadData_Click(object sender, RoutedEventArgs e)
        {
            await smphore.WaitAsync();
            using (var db = new LibraryContext())
            {

                var res = await Task.Run<List<photoLine>>(() =>
                {
                    var photos = db.photos.Include(item => item.Details).Include(item => item.emotions).ToList();
                    return photos;
                }, cts.Token);
                photo_list = new ObservableCollection<photoLine>
                    (res);
                PhotoList.ItemsSource = photo_list;
                RadioButton_Checked(this, new RoutedEventArgs());
                PhotoList.Focus();
                Calculations_status = 2;

            }
            smphore.Release();
        }
    }
}
