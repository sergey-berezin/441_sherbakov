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
          taskArr[i] = getResultsAsync(args[i]);
        }
        await Task.WhenAll(taskArr);
      }
    }
    static void getResults (string? fname = null, int i = -1, bool withTasks = false) {
      try {
        var img = File.ReadAllBytes(fname == null ? "face.png" : fname);
        var L = emoML.GetMostLikelyEmotions(new ConsoleReporter(), img, i, withTasks);

        Console.WriteLine();
        foreach(var item in L) {
            Console.WriteLine($"{item.Item1}: {item.Item2}");
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"Error occured in Console Application: {ex.Message}");
      }
    }
    static async Task getResultsAsync (string? fname = null, int i = -1, bool withTasks = false) {
      try {
        var img = File.ReadAllBytes(fname == null ? "face.png" : fname);
        var L = await emoML.GetMostLikelyEmotionsAsync(new ConsoleReporter(), img, i, withTasks);

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
    static void testSync (string[] args) {
      try {
        Images_Num = 4;
        args = new string[] {"imgs/happy.jpg", "imgs/fear.jpg", "imgs/surprise.jpg", "imgs/neutral.jpg" };
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");
        emoML.setTaskNames(taskNames);
        var watch = new Stopwatch();
        watch.Start();

        for (int i = 0; i < args.Length; i++) {
          getResults(args[i], i, true);
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
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");
        emoML.setTokens(4);
        emoML.setTaskNames(taskNames);

        var taskArr = new Task[args.Length];

        var watch = new Stopwatch();
        watch.Start();

        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], i, true);
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
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");

        emoML.setTokens(Images_Num);
        emoML.setTaskNames(taskNames);

        var taskArr = new Task[args.Length];

        var watch = new Stopwatch();
        watch.Start();

        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], i, false); //i - number of Task (need for upcoming Cancelling with CancellationToken)
        }
        await Task.Delay(delayTime);
        for (int i = 0; i < Images_Num; i++)
          emoML.cancelTask(i);
        await Task.WhenAll(taskArr);
        Console.WriteLine($"OverAll time in testAsyncWithCancellation (All tasks were cancelled after {delayTime}ms from start): " +
        $"{watch.ElapsedMilliseconds}ms\n");
    }
    static async Task testAsyncWithImmediatelyCancellation (string[] args) {
        Images_Num = 4;
        args = new string[] {"imgs/happy.jpg", "imgs/fear.jpg", "imgs/surprise.jpg", "imgs/neutral.jpg" };
        taskNames.Add("happy"); taskNames.Add("in fear"); taskNames.Add("surprised"); taskNames.Add("neutral");

        emoML.setTokens(Images_Num);
        emoML.setTaskNames(taskNames);

        var taskArr = new Task[args.Length];

        for (int i = 0; i < Images_Num; i++)
          emoML.cancelTask(i);

        var watch = new Stopwatch();
        watch.Start();
        
        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResultsAsync(args[i], i, false); //i - number of Task (need for upcoming Cancelling with CancellationToken)
        }
        await Task.WhenAll(taskArr);
        Console.WriteLine($"OverAll time in testAsyncWithImmediatelyCancellation (were Cancel before Tasks started): " +
        $"{watch.ElapsedMilliseconds}ms\n");
    }
    public class ConsoleReporter : IErrorReporter
    {
        public void ReportError(string message) => Console.WriteLine($"Error in internal library!\n{message}");
    }
  }
}


