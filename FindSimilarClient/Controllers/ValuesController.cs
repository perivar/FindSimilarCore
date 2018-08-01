using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace FindSimilarClient.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public string Post([FromBody] JObject data)
        {
            var name = (string)data["name"];
            var time = (string)data["time"];
            var key = (string)data["key"];

            return $"name: {name}, time: {time}, key: {key}";
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public string Put(int id, [FromBody] JObject data)
        {
            var name = (string)data["name"];
            var time = (string)data["time"];
            var key = (string)data["key"];

            return $"name: {name}, time: {time}, key: {key}";
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public string Delete(int id)
        {
            return $"deleted {id}";
        }
    }
}
