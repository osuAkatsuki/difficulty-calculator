using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using osu.Game.Rulesets.Difficulty;

namespace DifficultyCalculator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AttributesController : Controller
    {
        private readonly DifficultyCache cache;

        public AttributesController(DifficultyCache cache)
        {
            this.cache = cache;
        }

        [HttpPost]
        public Task<DifficultyAttributes> Post([FromBody] DifficultyRequest request) => cache.GetAttributes(request);
    }
}