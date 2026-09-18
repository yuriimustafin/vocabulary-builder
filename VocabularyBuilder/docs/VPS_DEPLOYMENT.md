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

`/srv/vocabulary-builder/.env`:

```env
# Used for study content, and for the import step that reads free-form lesson notes.
# Without it those features fail; nothing else does.
OpenAI__ApiKey=<key>
```

```bash
chmod 600 /srv/vocabulary-builder/.env
```

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
endpoint reads a server-side path rather than an upload:

```bash
docker exec vocabulary-builder \
  curl -fsS -X POST --get \
  --data-urlencode "filePath=/app/data/frequency-words-fr.txt" \
  "http://localhost:8080/api/NewWords/import-frequency?lang=fr"
```

46,945 lemmas, about thirty seconds. It only needs doing once per database, not per deploy.

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

## Two things to decide before this is public

**The API explorer is served at `/api`.** `UseSwaggerUi` publishes the whole surface, including
the endpoints that write. It is reachable by anyone who reaches the site. Either gate it behind
`app.Environment.IsDevelopment()` or block the path in Caddy:

```
    handle /api/specification.json {
        respond 404
    }
```

**There is no authentication on the vocabulary endpoints.** Identity is wired up, but the API
and the SPA are open to whoever has the URL. On a public domain that means anyone can read and
change the collection. Options, roughly in order of effort: keep the hostname unadvertised,
put Caddy `basic_auth` in front of everything, or require an authenticated user on the
endpoints.

## When something is wrong

**Container restarts in a loop** — `docker compose logs web`. The usual cause is the database:
a volume restored from elsewhere, or a migration that cannot apply.

**`/health` answers but the page is blank** — the React app did not make it into the image.
Confirm `wwwroot/index.html` exists in the published output:
`docker exec vocabulary-builder ls wwwroot | head`.

**A browser is sent to a port that refuses the connection** — `BehindReverseProxy` is not set,
so the app is issuing its own HTTPS redirect. It must be `true` whenever Caddy is in front.

**"Database is locked"** — two things are writing to the same file. Only one container may
mount the volume; check for a stray container from an older deployment with `docker ps -a`.

**The build runs out of memory on a small VPS** — the React build is the heavy step. Build the
image somewhere else and push it to a registry, or add swap.
