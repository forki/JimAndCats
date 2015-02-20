#Jim and Cats

A bunch of F# microservices with a web API.

If this was a real thing with lots of developers they would be in separate Visual Studio solutions, but as a personal project it's easier to just keep them all together.

###The services are:

####Jim.CommandHandler: Just Identity Management Command Handler

* Manages commands relating to user authentication details and basic identity info (i.e. people's full name).

* Commands result in events being written to a private stream in an Event Store cluster. An Event Store projection takes the private identity events resulting from commands and maps them to new events on a public stream for use by other services (currently the Cats service). All data and events relating to password hashes are omitted.

* After any events resulting from a command have been written to the Event Store cluster, the service updates the current state of the relevant user aggregate in a SQL Server table. This table is used both by the command handler itself (to check the legality of commands before executing them) and by the Jim query handler service.

* Commands are intentionally synchronous (via an F# MailboxProcessor) to avoid the creation of conflicting users via concurrent events (e.g. two users with the same email address). Because of this the service is not currently horizontally scalable. Given that the identity read model is scalable, this doesn't matter unless either:

a) any downtime on the ability to create and modify users is considered totally unacceptable

or

b) There is an expectation of an extremely high number of user creation and modification commands

If either of these is considered a problem, then there would need to be some logic to handle the case where conflicting events have both been written to the Event Store - e.g. two users with the same email address have been created.

####Jim.QueryHandler: Just Identity Management Query Handler

* Verifies auth tokens for other microservices.

* Also allows admins to query user details (currently all users are admins...). All queries are served via SQL Server - this service does not require any knowledge of Event Store.

####Cats: Crowdfunding Ask Templates

* Manages a collection of projects asking for crowdfunding.

* Handles both queries and commands. It can be horizontally scaled across multiple instances because there is no command that will get the system into an illegal state, even when the aggregate checked by a command handler is out of date.

####Cats.ReadModelUpdater:

* Listens to the private Cats event stream, the public identity event stream and the public pledges event stream. It uses them to update SQL Server tables for use by Cats services. There can only be one running instance to avoid conflicting writes to the read models.

####Pledges:

* Allows people to make pledges to cats. It can be horizontally scaled across multiple instances because there is no command that will get the system into an illegal state.

* Events are written to a private event stream, where an Event Store projection maps them to a public stream for consumption by other services (currently the Cats service).

* Handles only commands.

###There are also some shared libraries:

####GenericErrorHandling

* Description TODO

####Jim.UserRepository

* Used by both Jim.QueryHandler and Jim.CommandHandler. Contains both a SQL Server implementation and an in-memory implementation. The in-memory implementation is chiefly for use by tests, though unless there are many millions of users there is no technical reason in-memory data stores can't be used in production environments (alongside EventStore checkpoints for quick restore of new/restarted nodes), which would make the architecture much simpler.

####EventStore.YetAnotherClient

* Description TODO

####Suave.Extensions

Some handy utilities for making Suave web services.

####TestingHelpers

Some utility methods using the official Suave.Testing library for writing tests on web server endpoints, as well as some minimal BDD helper functions for testing domain logic.

###Other:
Most of the projects have an associated unit test project. These do not require access to a real EventStore instance, and run any required web server in-process.

There is also a separate solution called IntegrationTests in its own folder, containing tests which start the services in separate processes and interact with them soley via REST. These tests verify that the different services are successfully coordinated via EventStore.