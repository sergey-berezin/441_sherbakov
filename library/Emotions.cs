// Download ONNX model from https://github.com/onnx/models/blob/main/vision/body_analysis/emotion_ferplus/model/emotion-ferplus-7.onnx
// to project directory before build

using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EmotionFerPlus {
    public interface IErrorReporter
    {   
        void ReportError(string message);
    }
    public class Emotions {
        public List<CancellationTokenSource> cts = new List<CancellationTokenSource>(); 
        public List<string> tasksNames = new List<string>(); //naming tasks (for tests to see what's answer gonna be about)
        //public void setTokens (List<CancellationTokenSource> cts) => this.cts = cts;
        public void setTokens (int num) {
            for (int i = 0; i < num; i++) {
                cts.Add(new CancellationTokenSource());
            }
        }
        public void cancelTask (int i) {
            cts[i].Cancel();
        }
        public void setTaskNames (List<string> tn) => tasksNames = tn;
        public Emotions () {}

        public bool CancelTaskRequested (int i) {
            if (i <= -1)
                return false;
            if (cts[i].IsCancellationRequested)
                return true;
            return false;
        }
        public async Task<IEnumerable <(string, double)>> GetMostLikelyEmotionsAsync (IErrorReporter reporter, byte[] img, int i = -1, bool withTaskNames = false) {

            IErrorReporter myReporter = reporter;
            var L = new List <(string, double)>();
            try {
                

                Func<IEnumerable <(string, double)>> func =  //func for upcoming Task.Run
                    () => {
                    using Image<Rgb24> image = Image.Load<Rgb24>(img);
                    image.Mutate(ctx => {
                        ctx.Resize(new ResizeOptions 
                                    {
                                        Size = new Size(64, 64),
                                        Mode = ResizeMode.Crop
                                    });
                    });

                    if (CancelTaskRequested(i)) //additional check 1 after resizing the image (for leaving cause of CancellationToken)
                        return L;
                    
                    using var modelStream = typeof(Emotions).Assembly.GetManifestResourceStream("emotion.onnx");
                    using var memoryStream = new MemoryStream();
                    modelStream.CopyTo(memoryStream);
                    using var session = new InferenceSession(memoryStream.ToArray()); 

                    if (CancelTaskRequested(i)) //check 2 after creating session 
                        return L;

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("Input3", GrayscaleImageToTensor(image)) };

                    if (CancelTaskRequested(i)) //check 3 after transforming image to tensor 
                        return L;

                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);

                    if (CancelTaskRequested(i)) //check 4 after running ML 
                        return L;

                    var emotions = Softmax(results.First(v => v.Name == "Plus692_Output_0").AsEnumerable<float>().ToArray());

                    if (CancelTaskRequested(i)) //check 5 just almost before end (in case of suddenly came CansellationToken) 
                        return L;

                    string[] keys = { "neutral", "happiness", "surprise", "sadness", "anger", "disgust", "fear", "contempt" };
                    foreach (var item in keys.Zip(emotions)) {
                        L.Add(item);
                    }

                    return L;
                };
                if (i == -1) {
                    return await Task<IEnumerable <(string, double)>>.Run(func);
                }
                else {
                    var ct = cts[i].Token;
                    if (withTaskNames)
                        L.Add(("Gonna be mostly: " + tasksNames[i], 0));
                    return await Task<IEnumerable <(string, double)>>.Run(func, ct);
                }
            }
            catch (Exception ex) {
                myReporter.ReportError(ex.Message);
                return L;
            }
        }
        public IEnumerable <(string, double)> GetMostLikelyEmotions (IErrorReporter reporter, byte[] img, int i = -1, bool withTaskNames = false) {

            IErrorReporter myReporter = reporter;
            var L = new List <(string, double)>();
            try {

                Func<IEnumerable <(string, double)>> func = 
                    () => {
                    using Image<Rgb24> image = Image.Load<Rgb24>(img);
                    image.Mutate(ctx => {
                        ctx.Resize(new ResizeOptions 
                                    {
                                        Size = new Size(64, 64),
                                        Mode = ResizeMode.Crop
                                    });
                    });
                    
                    using var modelStream = typeof(Emotions).Assembly.GetManifestResourceStream("emotion.onnx");
                    using var memoryStream = new MemoryStream();
                    modelStream.CopyTo(memoryStream);
                    using var session = new InferenceSession(memoryStream.ToArray()); 

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("Input3", GrayscaleImageToTensor(image)) };

                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);

                    var emotions = Softmax(results.First(v => v.Name == "Plus692_Output_0").AsEnumerable<float>().ToArray());

                    string[] keys = { "neutral", "happiness", "surprise", "sadness", "anger", "disgust", "fear", "contempt" };
                    foreach (var item in keys.Zip(emotions)) {
                        L.Add(item);
                    }

                    return L;
                };
                if (withTaskNames)
                    L.Add(("Gonna be mostly: " + tasksNames[i], 0));
                return func();
            }
            catch (Exception ex) {
                myReporter.ReportError(ex.Message);
                return L;
            }
        }
        DenseTensor<float> GrayscaleImageToTensor(Image<Rgb24> img)
        {
                var w = img.Width;
                var h = img.Height;
                var t = new DenseTensor<float>(new[] { 1, 1, h, w });

                img.ProcessPixelRows(pa => 
                {
                    for (int y = 0; y < h; y++)
                    {           
                        Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                        for (int x = 0; x < w; x++)
                        {
                            t[0, 0, y, x] = pixelSpan[x].R; // B and G are the same
                        }
                    }
                });
                
                return t;
        }
        float[] Softmax(float[] z)
        {
            var exps = z.Select(x => Math.Exp(x)).ToArray();
            var sum = exps.Sum();
            return exps.Select(x => (float)(x / sum)).ToArray();
        }
    }
}