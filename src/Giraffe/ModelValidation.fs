namespace Giraffe

[<AutoOpen>]
module ModelValidation =

    /// <summary>
    /// Interface defining model validation methods.
    /// </summary>
    type IModelValidation<'T> =
        /// <summary>
        /// Contract for validating an object's state.
        ///
        /// If the object has a valid state then the function should return the object, otherwise it should return a `HttpHandler` function which is ought to return an error response back to a client.
        /// </summary>
        abstract member Validate : unit -> Result<'T, HttpHandler>

    /// <summary>
    /// Validates an object of type 'T where 'T must have implemented interface <see cref="IModelValidation{T}"/>.
    ///
    /// If validation was successful then object 'T will be passed into the <see cref="HttpHandler"/> function "f", otherwise an error response will be sent back to the client.
    /// </summary>
    /// <param name="f">A function which accepts the model 'T and returns a <see cref="HttpHandler"/> function.</param>
    /// <param name="model">An instance of type 'T, where 'T must implement interface <see cref="IModelValidation{T}"/>.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let validateModel<'T when 'T :> IModelValidation<'T>> (f : 'T -> HttpHandler) (model : 'T) : HttpHandler =
        match model.Validate() with
        | Ok _      -> f model
        | Error err -> err