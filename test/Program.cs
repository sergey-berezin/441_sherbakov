using System;
using EmotionFerPlus;

namespace app
{
  class Program
  {
    static object locker = new object();
    static async Task Main(string[] args)
    {
      if (args.Length == 0) {
        await getResults();
      }
      else {
        var taskArr = new Task[args.Length];
        for (int i = 0; i < args.Length; i++) {
          taskArr[i] = getResults(args[i], i);
        }
        await Task.WhenAll(taskArr);
      }
      Console.WriteLine("The End");
    }
    static async Task getResults (string? fname = null, int num = 0, int c = 40) {
      try {
        var img = File.ReadAllBytes(fname == null ? "face.png" : fname);

            
        var L = await Emotions.GetMostLikelyEmotions(new ConsoleReporter(), img);
        // for (int i = 0; i < c; i++) 
        //   L = await Emotions.GetMostLikelyEmotions(new ConsoleReporter(), img);

        // for (int i = 0; i < 1000; i++)
        //   Console.Write(num);

        lock(locker) {
          Console.WriteLine($"\n");
          foreach(var item in L) {
            Console.WriteLine($"{item.Item1}: {item.Item2}");
          }
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"Error occured {ex.Message}");
      }
    }

    // static async Task tasks1 (string args, int num) {
    //   await getResults(args, num);
    //   await getResults(args, num+1);
    //   await getResults(args, num+2);
    //   await getResults(args, num+3);
    // }

    public class ConsoleReporter : IErrorReporter
    {
        public void ReportError(string message) => Console.WriteLine($"Error in internal library!\n{message}");
    }
  }
}


