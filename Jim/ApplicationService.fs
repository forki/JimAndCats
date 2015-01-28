﻿namespace Jim.ApplicationService

open EventPersistence
open Jim.ApiResponses
open Jim.AppSettings
open Jim.AuthenticationService
open Jim.Domain
open Jim.ErrorHandling
open Jim.UserModel
open Jim.UserRepository
open System

type Query =
    | ListUsers of AsyncReplyChannel<User seq>
    | GetUser of Guid * AsyncReplyChannel<User option>
    | Authenticate of Authenticate * AsyncReplyChannel<Result<unit,string>>

type Message =
    | CommandMessage of Command * AsyncReplyChannel<Result<Event, string>>
    | Query of Query

type AppService(store:IEventStore<Event>, streamId) =
    let load repository =
        let rec fold version =
            async {
            let! events, lastEvent, nextEvent = 
                store.ReadStream streamId version 500

            List.iter (handleEvent repository) events
            match nextEvent with
            | None -> return lastEvent
            | Some n -> return! fold n }
        fold 0

    let save expectedVersion events = store.AppendToStream streamId expectedVersion events    

    let agent = MailboxProcessor<Message>.Start <| fun inbox -> 
        let rec messageLoop version repository = async {
            let! message = inbox.Receive()

            match message with
            | CommandMessage (command, replyChannel) ->
                let result = handleCommandWithAutoGeneration command repository
                match result with
                | Success newEvent ->
                    do! save version [newEvent]
                    handleEvent repository newEvent
                    replyChannel.Reply(result)
                    return! messageLoop (version + 1) repository
                | Failure f ->
                    replyChannel.Reply(result)
                    return! messageLoop version repository

            | Query query ->
                match query with
                | ListUsers replyChannel -> replyChannel.Reply(repository.List())
                | GetUser (id, replyChannel) -> replyChannel.Reply(repository.Get(id))
                | Authenticate (details, replyChannel) -> replyChannel.Reply(authenticate details repository)

                return! messageLoop version repository
            }
        async {
            let repository = new Repository()
            let! version = load repository
            return! messageLoop version repository
            }

    let makeCommandMessage (command:Command) = 
        fun replyChannel ->
            CommandMessage (command, replyChannel)

    let mapUserToUserResponse user =
        {
            GetUserResponse.Id = user.Id
            Name = extractUsername user.Name
            Email = extractEmail user.Email
            CreationTime = user.CreationTime.ToString()
        }

    new() =
        let streamId = appSettings.UserStream

        let projection = fun (x: Event) -> ()

        let store =
            match appSettings.UseEventStore with
            | true -> new EventPersistence.EventStore<Event>(streamId, projection) :> IEventStore<Event>
            | false -> new EventPersistence.InMemoryStore<Event>(projection) :> IEventStore<Event>
        AppService(store, streamId)

    (* Commands. If the query model wasn't in memory there would be likely be two separate processes for command and query. *)

    member this.runCommand(command:Command) =
        async {
            let! result = agent.PostAndAsyncReply(makeCommandMessage command)

            match result with
            | Success (UserCreated event) ->
                return OK ( { UserCreatedResponse.Id = event.Id; Message = "User created: " + extractUsername event.Name })
            | Success (NameChanged event) ->
                return OK ( { GenericResponse.Message = "Name changed to: " + extractUsername event.Name })
            | Success (EmailChanged event) ->
                return OK ( { GenericResponse.Message = "Email changed to: " + extractEmail event.Email })
            | Success (PasswordChanged event) ->
                return OK ( { GenericResponse.Message = "Password changed" })
            | Failure f -> return BadRequest ({ GenericResponse.Message = f})
        }

    member this.authenticate(details:Authenticate) =
        let makeMessage details = 
            fun replyChannel -> Query(Authenticate (details, replyChannel))
        async {
            let! result = agent.PostAndAsyncReply(makeMessage details)
            return OK ({ GenericResponse.Message ="TODO"})
        }

    (* End commands *)

    (* Queries *)

    member this.getUser(id) =
        async {
            let! result = agent.PostAndAsyncReply(fun replyChannel -> Query(GetUser(id, replyChannel)))

            match result with
            | Some user -> return OK (mapUserToUserResponse user)
            | None -> return NotFound
        }

    member this.listUsers() =
        async {
            let! users = agent.PostAndAsyncReply(fun replyChannel -> Query(ListUsers(replyChannel)))
            return OK ({GetUsersResponse.Users = (users |> Seq.map mapUserToUserResponse)})
        }

    (* End queries *)
