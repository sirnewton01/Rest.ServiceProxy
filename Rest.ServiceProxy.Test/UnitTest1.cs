using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using Xunit;

namespace Rest.ServiceProxy.Test
{

    public class UnitTest1
    {
        public class Good
        {

        }

        [Route("/api/foo")]
        public interface GoodService
        {
            [HttpGet("{id}")]
            [ProducesResponseType(typeof(Good), (int)HttpStatusCode.OK)]
            void FindFoo(int id);
        }

        [Microsoft.AspNetCore.Mvc.Route("/api/foo")]
        public class GoodController : Controller, GoodService
        {
            [HttpGet("{id}")]
            [ProducesResponseType(typeof(Good), (int)HttpStatusCode.OK)]
            public void FindFoo(int id)
            {
                throw new NotImplementedException();
            }
        }

        public class Bad
        {

        }

        [Route("/api/foo")]
        public interface BadService
        {
            [HttpGet("{id}")]
            [ProducesResponseType(typeof(Bad), (int)HttpStatusCode.OK)]
            void FindBad(int id);
        }

        [Microsoft.AspNetCore.Mvc.Route("/api/foo")]
        public class BadController : Controller, BadService
        {
            [HttpGet("{id}")]
            public void FindBad(int id)
            {
                throw new NotImplementedException();
            }
        }


        [Fact]
        public void TestValidateControllers()
        {
            ServiceProxy<string>.ValidateControllers(new Type[] { typeof(GoodController) });

            Assert.ThrowsAny<Exception>( () => ServiceProxy<string>.ValidateControllers(new Type[] { typeof(BadController) }));
        }
    }
}
