# Deploying VocabularyBuilder to the VPS

The app ships as a single container: one ASP.NET Core process serving both the API and the
built React app, with a SQLite file on a named volume. There is no database server to run.

This assumes a VPS that already has Docker with the compose plugin, a `deploy` user in the
`docker` group, Caddy holding ports 80/443 as the only public entry point, and ufw defaulting
to deny. If any of that is missing, set it up first — this guide only covers what this app
adds.

## What goes where

```
/srv/vocabulary-builder/
├── app/                    # this repo, cloned
├── docker-compose.yml      # copy of docker-compose.prod.yml from the repo
└── .env                    # secrets, never in git
```

> Keep `docker-compose.yml` **outside** `app/`. Running compose from inside the checkout would
> pick up whatever compose file the repo has at that moment, and a `git pull` would then be
> able to change how the running stack is configured.

| | |
| --- | --- |
| Container | `vocabulary-builder` |
| Port | `127.0.0.1:8087` → 8080 in the container |
| Volume | `vocabulary_builder_data` → `/data` |
| Health | `GET /health` |

Pick a host port nothing else is using, and change it in both `docker-compose.yml` and the
Caddy entry together.

> **Security rule**: the port binding must keep the `127.0.0.1:` prefix. Docker writes its own
> iptables rules and does not consult ufw, so a bare `"8087:8080"` publishes the app to the
> internet no matter what the firewall says. The binding is what keeps it private, and Caddy
> reaches it on the same host.

## First deployment

### 1. Give the VPS read access to the repo

As `deploy`, make a key for this repo and add the public half under the repository's
**Settings → Deploy keys** (read-only is enough):

```bash
ssh-keygen -t ed25519 -C "vps-vocabulary-pull" -f ~/.ssh/vps-vocabulary-pull
cat ~/.ssh/vps-vocabulary-pull.pub
```

Add a host alias to `~/.ssh/config` so git picks the right key on its own:

```
Host github-vocabulary
  HostName github.com
  User git
  IdentityFile ~/.ssh/vps-vocabulary-pull
  IdentitiesOnly yes
```

```bash
chmod 600 ~/.ssh/vps-vocabulary-pull ~/.ssh/config
ssh -T git@github-vocabulary     # expect "successfully authenticated"
```

### 2. Clone and place the compose file

```bash
sudo mkdir -p /srv/vocabulary-builder
sudo chown -R deploy:deploy /srv/vocabulary-builder

su - deploy
cd /srv/vocabulary-builder
git clone git@github-vocabulary:yuriimustafin/vocabulary-builder.git app
cp app/VocabularyBuilder/docker-compose.prod.yml docker-compose.yml
```

The compose file builds from `./app`, so point its build context at the solution directory:

```yaml
    build:
      context: ./app/VocabularyBuilder
```

### 3. Write the secrets

**Not in `appsettings.Production.json`.** That file is committed - only
`appsettings.Development.json` is gitignored, which is why a key can sit in that one on a
developer machine. A secret put in the Production file would be pushed to GitHub, and baked
into the image on top of that, since the build copies the published output in.

Secrets reach the container as environment variables instead. ASP.NET reads
`appsettings.json`, then `appsettings.<Environment>.json`, then the environment - so an
environment variable wins over both files, and nothing secret has to be written down inside
the image. The same mechanism is what points the app at the volume: the connection string in
`appsettings.Production.json` still says `VocabularyBuilder.Prod.db`, and the
`ConnectionStrings__DefaultConnection` set in `docker-compose.yml` overrides it.

`/srv/vocabulary-builder/.env`:

```env
# Used for study content, and for the import step that reads free-form lesson notes.
# Without it those features fail; nothing else does.
OpenAI__ApiKey=<key>

# The administrator, created on first start. The password is only used to create the account
# and is not re-applied afterwards. It has to satisfy Identity's rules - at least 6 characters
# with an upper and a lower case letter, a digit and a symbol - or the container stops at
# startup saying why.
Admin__Email=you@example.com
Admin__Password=<password>

# Who else may create an account, separated by commas. Nobody else can register - every
# account can spend the OpenAI key. Leave it empty to keep the site to the administrator.
Registration__AllowedEmails=friend@example.com, colleague@example.com
```

