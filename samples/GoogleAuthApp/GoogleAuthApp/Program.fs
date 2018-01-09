module GoogleAuthApp.App

open System
open System.IO
open System.Net
open System.Security.Claims
open System.Security.Cryptography.X509Certificates
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Giraffe

// ---------------------------------
// HTTPS Config
// ---------------------------------

type EndpointScheme =
    | Http
    | Https

type EndpointConfiguration =
    {
        Host          : string
        Port          : int option
        Scheme        : EndpointScheme
        FilePath      : string option
        Password      : string option
        StoreName     : string option
        StoreLocation : string option
    }
    static member Default =
        {
            Host          = "localhost"
            Port          = Some 8080
            Scheme        = Http
            FilePath      = None
            Password      = None
            StoreName     = None
            StoreLocation = None
        }

let loadCertificateFromStore (storeName : string)
                             (location  : string)
                             (cfg       : EndpointConfiguration)
                             (env       : IHostingEnvironment) =
    use store = new X509Store(storeName, Enum.Parse<StoreLocation> location)
    store.Open OpenFlags.ReadOnly
    let cert =
        store.Certificates.Find(
            X509FindType.FindBySubjectName,
            cfg.Host,
            not (env.IsDevelopment()))
    match cert.Count with
    | 0 -> raise(InvalidOperationException(sprintf "Certificate not found for %s." cfg.Host))
    | _ -> cert.[0]

let loadCertificate (cfg : EndpointConfiguration) (env : IHostingEnvironment) =
    match cfg.StoreName, cfg.StoreLocation, cfg.FilePath, cfg.Password with
    | Some n, Some l,      _,      _ -> loadCertificateFromStore n l cfg env
    |      _,      _, Some f, Some p -> new X509Certificate2(f, p)
    |      _,      _, Some f, None   -> new X509Certificate2(f)
    | _ -> raise (InvalidOperationException("No valid certificate configuration found for the current endpoint."))

type KestrelServerOptions with
    member this.ConfigureEndpoints (endpoints : EndpointConfiguration list) =
        let env    = this.ApplicationServices.GetRequiredService<IHostingEnvironment>()
        endpoints
        |> List.iter (fun endpoint ->
            let port =
                match endpoint.Port with
                | Some p -> p
                | None   ->
                    match endpoint.Scheme.Equals "https" with
                    | true  -> 443
                    | false -> 80

            let ipAddresses =
                match endpoint.Host.Equals "localhost" with
                | true  -> [ IPAddress.IPv6Loopback; IPAddress.Loopback ]
                | false ->
                    match IPAddress.TryParse endpoint.Host with
                    | true, ip -> [ ip ]
                    | false, _ -> [ IPAddress.IPv6Any ]

            ipAddresses
            |> List.iter (fun ip ->
                this.Listen(ip, port, fun options ->
                    match endpoint.Scheme with
                    | Https ->
                        loadCertificate endpoint env
                        |> options.UseHttps
                        |> ignore
                    | Http  -> ()
                )
            )
        )

// ---------------------------------
// Web app
// ---------------------------------

type SimpleClaim = { Type: string; Value: string }

let authorize =
    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

let greet =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let claim = ctx.User.FindFirst "name"
        let name = claim.Value
        text ("Hello " + name) next ctx

let showClaims =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let claims = ctx.User.Claims
        let simpleClaims = Seq.map (fun (i : Claim) -> {Type = i.Type; Value = i.Value}) claims
        json simpleClaims next ctx

let webApp =
    choose [
        GET >=>
            choose [
                route "/"       >=> text "Public endpoint."
                route "/greet"  >=> authorize >=> greet
                route "/claims" >=> authorize >=> showClaims
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    app.UseAuthentication()
       .UseGiraffeErrorHandler(errorHandler)
       .UseStaticFiles()
       .UseGiraffe webApp

let authenticationOptions (o : AuthenticationOptions) =
    o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
    o.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme

let jwtBearerOptions (cfg : JwtBearerOptions) =
    cfg.SaveToken <- true
    cfg.IncludeErrorDetails <- true
    cfg.Authority <- "https://accounts.google.com"
    cfg.Audience <- "1076119972881-h90n9gouqih9p78h52vlp0o3t3lpfs44.apps.googleusercontent.com"
    cfg.TokenValidationParameters <- TokenValidationParameters (
        ValidIssuer = "accounts.google.com"
    )

let configureServices (services : IServiceCollection) =
    services
        .AddGiraffe()
        .AddAuthentication(authenticationOptions)
        .AddJwtBearer(Action<JwtBearerOptions> jwtBearerOptions) |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let endpoints =
        [
            EndpointConfiguration.Default
            { EndpointConfiguration.Default with
                Port     = Some 44340
                Scheme   = Https
                FilePath = Some "/Users/dustinmoris/Temp/https.pfx"
                Password = Some "Just4Now" } ]

    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel(fun o -> o.ConfigureEndpoints endpoints)
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0