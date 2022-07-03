using Microsoft.AspNetCore.Mvc;

namespace DifficultyCalculator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CacheController : Controller
    {
        private readonly DifficultyCache cache;

        public CacheController(DifficultyCache cache)
        {
            this.cache = cache;
        }

        [HttpDelete]
        public void Delete([FromQuery(Name = "beatmap_md5")] string beatmapMd5)
            => cache.Purge(beatmapMd5);
    }
}
