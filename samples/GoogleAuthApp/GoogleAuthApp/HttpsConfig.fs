module GoogleAuthApp.HttpsConfig

open System
open System.Net
open System.Security.Cryptography.X509Certificates
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

// Follow the following instructions to set up
// a self signed certificate for localhost:
// https://blogs.msdn.microsoft.com/webdev/2017/11/29/configuring-https-in-asp-net-core-across-different-platforms/

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
                             (env       : IWebHostEnvironment) =
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

let loadCertificate (cfg : EndpointConfiguration) (env : IWebHostEnvironment) =
    match cfg.StoreName, cfg.StoreLocation, cfg.FilePath, cfg.Password with
    | Some n, Some l,      _,      _ -> loadCertificateFromStore n l cfg env
    |      _,      _, Some f, Some p -> new X509Certificate2(f, p)
    |      _,      _, Some f, None   -> new X509Certificate2(f)
    | _ -> raise (InvalidOperationException("No valid certificate configuration found for the current endpoint."))

type KestrelServerOptions with
    member this.ConfigureEndpoints (endpoints : EndpointConfiguration list) =
        let env    = this.ApplicationServices.GetRequiredService<IWebHostEnvironment>()
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