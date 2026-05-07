param(
    [Parameter(Mandatory)][string]$Server,   # ex: user@192.168.1.10
    [string]$RemotePath = "~/webstatenotifier"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Copie des fichiers vers $Server`:$RemotePath ..."
ssh $Server "mkdir -p $RemotePath"
scp -r `
    ".\WebStateNotifier.csproj" `
    ".\Program.cs" `
    ".\Models" `
    ".\Services" `
    ".\Workers" `
    ".\appsettings.json" `
    ".\Dockerfile" `
    ".\docker-compose.yml" `
    ".\. dockerignore" `
    "${Server}:${RemotePath}/"

Write-Host "==> Lancement du conteneur sur le serveur ..."
ssh $Server "cd $RemotePath && docker compose up -d --build"

Write-Host ""
Write-Host "Deploiement termine. Logs en direct :"
ssh $Server "cd $RemotePath && docker compose logs -f"
