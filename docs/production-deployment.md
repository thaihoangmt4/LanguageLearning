# Backend production deployment

The `Backend Production CI/CD` workflow runs on pushes to `main` and by manual dispatch. It restores, builds, and tests `LanguageLearning.slnx`; builds the existing backend Dockerfile for `linux/arm64`; publishes `latest` and full Git commit SHA tags to `ghcr.io/<repository-owner>/language-learning-backend`; and deploys the SHA-tagged image to EC2. A failed CI or image publication prevents deployment.

## GitHub configuration

Configure these repository or production-environment secrets:

- `EC2_HOST`: EC2 hostname or public IP.
- `EC2_USER`: dedicated deployment user.
- `EC2_SSH_PRIVATE_KEY`: private half of a dedicated CI/CD SSH key. Its public key must be in the deployment user's `~/.ssh/authorized_keys` on EC2.
- `EC2_KNOWN_HOSTS` (recommended): verified `known_hosts` entry for the EC2 SSH host. Obtain and verify the fingerprint through a trusted channel before storing it.

If `EC2_KNOWN_HOSTS` is absent, the workflow obtains the presented key with `ssh-keyscan`. This enables initial setup but does not independently authenticate the host and is less secure than a pre-verified secret.

The workflow uses `GITHUB_TOKEN` with `packages: write` only in the image-publishing job. No GHCR personal access token is needed in GitHub Actions.

## One-time EC2 prerequisites

The ARM64 Ubuntu host must have Docker Engine, the Docker Compose plugin, `curl`, and the deployment user's SSH public key. The deployment user must be able to run Docker without an interactive prompt and must own or have write access to this directory:

```text
/opt/language-learning-backend
```

Create `/opt/language-learning-backend/.env` from the documented keys in `.env.example`. Keep that file server-owned and uncommitted. The workflow copies only `docker-compose.prod.yml` and never overwrites `.env`.

The EC2 host must be able to pull the GHCR package. Public GHCR container packages support anonymous pulls and are the simplest option for a portfolio deployment. If the package remains private, perform a one-time `docker login ghcr.io` as the deployment user with a dedicated credential that has read-only `read:packages` access. Do not store that credential in Compose or `.env`.

## Deployment and health behavior

The deployment passes the current workflow SHA as `BACKEND_IMAGE_TAG` directly to each Compose command. It pulls only the backend image and then runs `docker compose up -d`, preserving the PostgreSQL named volume and starting PostgreSQL or Redis if needed. It never builds source, runs destructive Docker cleanup, or changes the production `.env`.

The workflow retries `http://127.0.0.1:5000/health/live` 30 times at five-second intervals. On failure, it prints Compose status and the last 100 backend log lines, then fails. `/health/ready` is intentionally not the automated deployment gate because a schema migration may still be pending.

## Manual database migration

The pipeline never applies database migrations. When a release requires one, an operator must intentionally call `POST /api/system/database/migrate` with the configured `X-Migration-Key`, then verify `/health/ready`. Keep `Migration__Enabled` disabled except for the controlled migration operation.

## Manual rollback

To redeploy a previously published SHA, run on EC2:

```bash
cd /opt/language-learning-backend
BACKEND_IMAGE=ghcr.io/<repository-owner>/language-learning-backend \
BACKEND_IMAGE_TAG=<previous-full-sha> \
docker compose -f docker-compose.prod.yml --env-file .env pull backend

BACKEND_IMAGE=ghcr.io/<repository-owner>/language-learning-backend \
BACKEND_IMAGE_TAG=<previous-full-sha> \
docker compose -f docker-compose.prod.yml --env-file .env up -d backend
```

Application rollback may be unsafe after an incompatible manual database migration. Do not automatically migrate the database down.
