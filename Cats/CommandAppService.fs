﻿module Cats.CommandAppService

open System

open EventStore.YetAnotherClient
open GenericErrorHandling

open Cats.AppSettings
open Cats.CommandContracts
open Cats.Domain.CatAggregate
open Cats.Domain.CommandsAndEvents

open Suave
open Suave.Http
open Suave.Extensions.Json

let getCommandPosterAndRepository() =
    let streamId = appSettings.PrivateCatStream
    let store =
        match appSettings.WriteToInMemoryStoreOnly with
        | false -> new EventStore<Event>(appSettings.PrivateEventStoreIp, appSettings.PrivateEventStorePort) :> IEventStore<Event>
        | true -> new InMemoryStore<Event>() :> IEventStore<Event>
    let repository = new SimpleInMemoryRepository<Cat>()
    let initialVersion = repository.Load<Event>(store, streamId, handleEvent) |> Async.RunSynchronously
    let postCommand = EventStore.YetAnotherClient.CommandAgent.getCommandPoster store repository handleCommandWithAutoGeneration handleEvent streamId initialVersion   
    
    postCommand, repository

let private runCommand postCommand (command:Command) : Types.WebPart =
    fun httpContext ->
    async {
        let! result = postCommand command

        match result with
        | Success (CatCreated event) ->
            return! jsonResponse Successful.CREATED ( { CatCreatedResponse.Id = event.Id; Message = "CAT created" }) httpContext
        | Success (TitleChanged event) ->
            return! jsonOK ( { GenericResponse.Message = "CAT renamed" }) httpContext
        | Failure (BadRequest f) -> return! RequestErrors.BAD_REQUEST f httpContext
        | Failure NotFound -> return! genericNotFound httpContext
    }

let createCat postCommand (request:CreateCatRequest) =   
    runCommand postCommand (CreateCat { CreateCat.Title=request.title; Owner=request.Owner })

let setTitle postCommand (id:Guid) (request:SetTitleRequest) =   
    runCommand postCommand (SetTitle {Id=id; Title=request.title})