using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization.Json;

namespace Rest.ServiceProxy
{
    /// <summary>
    /// Service proxy creates proxies that can be used to invoke REST services using
    ///  .Net method invocations. The interfaces must be annotated with the ASP.Net Core
    ///  attributes (Route, HttpGet, etc.) as you would annotate the controller that 
    ///  handles the REST requests.
    ///  
    /// The annotations must be synchronized between the controller and the interface or
    ///  there are risks of failed requests. There is a Validate() method that can be used
    ///  at startup or in unit tests to verify the controllers in your assembly checking for
    ///  discrepancies. A special RouteAttribute is included here to apply to your interfaces
    ///  since the standard one is only applicable to calsses.
    ///  
    /// The ServiceProxy can handle void and complex return types for methods. However, it is
    ///  recommended that for REST methods with a response body for any potential status code
    ///  should instead return IActionResult, which can encompass different types of bodies
    ///  for different potential status codes. For example, there can be a special error response
    ///  type that is different from the OK (200) response types. The ResponseType custom attribute
    ///  is used to associate each status code with the .Net type to be unmarshalled. The IActionResult
    ///  response can be safely case to ObjectResult where the status code can be checked and the
    ///  value casted to the correct type according to the ResponseType attribute for that status code.
    ///  
    ///  Clients must be prepared to catch and handle transport-level exceptions.
    ///  
    ///  Note that applications that use the proxies must depend on the Microsoft.AspNetCore.Mvc.Core
    ///   NuGet assembly in addition to .Net Core. The reason is the ASP.Net Core annotations.
    /// </summary>
    /// <typeparam name="T">The interface type for the proxy</typeparam>
    public class ServiceProxy<T> : DispatchProxy
    {
        private HttpMessageInvoker client;
        private string uri_base;

        /// <summary>
        /// Create a service proxy of the interface T using the provided HTTP
        ///  client to make the connections and the base URI. Each method
        ///  invoked on the proxy will use the same HTTP client instance.
        ///  It is recommended that all services use the same HTTP client
        ///  to improve performance.
        /// </summary>
        public static T Create(HttpMessageInvoker c, string u)
        {
            object proxy = Create<T, ServiceProxy<T>>();
            ((ServiceProxy.ServiceProxy<T>)proxy).client = c;
            ((ServiceProxy.ServiceProxy<T>)proxy).uri_base = u;
            return (T)proxy;
        }

