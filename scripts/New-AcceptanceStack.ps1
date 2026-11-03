$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$true
Set-Location (Join-Path $PSScriptRoot '..')
$privateRoot=Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103/acceptance-private'
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
if(Test-Path (Join-Path $privateRoot 'state.json')) { throw 'An acceptance stack already exists; clean it up before creating another.' }
$postgres=(docker inspect pickleverse_postgres | ConvertFrom-Json)[0]
$pg=@{};foreach($entry in $postgres.Config.Env){$pair=$entry.Split('=',2);$pg[$pair[0]]=$pair[1]}
if($postgres.Config.Image -ne 'postgres:16-alpine' -or $pg.POSTGRES_DB -ne 'pickleverse' -or $pg.POSTGRES_USER -ne 'sa'){throw 'Wrong development PostgreSQL instance.'}
$network=@($postgres.NetworkSettings.Networks.PSObject.Properties.Name)[0]
$suffix=[guid]::NewGuid().ToString('N')
$database='pkv_acceptance_'+$suffix
$bucket='pkv-acceptance-'+$suffix.Substring(0,12)
$api='pkv-acceptance-api-p18';$mail='pkv-acceptance-mail-p18';$worker='pkv-acceptance-worker-p18'
foreach($name in @($api,$mail,$worker)){
    if(@(docker ps -a --format '{{.Names}}') -contains $name){throw "Container name is already used: $name"}
}
$jwt=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$serverKey=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$rsa=[Security.Cryptography.RSA]::Create(2048)
$request=[Security.Cryptography.X509Certificates.CertificateRequest]::new("CN=$mail",$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true,$false,0,$true))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment -bor [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign,$true))
$san=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new();$san.AddDnsName($mail)
$request.CertificateExtensions.Add($san.Build())
$certificate=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(2))
[IO.File]::WriteAllText((Join-Path $privateRoot 'mailpit-cert.pem'),$certificate.ExportCertificatePem())
[IO.File]::WriteAllText((Join-Path $privateRoot 'mailpit-key.pem'),$rsa.ExportPkcs8PrivateKeyPem())
$rsa.Dispose();$certificate.Dispose()
$state=@{database=$database;bucket=$bucket;api=$api;mail=$mail;worker=$worker;network=$network;jwt=$jwt;serverKey=$serverKey;label='com.pickleverse.acceptance=20261103-p18';apiUrl='http://127.0.0.1:28080';mailUrl='http://127.0.0.1:28025'}
[IO.File]::WriteAllText((Join-Path $privateRoot 'state.json'),($state|ConvertTo-Json))
docker exec pickleverse_postgres psql -U sa -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $database OWNER sa;"
docker cp src/Database.sql "pickleverse_postgres:/tmp/$database.sql"
docker exec pickleverse_postgres psql -U sa -d $database -v ON_ERROR_STOP=1 --file="/tmp/$database.sql" | Out-Null
docker exec pickleverse_postgres rm -f "/tmp/$database.sql"
[IO.File]::WriteAllText((Join-Path $privateRoot 'Dockerfile'),"FROM pickleverse-api:20261103-p18`nUSER root`nCOPY mailpit-cert.pem /usr/local/share/ca-certificates/pickleverse-acceptance.crt`nRUN update-ca-certificates`n")
docker build -t pickleverse-api:acceptance-p18 -f (Join-Path $privateRoot 'Dockerfile') $privateRoot
docker run -d --name $mail --label $state.label --network $network -p 127.0.0.1:28025:8025 --mount "type=bind,source=$privateRoot,target=/certs,readonly" -e MP_SMTP_TLS_CERT=/certs/mailpit-cert.pem -e MP_SMTP_TLS_KEY=/certs/mailpit-key.pem -e MP_SMTP_REQUIRE_STARTTLS=true -e MP_SMTP_AUTH_ACCEPT_ANY=true axllent/mailpit:latest
$apiEnv=@(
    'ASPNETCORE_ENVIRONMENT=Development','ASPNETCORE_URLS=http://+:8080',
    "ConnectionStrings__DefaultConnection=Host=pickleverse_postgres;Port=5432;Database=$database;Username=$($pg.POSTGRES_USER);Password=$($pg.POSTGRES_PASSWORD);Maximum Pool Size=20;GSS Encryption Mode=Disable",
    'Redis__ConnectionString=pickleverse_redis:6379,abortConnect=false',"Redis__KeyPrefix=pickleverse:acceptance:$suffix`:",
    "Storage__BucketName=$bucket",'Storage__ServiceUrl=http://pickleverse_s3:9000','Storage__AccessKey=minioadmin','Storage__SecretKey=minioadmin','Storage__ForcePathStyle=true',
    "Jwt__Key=$jwt",'DedicatedServer__Enabled=true',"DedicatedServer__ServiceKey=$serverKey",'DedicatedServer__Protocol=18',
    'HybridAi__IngestEnabled=false','HybridAi__DispatcherEnabled=false','HybridAi__CoachingEnabled=false','HybridAi__ModelEnabled=false',
    "Smtp__Host=$mail",'Smtp__Port=1025','Smtp__FromEmail=qa@pickleverse.invalid','Smtp__FromName=PickleVerse','Smtp__Username=qa','Smtp__Password=acceptance-only'
)
[IO.File]::WriteAllLines((Join-Path $privateRoot 'api.env'),$apiEnv)
docker run -d --name $api --label $state.label --network $network -p 127.0.0.1:28080:8080 --env-file (Join-Path $privateRoot 'api.env') pickleverse-api:acceptance-p18
[IO.File]::WriteAllLines((Join-Path $privateRoot 'worker.env'),@("PKV_BACKEND_URL=http://$api`:8080","PKV_GAME_SERVER_KEY=$serverKey",'PKV_GAME_PORT=3669'))
docker run -d --name $worker --label $state.label --network $network --cpus 2 --memory 2g -p 127.0.0.1:3669:3669/udp --env-file (Join-Path $privateRoot 'worker.env') pickleverse-game-server:20261103-p18
Write-Output "Acceptance stack created on localhost:28080. Database: $database. SMTP remains inside Docker with STARTTLS."
