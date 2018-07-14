using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using Xunit;
using Moq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.IO;

namespace Rest.ServiceProxy.Test
{

    public class UnitTest1
    {
        private class HttpMessageHandlerMock<T> : HttpMessageHandler
        {
            HttpResponseMessage resp;
            HttpRequestMessage req;

            public HttpMessageHandlerMock(HttpResponseMessage m, T responseObject) : base()
            {
                resp = m;
                var serializer = new DataContractJsonSerializer(typeof(T));
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                serializer.WriteObject(stream, responseObject);
                stream.Position = 0;
                m.Content = new StringContent(new StreamReader(stream).ReadToEnd(), System.Text.Encoding.UTF8, "application/json");
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                req = request;
                return Task.FromResult<HttpResponseMessage>(resp);
            }

            public HttpRequestMessage GetRequest()
            {
                return req;
            }
        }
        public class Good
        {

        }

        [Route("api/good")]
        public interface GoodService
        {
            [HttpGet("{id}")]
            [ProducesResponseType(typeof(Good), (int)HttpStatusCode.OK)]
            Good Find(int id);
        }

        [Microsoft.AspNetCore.Mvc.Route("api/good")]
        public class GoodController : Controller, GoodService
        {
            [HttpGet("{id}")]
            [ProducesResponseType(typeof(Good), (int)HttpStatusCode.OK)]
            public Good Find(int id)
            {
                throw new NotImplementedException();
            }
        }

        public class Bad
        {

        }

        [Route("api/bad")]
        public interface BadService
        {
            [HttpGet("{id}")]
            [ProducesResponseType(typeof(Bad), (int)HttpStatusCode.OK)]
            Bad Find(int id);
        }

        [Microsoft.AspNetCore.Mvc.Route("api/bad")]
        public class BadController : Controller, BadService
        {
            [HttpGet("{id}")]
            public Bad Find(int id)
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

        [Fact]
        public void TestSimpleInvocation()
        {
            HttpMessageHandlerMock<Good> messageHandler = new HttpMessageHandlerMock<Good>(new HttpResponseMessage(HttpStatusCode.OK), new Good());

            var httpClientMock = new HttpMessageInvoker(messageHandler);
            var goodService = ServiceProxy<GoodService>.Create(httpClientMock, "http://localhost");
            goodService.Find(1);

            Assert.Equal("http://localhost/api/good/1", messageHandler.GetRequest().RequestUri.ToString());
        }
    }
}
