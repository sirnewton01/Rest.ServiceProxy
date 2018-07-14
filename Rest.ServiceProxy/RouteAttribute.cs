using System;

namespace Rest.ServiceProxy
{
    /// <summary>
    /// This RouteAttribute extends the ASP.Net Core RouteAttribute to allow it to be
    ///  applied to interfaces. Otherwise, if applying a RouteAttribute to a class then
    ///  use the core version.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class RouteAttribute: Microsoft.AspNetCore.Mvc.RouteAttribute
    {
        public RouteAttribute(string template) : base(template)
        {
        }
    }
}
