[<AutoOpen>]
module Giraffe.ModelValidation

/// **Description**
///
/// Interface defining model validation methods.
///
type IModelValidation<'T> =
    /// **Description**
    ///
    /// Contract for validating an object's state.
    ///
    /// If the object has a valid state then the function should return the object, otherwise it should return a `HttpHandler` function which is ought to return an error response back to a client.
    ///
    abstract member Validate : unit -> Result<'T, HttpHandler>

/// **Description**
///
/// Validates an object of type `'T` where `'T` must have implemented interface `IModelValidation<'T>`.
///
/// If validation was successful then object `'T` will be passed into the `HttpHandler` function `f`, otherwise an error response will be sent back to the client.
///
/// **Parameters**
///
/// - `f`: A function which accepts the model `'T` and returns a `HttpHandler` function.
/// - `model`: An instance of type `'T`, where `'T` must implement interface `IModelValidation<'T>`.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let validateModel<'T when 'T :> IModelValidation<'T>> (f : 'T -> HttpHandler) (model : 'T) : HttpHandler =
    match model.Validate() with
    | Ok _      -> f model
    | Error err -> err