﻿namespace GenericErrorHandling

[<AutoOpen>]
module Result =
    type Result<'a, 'b> = 
        | Success of 'a
        | Failure of 'b

    let bind func result = 
        match result with
        | Success s -> func s
        | Failure f -> Failure f

    type ResultBuilder() =
        member this.Bind(x, f) =
            async {
                let! result = x 
                return bind f result
            }

        member this.Return(x) = 
            x            

    let resultBuilder = new ResultBuilder()
