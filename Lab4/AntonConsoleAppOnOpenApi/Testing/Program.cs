using System;
using System.Net.Http;
using System.Threading.Tasks;
using MyOpenApi;
namespace SomeNameSpace
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var httpClient = new HttpClient();
                var client = new swaggerClient(httpClient);
                var photos = await client.GetPhotosAsync();
                Console.WriteLine($"Overall photos: {photos.Count.ToString()}");
                var ph = await client.GetPhotoAsync(2);
                Console.WriteLine($"filename of the photo with id = 2: {ph.FileName}");
                var ph2 = await client.GetPhotoAsync(999);
                Console.WriteLine($"filename of the photo with id = 999: {ph2.FileName}");
                //await client.DeletePhotosAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
