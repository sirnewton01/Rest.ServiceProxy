# REST Service Proxy

When you build a ASP.Net Core REST project you can define controllers, routes, response types
that define the REST/HTTP interactions with your REST API. This metadata helps ASP.Net Core
to route HTTP requests to correct controller and method to handle the request. You can define
model DTO classes for your complex JSON entities.

If you have clients that are written in C#, such as integration tests, you must write your own
service to work with the REST API using an HTTP client. There is duplication of logic that is
already defined in the metadata on the controller! There is also the possibility that the two
can get out of sync.

It would be useful to harvest the metadata already defined and automatically create services
that a C# client can use to interact with the REST API automatically. The client uses regular 
C# method invocation and exception handling instead of using some form of REST/HTTP client.

An additional benefit of this approach is that if you ever decide to re-use client code in your
server then it can use the controller directly. There are reasons why you might want to
do this at some pointin your project. It avoids the network overhead of HTTP request handling
and marshalling.

## Project Structure

When defining your REST API there are some straight forward changes you can make to enable the
service proxy workflow. The first step is to create a separate project to keep your model
classes and service interfaces. This project will be used by your clients to interact with
your REST API without giving away your controllers and forcing your clients to take
your dependencies.

TBD screenshot here (show new Client project)

Once you have created your client project you can move your model classes over there. For each
of your controllers you create an interface, copy your method declarations and make your
controller implement that interface.

TBD screenshot here (show controller and interface that it inherits)

The interface doesn't have all of that good metadata that is defined on your controller. You
can copy the custom attributes such as [Route], [HttpGet], [HttpPost] and [ProducesResponseType]
to the new interface. There's one small catch. If you have a [Route] at the top of your
controller it can't be applied to an interface because of the way it is defined in ASP.Net Core.
There is a Route custom attribute in this assembly that subclasses the standard attribute and
can be applied to interfaces.

Unfortunately, ASP.Net Core doesn't consider attributes inherited from the interface. Maybe
in a future release they could consider doing this. This creates a slight duplication problem
because the same annotations must be applied to the interface as the controller and the compiler
doesn't catch discrepancies like changes to the method signature. In order to help maintain
the duplication the ServiceProxy has a ValidateControllers() method that will scan your assembly
and look for discrepancies. You can call the ServiceProxy.Validate() in your startup or program
to notify developers immediately when there is a problem. If you don't want to add this
dependency to your production code then you can add it to your integration test suite.

TBD screenshot here (show where you can insert the ValidateControllers method)

Once this structure is in place you can give your Client project in either source or binary form
and they can use it in conjunction with the Rest.ServiceProxy assembly to make REST calls to
your API using the .Net HTTP Client like this.

```csharp
ValuesService valuesService = ServiceProxy<ValuesService>.Create(new HttpClient(), "http://localhost:63333");
foreach (string value in valuesService.Get()) {
	Console.WriteLine(value);
}
```

This example is super simple, but you can see that the service proxy turns an HTTP Client, the
base URI and a service interface into a nice C# object that you can use to make REST calls.
It's much easier than wiring up HTTP requests directly, handling the marshaling/unmarshaling
of the entities and directly reading the status codes.

## Building good hybrid API's

It's a little tricky maintaining both a C# service and a REST API. The service proxy is
designed to make that much easier than having to maintain two code bases. There are some
thing that you can do to make the evolution of your API more flexible.

It is tempting to return the Model object directly from your controller methods. Much of the
online documentation has examples that do just that. The service proxy will work fine in this
mode. What happens if in the future you decide to return another type of model for certain
response codes, such as client and server errors. This is a super common practice since it
allows a user or client to report a message ID to help an administrator or developer to trace
the problem in the server logs. Exposing stack traces to end users is considered a bad practice
since it gives attackers the ability to probe your system for potential entry points. If you
return your Model object directly from your method then there is no opportunity for the
ServiceProxy to unmarshal the correct type for the error case. Instead, it can only throw an
exception with the raw response payload and expect the client know what type to unmarshal the
response. The service proxy can do alot better with a little help from you when you are
designing your controllers.

Instead of returning your model object directly you should return IActionResult.

```csharp
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<string>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ClientError), (int)HttpStatusCode.NotFound)]
        public IActionResult GetAllValues(int id)
        {
             if (id < 0)
             {
                  Debug.WriteLine("Client used an invalid value ID: " + id);
                  return NotFound(new ClientError("ID12345"));
             }
  
             /* Get all of the values with the id*/

             return Ok(values);
       }
```

The client has slightly more work to do in the success case, but not too much more. The benefit
is no compile errors in the future if the controller changes the return type of the method.
Also, the client doesn't need to unmarshal, only cast the value like this.

```csharp
var valuesService = ...
var valuesResult = valuesService.GetAllValues(-1);
if (valuesResult is ObjectResult)
{
    var objectResult = (ObjectResult)valuesResult;
    switch (objectResult.StatusCode)
    {
          case (int)HttpStatusCode.OK:
              foreach (string s in ((IEnumerable<string>)result.Value))
              {
                  Console.WriteLine(s);
              }
              break;
          case (int)HttpStatusCode.NotFound:
              var clientError = (ClientError)result.Value;
              // Give the message ID to the user
    }
}
else
{
   // In the future, but not at the moment the REST call may support
   //  other status codes with no response value. These will be in the
   //  for of StatusCodeResults with the status code.
   var statusCodeResult = (StatusCodeResult)valuesResult;
   if (statusCodeResult.StatusCode == (int)HttpStatusCodes.InternalServerError) {
       Debug.WriteLine("Internet server error trying to get the values");
   } 
}
```

The service proxy gets unmarshals to to the correct type using the ProducesResponseType. It is
good practice to keep those annotations up to date.

Since the service interfaces are C# API's and not just REST API's it is important to evolve the
C# methods in ways that avoid compile errors. For example, changing the arguments or return type
should be done in backward compatible ways so that you don't break your clients. integration
testing using the service proxy or unit testing using the controller is a good way to catch
problems. If a method is replaced with a new one the old one can be obsoleted using the
Obsolete custom attribute so that clients can adjust as needed.

