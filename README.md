# WebStateNotifier

Service de surveillance d'URL qui envoie des notifications par email lorsqu'un site devient inaccessible, puis lorsqu'il est rétabli.

---

## Fonctionnement

```
Site UP  ──[CheckingDownDelay]──▶ vérification ──▶ toujours UP  ──▶ boucle
                                               └──▶ DOWN détecté ──▶ email d'alerte
                                                         │
                                               [CheckingUpDelay]
                                                         │
                                                    vérification ──▶ toujours DOWN ──▶ boucle
                                                               └──▶ UP rétabli ──▶ email de rétablissement
```

---

## Déploiement

### Prérequis serveur

- Docker ≥ 24
- Docker Compose ≥ 2
- Accès SSH avec clé autorisée

---

### Option 1 — Docker Context (depuis votre poste local)

Déploie directement sur le serveur sans copier de fichiers manuellement.

```bash
# Configuration initiale (une seule fois)
docker context create myserver --docker "host=ssh://user@ip-du-serveur"

# Déploiement
docker compose --context myserver up -d --build

# Logs en direct
docker compose --context myserver logs -f
```

---

### Option 2 — Script PowerShell

```powershell
.\deploy.ps1 -Server user@ip-du-serveur
```

---

### Option 3 — Sur le serveur directement

```bash
git clone https://github.com/Swordsouler/WebStateNotifier.git webstatenotifier
cd webstatenotifier

# Éditer la configuration (voir section Variables d'environnement)
nano docker-compose.yml

docker compose up -d --build
```

---

### Commandes utiles

```bash
# Voir les logs
docker compose logs -f

# Redémarrer le service
docker compose restart

# Arrêter le service
docker compose down

# Mettre à jour (reconstruire l'image)
docker compose up -d --build
```

---

## Configuration

Toute la configuration se fait via les **variables d'environnement** dans `docker-compose.yml`.

### Méthode recommandée : fichier `.env`

```bash
cp .env.example .env
# Éditer .env avec vos valeurs
```

Puis décommenter dans `docker-compose.yml` :

```yaml
env_file:
  - .env
```

---

## Variables d'environnement

### Surveillance — `Monitor`

| Variable | Défaut | Obligatoire | Description |
|---|---|:---:|---|
| `Monitor__Url` | _(vide)_ | ✅ | URL à surveiller (avec schéma `https://`) |
| `Monitor__Emails__0` | _(vide)_ | ✅ | Premier email destinataire |
| `Monitor__Emails__1` | _(vide)_ | | Deuxième email (ajouter `__2`, `__3`… pour plus) |
| `Monitor__CheckingDownDelaySeconds` | `60` | | Intervalle en secondes entre chaque vérification quand le site est **UP** |
| `Monitor__CheckingUpDelaySeconds` | `30` | | Intervalle en secondes entre chaque vérification quand le site est **DOWN** |
| `Monitor__HttpTimeoutSeconds` | `10` | | Délai maximum d'attente par requête HTTP |
| `Monitor__TimeZoneId` | `UTC` | | Fuseau horaire des dates dans les emails (format IANA) |

### SMTP — `Smtp`

| Variable | Défaut | Obligatoire | Description |
|---|---|:---:|---|
| `Smtp__Host` | _(vide)_ | ✅ | Adresse du serveur SMTP |
| `Smtp__Port` | `587` | | Port SMTP |
| `Smtp__Username` | _(vide)_ | | Nom d'utilisateur SMTP (laisser vide si non requis) |
| `Smtp__Password` | _(vide)_ | | Mot de passe SMTP |
| `Smtp__FromAddress` | _(vide)_ | ✅ | Adresse email de l'expéditeur |
| `Smtp__FromName` | `WebStateNotifier` | | Nom affiché de l'expéditeur |
| `Smtp__UseSsl` | `true` | | Activer StartTLS (`true` / `false`) |

### Logs — `Logging`

| Variable | Défaut | Description |
|---|---|---|
| `Logging__LogLevel__Default` | `Information` | Niveau de verbosité (`Trace`, `Debug`, `Information`, `Warning`, `Error`) |

---

## Exemples de configuration SMTP

### Gmail

```env
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__Username=votre.adresse@gmail.com
Smtp__Password=xxxx xxxx xxxx xxxx   # Mot de passe d'application Google
Smtp__FromAddress=votre.adresse@gmail.com
Smtp__UseSsl=true
```

> Générer un mot de passe d'application : [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)

### Outlook / Office 365

```env
Smtp__Host=smtp.office365.com
Smtp__Port=587
Smtp__Username=votre.adresse@outlook.com
Smtp__Password=votre_mot_de_passe
Smtp__FromAddress=votre.adresse@outlook.com
Smtp__UseSsl=true
```

### Serveur SMTP sans authentification (réseau interne)

```env
Smtp__Host=192.168.1.100
Smtp__Port=25
Smtp__Username=
Smtp__Password=
Smtp__FromAddress=notifier@mondomaine.local
Smtp__UseSsl=false
```

---

## Structure du projet

```
WebStateNotifier/
├── Models/
│   ├── MonitorOptions.cs       # Paramètres de surveillance
│   └── SmtpOptions.cs          # Paramètres SMTP
├── Services/
│   ├── EmailService.cs         # Envoi des emails HTML
│   └── UrlHealthChecker.cs     # Vérification HTTP
├── Workers/
│   └── MonitorWorker.cs        # Boucle de surveillance (BackgroundService)
├── Program.cs                  # Point d'entrée et injection de dépendances
├── appsettings.json            # Valeurs par défaut
├── Dockerfile                  # Build multi-stage, conteneur non-root
├── docker-compose.yml          # Déploiement Docker
├── .env.example                # Template de configuration
└── deploy.ps1                  # Script de déploiement PowerShell
```
