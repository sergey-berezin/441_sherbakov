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
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using AntonContracts;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.Json;
using Polly.Retry;
using Polly;
using System.Net.Http.Json;

namespace EmotionsWPF
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly string url = "http://localhost:5041/photos";
        private CancellationTokenSource cts = new CancellationTokenSource();
        public ICommand CancelCalculations { get; private set; }
        public ICommand ClearImages { get; private set; }
        public ICommand DeleteImage { get; private set; }
        private const int MaxRetries = 3;  // numbers retries to reconnect to the server
        private readonly AsyncRetryPolicy _retryPolicy; // retryPolicy

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

            _retryPolicy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(MaxRetries, times =>
                TimeSpan.FromMilliseconds(Math.Exp(times) * 250));
        }
        private async Task<photoLine?> ProceedImages(string path)
        {
            try
            {
                var tmp = path.Split("\\");
                var relPath = tmp[tmp.Length - 1];
                var img = await File.ReadAllBytesAsync(path, cts.Token);

                var postObj = new postInput { img = img, fname = relPath };
                // no reasons to specify to work Base64 format - I checked, seems like it's already using as default way
                // in Json.Serialize
                var s = JsonConvert.SerializeObject(postObj);
                var buffer = System.Text.Encoding.UTF8.GetBytes(s);
                var byteContent = new ByteArrayContent(buffer);
                byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var client = new HttpClient();
                    var responsePost = await client.PostAsync(url, byteContent, cts.Token);
                    var responsePostResult = JsonConvert.DeserializeObject<postOutput>(responsePost.Content.ReadAsStringAsync().Result);
                    if (responsePostResult.isFound == true && ShowData == 1) // photo is found in DB and in WPF App set the option to not show
                        return null;
                    if (responsePostResult.id == -1) // Most likely it means that Task Cancel Exception occured
                    {
                        return null;
                    }
                    int id = responsePostResult.id;
                    var responseGetPhoto = await client.GetAsync($"{url}/{id}", cts.Token);
                    var photo = JsonConvert.DeserializeObject<photoLine?>(responseGetPhoto.Content.ReadAsStringAsync().Result);
                    return photo;
                });
            }
            catch (Exception ex)
            {
                throw new OperationCanceledException(ex.Message); // to a higher level in catch in FilePicker
            }
        }
        private async void FilePicker_Click(object sender, RoutedEventArgs? e = null)
        {
            filePicker.IsEnabled = false;
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

                    for (int i = 0; i < Tasks.Length; i++)
                    {
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
            catch (OperationCanceledException)
            {
                Calculations_status = 2;
                barFill = 0;
                RadioButton_Checked(this, new RoutedEventArgs());
                cts = new CancellationTokenSource();
                MessageBox.Show("Tasks were cancelled");
            }
            catch (Exception ex)
            {
                Calculations_status = 2;
                barFill = 0;
                RadioButton_Checked(this, new RoutedEventArgs());
                cts = new CancellationTokenSource();
                if (ex.Message == "One or more errors occurred. (A task was canceled.)")
                    MessageBox.Show("All the tasks were canceled");
            }
            finally
            {
                filePicker.IsEnabled = true;
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
            //if (PhotoList.SelectedItem != null) // was correct for deleting 1 image, but I deleting all
            if (PhotoList.Items.Count > 0)
                return true;
            return false;
        }
        private async void DoDelete(object sender)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                HttpClient client = new HttpClient();
                var result = await client.DeleteAsync(url);
                var answer = JsonConvert.DeserializeObject<int>(result.Content.ReadAsStringAsync().Result);
                barFill = 0;
                photo_list.Clear();
            });
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
            try
            {
                Calculations_status = 1;
                barFill = 0;
                ProgressBar.Maximum = 1;
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    HttpClient client = new HttpClient();
                    string result = await client.GetStringAsync(url, cts.Token);
                    var allPhotos = JsonConvert.DeserializeObject<List<photoLine>>(result);
                    photo_list = new ObservableCollection<photoLine>(allPhotos);
                    PhotoList.ItemsSource = photo_list;
                    Calculations_status = 2;
                    barFill = 1;
                    RadioButton_Checked(this, new RoutedEventArgs());
                    PhotoList.Focus();
                });
            }
            catch (OperationCanceledException ex)
            {
                Calculations_status = 2;
                cts = new CancellationTokenSource();
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                Calculations_status = 2;
                cts = new CancellationTokenSource();
                MessageBox.Show(ex.Message);
            }
        }
    }
}
