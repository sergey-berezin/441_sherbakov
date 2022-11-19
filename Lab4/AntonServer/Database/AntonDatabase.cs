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
            await smphore.WaitAsync();
            using (var db = new DatabaseContext())
            {
                var photos = db.photos
                    .Include(x => x.Details)
                    .Include(x => x.emotions);
                if (photos == null)
                {
                    smphore.Release();
                    return 0;
                }
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [details]");
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [emotions]");
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [photos]");
                smphore.Release();
                return 0;
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
                    var res = await Task.Run<List<photoLine>>(() =>
                    {
                        List<photoLine> photos = db.photos.Include(item => item.Details).Include(item => item.emotions).ToList();
                        if (ct.IsCancellationRequested) {
                            return emptyList;
                        }
                        return photos;
                    }, ct);
                    smphore.Release();
                    return res;
                }
            }
            catch (OperationCanceledException ex)
            {
                if (smphore.CurrentCount == 0)
                   smphore.Release();
                return emptyList;
            }
            catch (Exception ex)
            {
                if (smphore.CurrentCount == 0)
                   smphore.Release();
                return emptyList;
            }
        }
        public async Task<photoLine?> TryGetPhotoById(int id)
        {
            try {
                await smphore.WaitAsync();
                using (var db = new DatabaseContext())
                {
                    var res = await Task.Run<photoLine>(() =>
                    {
                        photoLine photo = db.photos.Where(x => x.photoId == id)
                                            .Include(x => x.Details).Include(x => x.emotions).FirstOrDefault();
                        return photo;
                    });
                    smphore.Release();
                    return res;
                }
            }
            catch (OperationCanceledException ex)
            {
                if (smphore.CurrentCount == 0)
                   smphore.Release();
                return null;
            }
            catch (Exception ex)
            {
                if (smphore.CurrentCount == 0)
                   smphore.Release();
                return null;
            }
        }
        public async Task<(int, bool)> PostImage(byte[] img, CancellationToken ct, string name = "default_name")
        {
            var defaultAnswer = (-1, false);
            try 
            {
                var photo_obj = new photoLine { fileName = name };
                photo_obj.CreateHashCode(img); // creating hashcode for our BLOB
                photo_obj.Details = new photoDetails { imageBLOB = img };
                await smphore.WaitAsync();
                using (var db = new DatabaseContext()) // check if image exists in DB
                {
                    var res = await Task<int>.Run(() => //if bool will be true - it means we must leave
                    {
                        if (db.photos.Any(x => x.imgHashCode == photo_obj.imgHashCode)) // if HashCode is the same
                        {
                            var query = db.photos.Where(x => x.imgHashCode == photo_obj.imgHashCode)
                                .Include(item => item.Details);
                            // if BLOBs are the same - and ShowData is true - returning photoLine from DB and then adding to photo_list
                            if (query.Any(x => Enumerable.SequenceEqual(x.Details.imageBLOB, photo_obj.Details.imageBLOB)))
                            {
                                var item = query
                                    .Where(x => Enumerable.SequenceEqual(x.Details.imageBLOB, photo_obj.Details.imageBLOB))
                                    .FirstOrDefault().photoId;
                                return item;
                            }
                        }
                        return -1;
                    }, ct);
                    if (res != -1)
                    {
                        smphore.Release();
                        return (res, true);
                    }
                }
                var id = -1;
                var rt = await Task<int>.Run(async () =>
                {
                    var res = await emoML.GetMostLikelyEmotionsAsync(img, ct);
                    using (DatabaseContext db = new DatabaseContext())
                    {
                        var emoList = new List<emotion>();
                        foreach (var item in res)
                        {
                            emoList.Add(new emotion() { emoOdds = item.Item2, emoName = item.Item1 });
                        }
                        photo_obj.emotions = emoList;
                        db.photos.Add(photo_obj);
                        db.SaveChanges();
                        id = db.photos.Max(x => x.photoId);
                    }
                    return id;
                }, ct);
                smphore.Release();
                return (id, false);
            }
            catch (OperationCanceledException ex)
            {
                if (smphore.CurrentCount == 0)
                   smphore.Release();
                return defaultAnswer;
            }
            catch (Exception ex)
            {
                if (smphore.CurrentCount == 0)
                   smphore.Release();
                return defaultAnswer;
            }
        }
    }
}