```bash
chmod 600 /srv/vocabulary-builder/.env
```

The allowlist is read on each registration, so a changed list takes effect on
`docker compose up -d` without a rebuild. Taking an address off it stops new registrations
only: an account that already exists keeps working.

Everything else the container needs is already set in `docker-compose.yml`. Any setting can be
overridden here using the double-underscore form — `Study__NewCardsPerDay=20`, for instance.

### 4. Start it

```bash
cd /srv/vocabulary-builder
docker compose up -d --build
docker compose logs -f
```

The first build takes several minutes: it restores NuGet packages, installs npm packages and
builds the React app. The container creates the database on first start and applies every
migration, so there is nothing to run by hand.

Check it is serving before going near Caddy:

```bash
curl -fsS http://127.0.0.1:8087/health && echo OK
```

Everything except `/health`, the React app and the sign-in endpoints needs a signed-in user,
so an API call without one answering `401` is the app working, not failing.

### Bringing existing words along (optional)

A database from before users existed can seed the new deployment. The migration that adds
ownership gives every word already in it to a placeholder account, and the administrator
takes that account over on first start - so the words arrive in the administrator's
collection. Do this before the first `docker compose up`, into an empty volume:

```bash
# On the machine that has the data: one self-contained file, WAL included
sqlite3 VocabularyBuilder.Prod.db ".backup vocab-seed.db"
scp vocab-seed.db deploy@vps:/srv/vocabulary-builder/

# On the VPS
docker volume create vocabulary_builder_data
docker run --rm -v vocabulary_builder_data:/data -v /srv/vocabulary-builder:/seed alpine \
  sh -c 'cp /seed/vocab-seed.db /data/VocabularyBuilder.db && chown 1654:1654 /data/VocabularyBuilder.db'
```

`1654` is the `app` user in the .NET images. The container migrates the file when it starts.

### 5. Put Caddy in front of it

As root, add to `/etc/caddy/Caddyfile`:

```
vocab.example.com {
    reverse_proxy localhost:8087
}
```

```bash
caddy validate --config /etc/caddy/Caddyfile
systemctl restart caddy
```

Caddy obtains and renews the certificate itself. The container is told it sits behind a proxy
(`BehindReverseProxy=true`), so it reads the forwarded scheme and leaves the HTTPS redirect and
HSTS header to Caddy rather than sending them twice.

Point the DNS record at the VPS before restarting Caddy, or the certificate request fails.

### 6. Load the French frequency data

The lemma tier that reduces conjugated verbs is a **silent no-op until this is imported** —
every conjugation falls through to the model instead. The file ships inside the image, and the
endpoint reads a server-side path rather than an upload. Because of that path, and because the
data is shared by every user, only the administrator may call it: sign in for a bearer token
first, using the credentials the container already has in its environment:

```bash
docker exec vocabulary-builder sh -c '
  TOKEN=$(curl -fsS -X POST http://localhost:8080/api/Users/login \
      -H "Content-Type: application/json" \
      -d "{\"email\":\"$Admin__Email\",\"password\":\"$Admin__Password\"}" \
    | sed -E "s/.*\"accessToken\":\"([^\"]+)\".*/\1/")
  curl -fsS -X POST --get -H "Authorization: Bearer $TOKEN" \
    --data-urlencode "filePath=/app/data/frequency-words-fr.txt" \
    "http://localhost:8080/api/NewWords/import-frequency?lang=fr"'
```

