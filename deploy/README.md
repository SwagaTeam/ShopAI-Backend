# ShopAI deployment

Production deploy is driven by `deploy/deploy.sh` and `deploy/docker-compose.prod.yml`.
GitHub Actions uploads the changed repository source over SSH, then the script starts the full stack with Docker Compose.
The server keeps both source directories under `$APP_DIR/backend` and `$APP_DIR/frontend`.

Required GitHub secret:

- `SERVER_SSH_KEY`: private SSH key for the deployment user.

Optional GitHub secret:

- `PROD_ENV_FILE`: full contents of the production `.env` file. If it is not set, the server must already have `$APP_DIR/backend/.env`.

Optional GitHub variables:

- `SERVER_HOST`: defaults to `84.252.132.226`.
- `SERVER_USER`: defaults to `sraka`.
- `SERVER_PORT`: defaults to `22`.
- `APP_DIR`: defaults to `$HOME/shopai` on the server.

The production `.env` should be based on `.env.example`.
For presigned MinIO image URLs, set `MINIO_ENDPOINT` to a public host and port, for example `84.252.132.226:9000`, unless MinIO is hidden behind another public proxy.

The first deployment needs both directories present on the server. After that, either repository workflow can update its own source directory and rebuild the shared stack.
