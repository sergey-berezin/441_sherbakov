using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntonContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AntonServer.Database;
using Newtonsoft.Json;

namespace AntonServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PhotosController : ControllerBase
    {
        private IPhotosDb dB;

        public PhotosController(IPhotosDb db)
        {
            this.dB = db;
        }

        public async Task<IEnumerable<photoLine>> GetPhotos(CancellationToken ct) 
        {
            var phs = await dB.GetAllPhotos(ct);
            return phs;
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<photoLine>> GetPhoto(int id) 
        {
            var ph = await dB.TryGetPhotoById(id);
            if (ph != null)
                return ph;
            return StatusCode(404, "Photo with given id is not found"); 
        }

        [HttpDelete]
        public async Task<int> DeletePhotos() 
        {
            return await dB.DeleteAllImages();
        }

        [HttpPost]
        public async Task<postOutput> PostImage(postInput obj, CancellationToken ct) 
        {
            var img = obj.img;
            var name = obj.fname;
            return await dB.PostImage(img, ct, name);
        }
    }
}
