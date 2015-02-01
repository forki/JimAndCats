#Jim and Cats

A bunch of F# microservices with a web API.

They would be in separate Visual Studio solutions if this was a real thing with lots of developers, but as a one person personal project it's easier to just keep them all together.

###The services are:

Jim: Just Identity Management. Manages authentication and basic user details.

Cats: Crowdfunding Ask Templates. Manages a collection of projects asking for crowdfunding.

Pledges: Allows people to make pledges to cats

###There are also some shared libraries:

MicroCQRS.Common - some generic code for making F# microservices backed by EventStore and fronted by a Suave web server

Suave.Extensions - some handy utilities for making Suave web services

MicroCQRS.Common.Testing - some utilities for testing Suave/EventStore microservices

###And some test projects

Almost all the projects have an associated unit test project. These do not require access to a real EventStore instance, and run any required web server in-process.

There is also a separate solution called IntegrationTests in its own folder, containing tests which start the services in separate processes and interact with them soley via REST. These tests verify that the different services are successfully coordinated via EventStore.

###And some scripts in their own folder