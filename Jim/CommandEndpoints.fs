﻿module Jim.CommandEndpoints

open System

open MicroCQRS.Common.CommandAgent
open MicroCQRS.Common
open MicroCQRS.Common.CommandFailure
open MicroCQRS.Common.Result

open Jim.AppSettings
open Jim.CommandContracts
open Jim.Domain.CommandsAndEvents
open Jim.Domain.UserAggregate
open Jim.InMemoryUserRepository

open Suave
open Suave.Http
open Suave.Types
open Suave.Extensions.Json

(* If the system used a SQL database to maintain state for the user repository then the repository instance would not need to be shared between the command and query services, and the query service would not rely on the event store at all *)
let getCommandPosterAndRepository() =
    let streamId = appSettings.PrivateIdentityStream
    let store =
        match appSettings.UseEventStore with
        | true -> new MicroCQRS.Common.EventStore<Event>(streamId) :> IEventStore<Event>
        | false -> new MicroCQRS.Common.InMemoryStore<Event>() :> IEventStore<Event>
    let repository = new InMemoryUserRepository()
    let initialVersion = repository.Load(store, streamId) |> Async.RunSynchronously
    let postCommand = getCommandPoster store repository handleCommandWithAutoGeneration handleEvent streamId initialVersion
    
    postCommand, repository

let runCommand postCommand (command:Command) : Types.WebPart =
    fun httpContext ->
    async {
        let! result = postCommand command

        match result with
        | Success (UserCreated event) ->
            return! jsonOK ( { UserCreatedResponse.Id = event.Id; Message = "User created: " + extractUsername event.Name }) httpContext
        | Success (NameChanged event) ->
            return! jsonOK ( { GenericResponse.Message = "Name changed to: " + extractUsername event.Name }) httpContext
        | Success (EmailChanged event) ->
            return! jsonOK ( { GenericResponse.Message = "Email changed to: " + extractEmail event.Email }) httpContext
        | Success (PasswordChanged event) ->
            return! jsonOK ( { GenericResponse.Message = "Password changed" }) httpContext
        | Failure (BadRequest f) -> return! RequestErrors.BAD_REQUEST f httpContext
        | Failure NotFound -> return! genericNotFound httpContext
    }

let createUser postCommand (requestDetails:CreateUserRequest) =   
    runCommand postCommand (CreateUser {Name=requestDetails.name; Email=requestDetails.email; Password=requestDetails.password})

let setName postCommand (id:Guid) (requestDetails:SetNameRequest) =    
    runCommand postCommand (SetName{ Id=id; Name = requestDetails.name})

let setEmail postCommand (id:Guid) (requestDetails:SetEmailRequest) =
    runCommand postCommand ( SetEmail {Id = id; Email = requestDetails.email} )

let setPassword postCommand (id:Guid) (requestDetails:SetPasswordRequest) =    
    runCommand postCommand ( SetPassword{ Id=id; Password = requestDetails.password})