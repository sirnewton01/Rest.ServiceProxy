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
server then it can work against the controller directly avoiding the network overhead of HTTP
request handling and marshaling.

## Setting up your project
TBD