        /// <summary>
        /// Validates all controller classes in the provided enumberble
        ///  (ie. from an assembly.GetTypes() call) for
        ///  their service interfaces checking for any discrepancies.
        /// Discrepancies are reported as exceptions allowing one to hook
        ///  this into the server startup or unit testing to catch problems.
        /// </summary>
        public static void ValidateControllers(IEnumerable<Type> types)
        {
            foreach (Type t in types)
            {
                if (t.IsClass && t.BaseType.Equals(typeof(Controller)))
                {
                    foreach (Type i in t.GetInterfaces())
                    {
                        IEnumerable<Microsoft.AspNetCore.Mvc.RouteAttribute> iRouteAttributes = i.GetCustomAttributes<Microsoft.AspNetCore.Mvc.RouteAttribute>();
                        IEnumerable<Microsoft.AspNetCore.Mvc.RouteAttribute> tRouteAttributes = t.GetCustomAttributes<Microsoft.AspNetCore.Mvc.RouteAttribute>();

                        if (iRouteAttributes.Count() > tRouteAttributes.Count())
                        {
                            throw new Exception("In controller " + t.FullName + " the number of [Route] custom attributes doesn't match the interface " + i.FullName);
                        }

                        foreach (Microsoft.AspNetCore.Mvc.RouteAttribute route1 in iRouteAttributes)
                        {
                            bool matched = false;
                            foreach (Microsoft.AspNetCore.Mvc.RouteAttribute route2 in tRouteAttributes)
                            {
                                if (Object.Equals(route1.Template, route2.Template)
                                    && route1.Order == route2.Order
                                    && Object.Equals(route1.Name, route2.Name))
                                {
                                    matched = true;
                                    break;
                                }
                            }

                            if (!matched)
                            {
                                throw new Exception("In controller " + t.FullName + " the annotation [Route("+route1.Template+", Name=\""+route1.Name+"\", Order="+route1.Order+")] on interface " + i.FullName + " is not found");
                            }
                        }

                        InterfaceMapping mapping = t.GetInterfaceMap(i);

                        for (int idx = 0; idx < mapping.InterfaceMethods.Length; idx++)
                        {
                            MethodInfo m1 = mapping.InterfaceMethods[idx];
                            MethodInfo m2 = mapping.TargetMethods[idx];

                            var m1RouteAttributes = m1.GetCustomAttributes<Microsoft.AspNetCore.Mvc.RouteAttribute>();
                            var m2RouteAttributes = m2.GetCustomAttributes<Microsoft.AspNetCore.Mvc.RouteAttribute>();

                            if (m1RouteAttributes.Count() > m2RouteAttributes.Count())
                            {
                                throw new Exception("In controller " + t.FullName + " on method " + m2 + " there is a mismatch on the number of [Route] attributes on interface " + i.FullName);
                            }

                            foreach(Microsoft.AspNetCore.Mvc.RouteAttribute route1 in m1RouteAttributes)
                            {
                                bool matched = false;
                                foreach(Microsoft.AspNetCore.Mvc.RouteAttribute route2 in m2RouteAttributes)
                                {
                                    if (Object.Equals(route1.Template, route2.Template)
                                        && route1.Order == route2.Order
                                        && Object.Equals(route1.Name, route2.Name))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }

                                if (!matched)
                                {
                                    throw new Exception("In controller " + t.FullName + " the annotation [Route(\"" + route1.Template + "\", Name=\""+route1.Name+"\", Order="+route1.Order+")] on interface " + i.FullName + " was not applied to method " + m1);
                                }
                            }

                            var m1MethodAttr = m1.GetCustomAttributes<HttpMethodAttribute>();
                            var m2MethodAttr = m2.GetCustomAttributes<HttpMethodAttribute>();

                            if (m1MethodAttr.Count() > m2MethodAttr.Count())
                            {
                                throw new Exception("In controller " + t.FullName + " on method " + m2 + " there is a mismatch on the number of [Http*] attributes on interface " + i.FullName);
                            }

                            foreach (HttpMethodAttribute methodAttr1 in m1MethodAttr)
                            {
                                bool matched = false;
                                foreach (HttpMethodAttribute methodAttr2 in m2MethodAttr)
                                {
                                    if (Object.Equals(methodAttr1.Template, methodAttr2.Template)
                                        && methodAttr1.Order == methodAttr2.Order
                                        && Object.Equals(methodAttr1.Name, methodAttr2.Name)
                                        && Object.Equals(methodAttr1.GetType(), methodAttr2.GetType()))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }

                                if (!matched)
                                {
                                    throw new Exception("In controller " + t.FullName + " the annotation ["+methodAttr1.GetType().Name+"(" + methodAttr1.Template + ", Name=\""+methodAttr1.Name+"\", Order="+methodAttr1.Order+")] on interface " + i.FullName + " was not applied to method " + m1);
                                }
                            }

                            var m1RespTypeAttributes = m1.GetCustomAttributes<ProducesResponseTypeAttribute>();
                            var m2RespTypeAttributes = m2.GetCustomAttributes<ProducesResponseTypeAttribute>();

                            if (m1RespTypeAttributes.Count() > m2RespTypeAttributes.Count())
                            {
                                throw new Exception("In controller " + t.FullName + " on method " + m2 + " there is a mismatch on the number of [ProducesResponseType] attributes on interface " + i.FullName);
                            }

                            foreach (ProducesResponseTypeAttribute respTypeAttr1 in m1RespTypeAttributes)
                            {
                                bool matched = false;
                                foreach (ProducesResponseTypeAttribute respTypeAttr2 in m2RespTypeAttributes)
                                {
                                    if (Object.Equals(respTypeAttr1.Type, respTypeAttr2.Type)
                                        && Object.Equals(respTypeAttr1.StatusCode, respTypeAttr2.StatusCode))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }

                                if (!matched)
                                {
                                    throw new Exception("In controller " + t.FullName + " the annotation [ProducesResponseType(" + respTypeAttr1.Type.Name + ", "+respTypeAttr1.StatusCode+")] on interface " + i.FullName + " was not applied to method " + m1);
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            string uri = uri_base;

            foreach (object a in targetMethod.DeclaringType.GetCustomAttributes())
            {
                if (a is Microsoft.AspNetCore.Mvc.RouteAttribute)
                {
                    uri = uri + "/" + ((Microsoft.AspNetCore.Mvc.RouteAttribute)a).Template;
                    break;
                }
            }

            HttpMethod method = null;

            foreach (object a in targetMethod.GetCustomAttributes())
            {
                if (targetMethod.Name.StartsWith("Get") || a is HttpGetAttribute)
                {
                    method = HttpMethod.Get;
                }
                else if (targetMethod.Name.StartsWith("Put") || a is HttpPutAttribute)
                {
                    method = HttpMethod.Put;
                }
                else if (targetMethod.Name.StartsWith("Post") || a is HttpPostAttribute)
                {
                    method = HttpMethod.Post;
                }
                else if (targetMethod.Name.StartsWith("Delete") || a is HttpDeleteAttribute)
                {
                    method = HttpMethod.Delete;
                }

                if (a is HttpMethodAttribute)
                {
                    if (((HttpMethodAttribute)a).Template != null)
                    {
                        uri = uri + "/" + ((HttpMethodAttribute)a).Template;
                    }
                }
            }

            object fromBody = null;
            Type fromBodyType = null;

            int idx = 0;
            foreach (ParameterInfo p in targetMethod.GetParameters())
            {
                // Identify the from body, if any
                foreach (object a in p.GetCustomAttributes())
                {
                    if (a is FromBodyAttribute)
                    {
                        fromBody = args[idx-1];
                        fromBodyType = p.GetType();
                        break;
                    }
                }

                // Find query parameters, if any
                // TODO it is not sufficient in all cases just to call ToString() on the argument
                // TODO handle constraints on the parameters
                if (args[idx] != null && uri.IndexOf("{" + p.Name + "}") != -1)
                {
                    uri = uri.Replace("{" + p.Name + "}", args[idx].ToString());
                }
                else if (args[idx] != null)
                {
                    uri += (uri.IndexOf("?") == -1 ? "?" : "&") + p.Name + "=" + args[idx].ToString();
                }
            }

            HttpResponseMessage response = null;
            HttpContent requestContent = null;

            if (fromBody != null && fromBodyType != null)
            {
                var serializer = new DataContractJsonSerializer(fromBodyType);
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                serializer.WriteObject(stream, fromBody);
                stream.Position = 0;

                requestContent = new StringContent(new StreamReader(stream).ReadToEnd(), System.Text.Encoding.UTF8, "application/json");
            }

            if (method == null)
            {
                throw new Exception("The method is not a valid MVC method. The HTTP method could not be discovered. Did you put the right [HttpGet/Put/Post/Delete] attribute on it?");
            }

            if (uri.Equals(uri_base))
            {
                throw new Exception("The method is not a valid MVC method. Did you put put a [Route] attribute on the interface and did you put an [HttpGet/Put/Post/Delete] attribute on the method?");
            }

            response = client.SendAsync(new HttpRequestMessage(method, uri), new System.Threading.CancellationToken(false)).Result;

            foreach (object a in targetMethod.GetCustomAttributes())
            {
                if (a is ProducesResponseTypeAttribute)
                {
                    var prta = (ProducesResponseTypeAttribute)a;

                    if (prta.StatusCode == (int)response.StatusCode)
                    {
                        if (targetMethod.ReturnType.Equals(prta.Type))
                        {
                            if (response.IsSuccessStatusCode) {
                                var serializer = new DataContractJsonSerializer(prta.Type);
                                var responseEntity = serializer.ReadObject(response.Content.ReadAsStreamAsync().Result);
                                return responseEntity;
                            }
                            else
                            {
                                throw new Exception("Bad response: " + response.StatusCode +" from REST call.");
                            }
                        }
                        else if (targetMethod.ReturnType.Equals(typeof(IActionResult)))
                        {
                            if (prta.Type != null)
                            {
                                var serializer = new DataContractJsonSerializer(prta.Type);
                                var responseEntity = serializer.ReadObject(response.Content.ReadAsStreamAsync().Result);
                                var result = new ObjectResult(responseEntity);
                                result.StatusCode = (int)response.StatusCode;
                                return result;
                            }
                            else
                            {
                                var result = new StatusCodeResult((int)response.StatusCode);
                                return result;
                            }
                        }
                    }
                }
            }

            // This status code was not declared as a ResponseType, we will do our best to produce a response
            // TODO log this as some kind of 
            if (targetMethod.ReturnType.Equals(typeof(IActionResult)))
            {
                var stream = response.Content.ReadAsStreamAsync().Result;

                if (stream.Length == 0) {
                    return new StatusCodeResult((int)response.StatusCode);
                } else {
                    var result = new ContentResult();
                    // TODO figure out how to read the stream into a string for the content
                    //result.Content = stream.ReadAsync().Result;
                    result.StatusCode = (int)response.StatusCode;
                    return result;
                }
            }
            else if (targetMethod.ReturnType != null && response.IsSuccessStatusCode)
            {
                var serializer = new DataContractJsonSerializer(targetMethod.ReturnType);
                var responseEntity = serializer.ReadObject(response.Content.ReadAsStreamAsync().Result);
                return responseEntity;
            }
            else if (targetMethod.ReturnType == null && response.IsSuccessStatusCode)
            {
                return null;
            }
            else
            {
                throw new Exception("Response from the server is: " + response.StatusCode);
            }
        }
    }
}