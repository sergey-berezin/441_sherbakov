// Download ONNX model from https://github.com/onnx/models/blob/main/vision/body_analysis/emotion_ferplus/model/emotion-ferplus-7.onnx
// to project directory before build

using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Threading.Tasks.Dataflow;

namespace EmotionFerPlus {
    public class Emotions {
        public SemaphoreSlim captureSession = new SemaphoreSlim(1, 1);
        public InferenceSession session;
        bool CancelTaskRequested(CancellationTokenSource? cts) {
            if (cts == null)
                return false;
            if (cts.IsCancellationRequested)
                return true;
            return false;
        }
        public Emotions () {
            using var modelStream = typeof(Emotions).Assembly.GetManifestResourceStream("emotion.onnx");
            using var memoryStream = new MemoryStream();
            if (modelStream != null)
                modelStream.CopyTo(memoryStream);
            this.session = new InferenceSession(memoryStream.ToArray()); 
        }
        public async Task<IEnumerable <(string, double)>> GetMostLikelyEmotionsAsync (byte[] img, CancellationTokenSource? cts = null, string? taskName = null) {

            var L = new List <(string, double)>();
            try {
                var myStream = new MemoryStream(img);
                Image<Rgb24> image;
                if (cts == null) {
                    image = await Image.LoadAsync<Rgb24>(myStream);
                }
                else
                    image = await Image.LoadAsync<Rgb24>(myStream, cts.Token);

                Func<IEnumerable <(string, double)>> func = //func for upcoming Task.Run
                    () => {
                    image.Mutate(ctx => {
                        ctx.Resize(new ResizeOptions 
                                    {
                                        Size = new Size(64, 64),
                                        Mode = ResizeMode.Crop
                                    });
                    });

                    if (CancelTaskRequested(cts)) //additional check 1 after resizing the image (for leaving cause of CancellationToken)
                        return L;

                    if (CancelTaskRequested(cts)) //check 2 after creating session 
                        return L;

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("Input3", GrayscaleImageToTensor(image)) };
                    image.Dispose();

                    if (CancelTaskRequested(cts)) //check 3 after transforming image to tensor 
                        return L;


                    await captureSession.Wait();
                    var results = this.session.Run(inputs);
                    captureSession.Release();

                    var emotions = (Softmax(results.First(v => v.Name == "Plus692_Output_0").AsEnumerable<float>().ToArray()));

                    if (CancelTaskRequested(cts)) //check 4 after running ML 
                        return L;

                    if (CancelTaskRequested(cts)) //check 5 just almost before end (in case of suddenly came CansellationToken) 
                        return L;

                    string[] keys = { "neutral", "happiness", "surprise", "sadness", "anger", "disgust", "fear", "contempt" };
                    foreach (var item in keys.Zip(emotions)) {
                        L.Add(item);
                    }

                    return L;
                };

                if (taskName != null)
                    L.Add(("Gonna be mostly: " + taskName, 0));

                if (cts == null) {
                        return await Task<IEnumerable <(string, double)>>.Run(func);
                }

                else {
                    return await Task<IEnumerable <(string, double)>>.Run(func, cts.Token);
                }
            }
            catch (Exception ex) {
                throw new Exception(ex.Message, ex);
                // return L;
            }
        }
        public IEnumerable <(string, double)> GetMostLikelyEmotions (byte[] img, CancellationTokenSource? cts = null, string? taskName = null) {

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

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("Input3", GrayscaleImageToTensor(image)) };


                    captureSession.Wait();
                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = this.session.Run(inputs);
                    captureSession.Release();

                    var emotions = (Softmax(results.First(v => v.Name == "Plus692_Output_0").AsEnumerable<float>().ToArray()));

                    string[] keys = { "neutral", "happiness", "surprise", "sadness", "anger", "disgust", "fear", "contempt" };
                    foreach (var item in keys.Zip(emotions)) {
                        L.Add(item);
                    }

                    return L;
                };
                if (taskName != null)
                    L.Add(("Gonna be mostly: " + taskName, 0));
                return func();
            }
            catch (Exception ex) {
                throw new Exception(ex.Message, ex);
                // return L;
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