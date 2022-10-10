using System;
using System.Diagnostics;
using EmotionFerPlus;

namespace application
{
  class Program
  {
    static object locker = new object(); // for sync for Console.WriteLine (in case of several images in input)
    static Emotions emoML = new Emotions(); // To init ML object
    static int Images_Num = 1;
    static List<string> taskNames = new List<string>(); // for test if answer is right
    static List<CancellationTokenSource> cts_array = new List<CancellationTokenSource>(); // token array if we want to use token in Task
    static async Task Main(string[] args)
    {
      if (args.Length == 0) { //if no parametres in command line - start my "standart" test with 4 images
        testSync(args);
        await testAsync(args);
        await testAsyncWithCancellation(args, 60);//second parameter points out time(in ms) from starting after which all the tasks would be cancelled
        await testAsyncWithImmediatelyCancellation(args);
      }
      else {
        var taskArr = new Task[args.Length];
        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], CancellationToken.None);
        }
        await Task.WhenAll(taskArr);
      }
    }
    static async Task getResultsAsync (string fname, CancellationToken ct, string? taskName = null) {
      try {
        var img = await File.ReadAllBytesAsync(fname);
        var L = await emoML.GetMostLikelyEmotionsAsync(img, ct, taskName);

        lock(locker) { //giving access to Console.Write only for 1 Thread
          Console.WriteLine();
          foreach(var item in L) {
            Console.WriteLine($"{item.Item1}: {item.Item2}");
          }
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"Error occured in Console Application: {ex.Message}");
      }
    }
    static void getResults (string fname, CancellationToken ct, string? taskName = null) {
      try {
        var img = File.ReadAllBytes(fname);
        var L = emoML.GetMostLikelyEmotions(img, taskName);

        Console.WriteLine();
        foreach(var item in L) {
            Console.WriteLine($"{item.Item1}: {item.Item2}");
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"Error occured in Console Application: {ex.Message}");
      }
    }
    static void testSync (string[] args) {
      try {
        Images_Num = 4;
        args = new string[] {"imgs/happy.jpg", "imgs/fear.jpg", "imgs/surprise.jpg", "imgs/neutral.jpg" };

        cts_array.Clear();
        taskNames.Clear();

        for (int i = 0; i < Images_Num; i++)
          cts_array.Add(new CancellationTokenSource());
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");
        var watch = new Stopwatch();
        watch.Start();

        for (int i = 0; i < args.Length; i++) {
          getResults(args[i], cts_array[i].Token, taskNames[i]);
        }

        Console.WriteLine($"OverAll time in testSync: {watch.ElapsedMilliseconds}ms\n");

      }
      catch (Exception ex) {
        Console.WriteLine($"Error occured in Console Application: {ex.Message}");
      }
    }
    static async Task testAsync (string[] args) {
      try {
        Images_Num = 4;
        args = new string[] {"imgs/happy.jpg", "imgs/fear.jpg", "imgs/surprise.jpg", "imgs/neutral.jpg" };

        cts_array.Clear();
        taskNames.Clear();

        for (int i = 0; i < Images_Num; i++)
          cts_array.Add(new CancellationTokenSource());
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");


        var taskArr = new Task[args.Length];

        var watch = new Stopwatch();
        watch.Start();

        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], cts_array[i].Token, taskNames[i]);
        }

        await Task.WhenAll(taskArr);
        Console.WriteLine($"OverAll time in testAsync: {watch.ElapsedMilliseconds}ms\n");

      }
      catch (Exception ex) {
        Console.WriteLine($"Error occured in Console Application: {ex.Message}");
      }
    }
    static async Task testAsyncWithCancellation (string[] args, int delayTime = 50) {
        Images_Num = 4;
        args = new string[] {"imgs/happy.jpg", "imgs/fear.jpg", "imgs/surprise.jpg", "imgs/neutral.jpg" };

        cts_array.Clear();
        taskNames.Clear();

        for (int i = 0; i < Images_Num; i++)
          cts_array.Add(new CancellationTokenSource());
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");

        var taskArr = new Task[args.Length];

        var watch = new Stopwatch();
        watch.Start();

        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], cts_array[i].Token, taskNames[i]); //i - number of Task (need for upcoming Cancelling with CancellationToken)
        }
        await Task.Delay(delayTime);
        for (int i = 0; i < Images_Num; i++)
          cts_array[i].Cancel();
        await Task.WhenAll(taskArr);
        Console.WriteLine($"OverAll time in testAsyncWithCancellation (All tasks were cancelled after {delayTime}ms from start): " +
        $"{watch.ElapsedMilliseconds}ms\n");
    }
    static async Task testAsyncWithImmediatelyCancellation (string[] args) {
        Images_Num = 4;
        args = new string[] {"imgs/happy.jpg", "imgs/fear.jpg", "imgs/surprise.jpg", "imgs/neutral.jpg" };

        cts_array.Clear();
        taskNames.Clear();

        for (int i = 0; i < Images_Num; i++)
          cts_array.Add(new CancellationTokenSource());
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");

        var taskArr = new Task[args.Length];

        for (int i = 0; i < Images_Num; i++)
          cts_array[i].Cancel();

        var watch = new Stopwatch();
        watch.Start();
        
        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], cts_array[i].Token, taskNames[i]); //i - number of Task (need for upcoming Cancelling with CancellationToken)
        }
        await Task.WhenAll(taskArr);
        Console.WriteLine($"OverAll time in testAsyncWithImmediatelyCancellation (were Cancel before Tasks started): " +
        $"{watch.ElapsedMilliseconds}ms\n");
    }
  }
}


