using System.Collections.Generic;
using AntonContracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using EmotionFerPlus;

namespace AntonServer.Database 
{
    public class DatabaseContext : DbContext
    {
        public DbSet<photoLine> photos { get; set; }
        public DbSet<emotion> emotions { get; set; }
        public DbSet<photoDetails> details { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder o) => 
            o.UseSqlite("Data Source=emotions.db");
    }
    public interface IPhotosDb 
    {
        Task<IEnumerable<photoLine>> GetAllPhotos(CancellationToken ct);
        Task<(int, bool)> PostImage(byte[] img, CancellationToken ct, string name = "default_name");
        Task<photoLine?> TryGetPhotoById(int id);
        Task<int> DeleteAllImages();
    }
    public class FunctionsWithDB : IPhotosDb
    {
        private SemaphoreSlim smphore = new SemaphoreSlim(1, 1);
        private Emotions emoML = new Emotions();
        public async Task<int> DeleteAllImages()
        {
            try {
                await smphore.WaitAsync();
                using (var db = new DatabaseContext())
                {
                    var photos = db.photos
                        .Include(x => x.Details)
                        .Include(x => x.emotions);
                    if (photos == null)
                    {
                        return 0;
                    }
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM [details]");
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM [emotions]");
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM [photos]");
                    return 0;
                }
            }
            catch (Exception ex) {
                return -1;
            }
            finally {
                smphore.Release();
            }
        }
        public async Task<IEnumerable<photoLine>> GetAllPhotos(CancellationToken ct)
        {
            var emptyList = new List<photoLine>();
            try 
            {
                await smphore.WaitAsync();
                using (var db = new DatabaseContext())
                {
                    List<photoLine> photos = db.photos.Include(item => item.Details).Include(item => item.emotions).ToList();
                    if (ct.IsCancellationRequested) {
                        return emptyList;
                    }
                    return photos;
                }
            }
            catch (Exception ex)
            {
                return emptyList;
            }
            finally {
                smphore.Release();
            }
        }
        public async Task<photoLine?> TryGetPhotoById(int id)
        {
            try {
                await smphore.WaitAsync();
                using (var db = new DatabaseContext())
                {
                    photoLine photo = db.photos.Where(x => x.photoId == id)
                                        .Include(x => x.Details).Include(x => x.emotions).FirstOrDefault();
                    return photo; // may be null, if null it means that photo with given id not found
                }
            }
            catch (Exception ex)
            {
                return null;
            }
            finally {
                smphore.Release();
            }
        }
        public async Task<(int, bool)> PostImage(byte[] img, CancellationToken ct, string name = "default_name")
        {
            var defaultAnswer = (-1, false);
            try 
            {
                await smphore.WaitAsync();
                var photo_obj = new photoLine { fileName = name };
                photo_obj.CreateHashCode(img); // creating hashcode for our BLOB
                photo_obj.Details = new photoDetails { imageBLOB = img };
                if (ct.IsCancellationRequested)
                    return defaultAnswer;
                using (var db = new DatabaseContext()) // check if image exists in DB
                {
                    //if bool will be true - it means we must leave
                    var res = -1;
                    if (db.photos.Any(x => x.imgHashCode == photo_obj.imgHashCode)) // if HashCode is the same
                    {
                        var query = db.photos.Where(x => x.imgHashCode == photo_obj.imgHashCode)
                            .Include(item => item.Details);
                        // if BLOBs are the same - and ShowData is true - returning photoLine from DB and then adding to photo_list
                        if (ct.IsCancellationRequested)
                            return defaultAnswer;
                        if (query.Any(x => Enumerable.SequenceEqual(x.Details.imageBLOB, photo_obj.Details.imageBLOB)))
                        {
                            var item = query
                                .Where(x => Enumerable.SequenceEqual(x.Details.imageBLOB, photo_obj.Details.imageBLOB))
                                .FirstOrDefault().photoId;
                            if (item != null)
                                res = item;
                        }
                    }
                    if (ct.IsCancellationRequested)
                        return defaultAnswer;
                    if (res != -1)
                    {
                        return (res, true);
                    }
                    var nuget_res = await emoML.GetMostLikelyEmotionsAsync(img, ct);
                    if (ct.IsCancellationRequested)
                        return defaultAnswer;
                    var emoList = new List<emotion>();
                    foreach (var item in nuget_res)
                    {
                        emoList.Add(new emotion() { emoOdds = item.Item2, emoName = item.Item1 });
                    }
                    if (ct.IsCancellationRequested)
                        return defaultAnswer;
                    photo_obj.emotions = emoList;
                    db.photos.Add(photo_obj);
                    db.SaveChanges();
                    res = db.photos.Max(x => x.photoId);
                    return (res, false);
                }
            }
            catch (Exception ex)
            {
                return defaultAnswer;
            }
            finally {
                smphore.Release();
            }
        }
    }
}