# WebStateNotifier

Service de surveillance de sites web qui envoie des notifications par email lorsqu'un site devient inaccessible, puis lorsqu'il est rétabli.

Il surveille autant de sites que voulu, chacun avec son propre groupe de destinataires : la panne d'un site ne prévient que le groupe de ce site.

---

## Fonctionnement

Chaque site est surveillé indépendamment, avec son propre état (compteur d'échecs, panne en cours) :

```
Site UP  ──[CheckingDownDelay]──▶ vérification ──▶ toujours UP  ──▶ boucle
                                               └──▶ échec ──[CheckingUpDelay]──▶ revérification
                                                         │  (FailuresBeforeDown échecs de suite)
                                                    DOWN confirmé ──▶ email d'alerte au groupe du site
                                                         │
                                               [CheckingUpDelay]
                                                         │
                                                    vérification ──▶ toujours DOWN ──▶ boucle
                                                               └──▶ UP rétabli ──▶ email de rétablissement au groupe du site
```

---

## Déploiement

### Prérequis serveur

- Docker ≥ 24
- Docker Compose ≥ 2
- Accès SSH avec clé autorisée

Le projet Compose s'appelle `webstatenotifier` (attribut `name` de `docker-compose.yml`), quel que soit le nom du dossier où le dépôt est cloné.

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

Le script copie les sources dans `~/webstatenotifier` (paramètre `-RemotePath`) : le fichier `.env` doit déjà s'y trouver.

---

### Option 3 — Sur le serveur directement

```bash
git clone https://github.com/Swordsouler/WebStateNotifier.git
cd WebStateNotifier

# Configuration (voir section Variables d'environnement)
cp .env.example .env
nano .env

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

Au démarrage, les logs listent chaque site surveillé, son nombre de destinataires et son seuil d'alerte.

---

## Configuration

Toute la configuration se fait via des **variables d'environnement**, dans un fichier `.env` que `docker-compose.yml` lit (`env_file`).

```bash
cp .env.example .env
# Éditer .env avec vos valeurs
```

Une erreur de configuration (URL invalide, site sans destinataire, adresse email mal formée…) arrête le service au démarrage avec un message qui nomme la variable à corriger.

---

## Variables d'environnement

### Sites surveillés — `Monitors`

Un bloc de variables par site, numérotés à partir de `0` : `Monitors__0__…`, `Monitors__1__…`, etc.

| Variable | Défaut | Obligatoire | Description |
|---|---|:---:|---|
| `Monitors__0__Url` | _(vide)_ | ✅ | URL à surveiller (avec schéma `https://`) |
| `Monitors__0__Name` | hôte de l'URL | | Nom du site dans les emails et les logs |
| `Monitors__0__Recipients__0` | _(vide)_ | ✅ | Premier destinataire des alertes de ce site |
| `Monitors__0__Recipients__1` | _(vide)_ | | Deuxième destinataire (ajouter `__2`, `__3`… pour plus) |
| `Monitors__0__FailuresBeforeDown` | `Monitor__FailuresBeforeDown` | | Nombre d'échecs de suite avant l'alerte, pour ce site seulement |

Exemple : StreamBlades, dont les pannes ne préviennent qu'une adresse, à côté d'un autre site et de son équipe :

```env
Monitors__0__Name=StreamBlades
Monitors__0__Url=https://streamblades.com/health
Monitors__0__Recipients__0=swordsouler.nsl@gmail.com

Monitors__1__Name=Mon autre site
Monitors__1__Url=https://example.com
Monitors__1__Recipients__0=admin@example.com
Monitors__1__Recipients__1=ops@example.com
Monitors__1__FailuresBeforeDown=5
```

Une panne de StreamBlades ne prévient que son unique destinataire ; une panne de l'autre site ne prévient que ses deux destinataires.

### Réglages communs — `Monitor`

| Variable | Défaut | Obligatoire | Description |
|---|---|:---:|---|
| `Monitor__CheckingDownDelaySeconds` | `60` | | Intervalle en secondes entre chaque vérification quand le site est **UP** |
| `Monitor__CheckingUpDelaySeconds` | `30` | | Intervalle en secondes entre chaque vérification quand le site est **DOWN** ou vient d'échouer |
| `Monitor__FailuresBeforeDown` | `3` | | Nombre d'échecs de suite avant d'envoyer l'alerte, pour les sites qui ne fixent pas le leur : un échec isolé ne prévient personne |
| `Monitor__HttpTimeoutSeconds` | `10` | | Délai maximum d'attente par requête HTTP |
| `Monitor__TimeZoneId` | `UTC` | | Fuseau horaire des dates dans les emails (format IANA) |

### Ancien format à un seul site

Les configurations écrites avant la surveillance de plusieurs sites marchent telles quelles : `Monitor__Url` et `Monitor__Emails__N` forment un site à part entière, surveillé en plus des `Monitors__N`.

| Variable | Défaut | Description |
|---|---|---|
| `Monitor__Url` | _(vide)_ | URL à surveiller |
| `Monitor__Name` | hôte de l'URL | Nom du site dans les emails et les logs |
| `Monitor__Emails__0` | _(vide)_ | Premier destinataire (ajouter `__1`, `__2`… pour plus) |

Ce site utilise `Monitor__FailuresBeforeDown`. Pour passer au nouveau format, déplacer ces valeurs dans un bloc `Monitors__N__…` (les `Emails` deviennent des `Recipients`) puis supprimer `Monitor__Url`, `Monitor__Name` et `Monitor__Emails__N`, sinon le site serait surveillé deux fois.

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

## Tests

```bash
dotnet test tests/WebStateNotifier.Tests
```

Ils couvrent la lecture de la configuration (nouveau format et ancien), le comptage des échecs site par site et l'envoi des alertes au seul groupe du site concerné.

---

## Structure du projet

```
WebStateNotifier/
├── Models/
│   ├── MonitorOptions.cs         # Réglages communs (et ancien format à un seul site)
│   ├── SiteMonitorOptions.cs     # Un site de la liste Monitors
│   ├── SiteMonitor.cs            # Un site validé, prêt à surveiller
│   └── SmtpOptions.cs            # Paramètres SMTP
├── Services/
│   ├── MonitorConfiguration.cs   # Lecture et validation des sites à surveiller
│   ├── EmailService.cs           # Envoi des emails HTML
│   └── UrlHealthChecker.cs       # Vérification HTTP
├── Workers/
│   ├── MonitorWorker.cs          # Lance la surveillance de chaque site (BackgroundService)
│   └── SiteMonitorRunner.cs      # Boucle et état de surveillance d'un site
├── tests/
│   └── WebStateNotifier.Tests/   # Tests xUnit
├── Program.cs                    # Point d'entrée et injection de dépendances
├── appsettings.json              # Valeurs par défaut
├── Dockerfile                    # Build multi-stage, conteneur non-root
├── docker-compose.yml            # Déploiement Docker
├── .env.example                  # Template de configuration
└── deploy.ps1                    # Script de déploiement PowerShell
```
