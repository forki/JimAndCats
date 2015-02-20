﻿module EventStore.YetAnotherClient.CommandAgent

open EventStore.YetAnotherClient
open GenericErrorHandling

let getCommandPoster<'TCommand, 'TEvent, 'TRepository>
    (store:IEventStore<'TEvent>)
    (repository:'TRepository)
    (handleCommand:'TCommand -> 'TRepository -> Async<Result<'TEvent, CQRSFailure>>)
    (handleEvent:'TRepository -> 'TEvent -> Async<unit>)
    (streamId:string)
    (initialVersion:int) = 
    
    let save expectedVersion events = store.AppendToStream streamId expectedVersion events

    let agent = MailboxProcessor<'TCommand * AsyncReplyChannel<Result<'TEvent, CQRSFailure>>>.Start <| fun inbox -> 
        let rec messageLoop version = async {
            let! command, replyChannel = inbox.Receive()
            
            let! result = handleCommand command repository
            match result with
            | Success newEvent ->
                do! save version [newEvent]
                handleEvent repository newEvent |> ignore
                replyChannel.Reply(result)
                return! messageLoop (version + 1)
            | Failure f ->
                replyChannel.Reply(result)
                return! messageLoop version            
            }
        async {          
            return! messageLoop initialVersion
            }

    fun command -> agent.PostAndAsyncReply(fun replyChannel -> command, replyChannel)