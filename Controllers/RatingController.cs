using Microsoft.AspNetCore.Mvc;

namespace DifficultyCalculator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RatingController : Controller
    {
        private readonly DifficultyCache cache;

        public RatingController(DifficultyCache cache)
        {
            this.cache = cache;
        }

        [HttpPost]
        public Task<double> Post([FromBody] DifficultyRequest request) => cache.GetDifficultyRating(request);
    }
}