46,945 lemmas, about thirty seconds. It only needs doing once per database, not per deploy.
A password containing `"` or `\` will need escaping in that JSON by hand.

## Updating

```bash
su - deploy
cd /srv/vocabulary-builder/app
git pull origin main
cd ..                              # compose runs from /srv/vocabulary-builder
docker compose up -d --build
```

Migrations in the pull are applied when the new container starts. The volume is untouched, so
the data carries over.

To roll back, check out the previous commit in `app/` and rebuild. **A migration is not undone
by doing that** — if the release added one, restore the database from a backup instead.

## Backups

The volume is the only thing that cannot be rebuilt from the repo. SQLite runs in WAL mode, so
copying the `.db` file alone is not a backup: the `-wal` beside it holds recent writes. Use
`sqlite3 .backup`, which checkpoints as part of the copy:

```bash
docker exec vocabulary-builder \
  sh -c 'sqlite3 /data/VocabularyBuilder.db ".backup /data/backup.db"'

docker cp vocabulary-builder:/data/backup.db ./vocab-$(date +%F).db
docker exec vocabulary-builder rm /data/backup.db
```

Copy it off the box, and restore one once to prove the backup works.

`/data/keys` holds the keys that sign login cookies. Losing it costs nothing but a sign-in:
everyone is logged out and signs in again.

> If `sqlite3` is not in the image, stop the container first and copy `/data` wholesale from
> the volume — a stopped database has nothing outstanding in its WAL.

## Everyday commands

```bash
cd /srv/vocabulary-builder

docker compose logs -f              # follow
docker compose ps                   # state and health
docker compose restart web
docker compose down                 # stop; the volume survives
docker compose build --no-cache     # when a cached layer is suspect
```

```bash
docker exec -it vocabulary-builder sh    # a shell inside
ss -tlnp | grep -v '127.0.0'             # must show only sshd and caddy
```

## Accounts

Each user has a collection of their own: words, lists and study history are all scoped to
whoever is signed in, and one user never sees another's. The administrator is created from
`Admin__Email` and `Admin__Password`; anyone on `Registration__AllowedEmails` can register from
the login page. There is no email sending, so there is no confirmation email and no "forgot
password": Identity's reset endpoints exist but have nothing to send the code through. A
signed-in user can still change their password, through `POST /api/Users/manage/info` with
`oldPassword` and `newPassword`; one who has forgotten it has no way back in yet.

Deleting a user deletes their collection with them.

## One thing to decide before this is public

**The API explorer is served at `/api`.** `UseSwaggerUi` publishes a description of the whole
surface. Every endpoint needs a signed-in user, so it cannot be used anonymously, but it still
describes the API to anyone who reaches the site. Either gate it behind
`app.Environment.IsDevelopment()` or block the path in Caddy:

```
    handle /api/specification.json {
        respond 404
    }
```

## When something is wrong

**Container restarts in a loop** — `docker compose logs web`. The usual cause is the database:
a volume restored from elsewhere, or a migration that cannot apply. The other is the
administrator: `Could not create the administrator` or `Could not accept the administrator's
password`, followed by a rule like "Passwords must have at least one non alphanumeric
character", means `Admin__Password` does not meet the password rules. `Admin:Email is set but
Admin:Password is not` means what it says.

**Nobody can sign in, and the log warns that data "belongs to no one who can sign in"** —
`Admin__Email` is not set, so the words carried over from before users existed are still with
the placeholder account. Set it and the password, and restart.

**Everyone is signed out after every deploy** — the cookie keys are not reaching the volume.
`DataProtection__KeysPath` must point inside `/data`.

**`/health` answers but the page is blank** — the React app did not make it into the image.
Confirm `wwwroot/index.html` exists in the published output:
`docker exec vocabulary-builder ls wwwroot | head`.

**A browser is sent to a port that refuses the connection** — `BehindReverseProxy` is not set,
so the app is issuing its own HTTPS redirect. It must be `true` whenever Caddy is in front.

**"Database is locked"** — two things are writing to the same file. Only one container may
mount the volume; check for a stray container from an older deployment with `docker ps -a`.

**The build runs out of memory on a small VPS** — the React build is the heavy step. Build the
image somewhere else and push it to a registry, or add swap.
