using System;
using EmotionFerPlus;

namespace app
{
  class Program
  {
    static void Main(string[] args)
    {
      var L = Emotions.GetMostLikelyEmotions("face.png");

      Console.WriteLine($"Hello, World!");


      foreach(var item in L) {
        Console.WriteLine($"{item.Item1}: {item.Item2}");
      }

    }
  }
}
